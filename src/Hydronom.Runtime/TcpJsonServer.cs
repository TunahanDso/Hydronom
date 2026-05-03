using Hydronom.Core.Domain;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class TcpJsonServer
{
    private readonly string _host;
    private readonly int _port;

    private readonly bool _logRawFrames;

    private readonly Action<FusedFrame>? _onFrame;
    private readonly Action<bool, IReadOnlyList<CapabilitySensorInfo>>? _onCapability;

    private readonly bool _sendDefaultSubscribe;
    private readonly DefaultSubscribeOptions _defaultSub;

    private static bool VerboseCapabilityLogging =>
        Environment.GetEnvironmentVariable("HYDRONOM_VERBOSE_CAP") == "1";

    private static bool VerboseObstacleDebug =>
        Environment.GetEnvironmentVariable("HYDRONOM_VERBOSE_OBS") == "1";

    private readonly object _writersGate = new();
    private readonly HashSet<StreamWriter> _writers = new();

    // Her writer için ayrı yazma kilidi
    private readonly object _writerLocksGate = new();
    private readonly Dictionary<StreamWriter, SemaphoreSlim> _writerLocks = new();

    // Runtime içine gelen ham satırları dosyaya dökmek için
    private static readonly object _ingressDumpGate = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public readonly record struct CapabilitySensorInfo(
        string? Sensor,
        string? Source,
        string? FrameId,
        double? RateHz,
        bool? Simulate,
        string? Backend,
        string? SimSource
    );

    public readonly record struct DefaultSubscribeOptions(
        bool WantFusedState,
        bool WantExternalState,
        double FusedHz,
        double ExternalHz,
        string PublishSamples
    );

    public TcpJsonServer(
        string host,
        int port,
        Action<FusedFrame>? onFrame = null,
        Action<bool, IReadOnlyList<CapabilitySensorInfo>>? onCapability = null,
        bool logRawFrames = true,
        bool sendDefaultSubscribe = true,
        DefaultSubscribeOptions? defaultSubscribe = null)
    {
        _host = host;
        _port = port;
        _onFrame = onFrame;
        _onCapability = onCapability;
        _logRawFrames = logRawFrames;

        _sendDefaultSubscribe = sendDefaultSubscribe;

        _defaultSub = defaultSubscribe ?? new DefaultSubscribeOptions(
            WantFusedState: true,
            WantExternalState: false,
            FusedHz: 10.0,
            ExternalHz: 10.0,
            PublishSamples: "imu-gps"
        );
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var ip = IPAddress.Parse(_host);
        var listener = new TcpListener(ip, _port);
        listener.Start();
        Console.WriteLine($"[TCP] Listening on {_host}:{_port} ...");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                Console.WriteLine("[TCP] Client connected.");
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        finally
        {
            try { listener.Stop(); } catch { }
            Console.WriteLine("[TCP] Listener stopped.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true))
        using (var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            4096,
            leaveOpen: true)
        { AutoFlush = true })
        {
            RegisterWriter(writer);
            DumpIngress("[TCP-DEBUG] HandleClientAsync started");

            using var ctReg = ct.Register(() =>
            {
                try { client.Close(); } catch { }
            });

            if (_sendDefaultSubscribe)
            {
                await SendDefaultSubscribeAsync(writer, _defaultSub).ConfigureAwait(false);
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync().ConfigureAwait(false);
                        DumpIngress($"[TCP-DEBUG] ReadLine returned null={line is null}");
                    }
                    catch
                    {
                        break;
                    }

                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (_logRawFrames)
                    {
                        var len = line.Length;
                        var head = line.Length <= 120 ? line : line.Substring(0, 120);
                        var tail = line.Length <= 120 ? line : line.Substring(Math.Max(0, line.Length - 120));

                        Console.WriteLine($"[TCP-RAW] len={len}");
                        Console.WriteLine($"[TCP-RAW-HEAD] {head}");
                        Console.WriteLine($"[TCP-RAW-TAIL] {tail}");
                    }

                    // 1) Önce bu satırı diğer bağlı client'lara aynala.
                    //    Gönderen writer'a geri yollamıyoruz.
                    await BroadcastExceptAsync(line, writer).ConfigureAwait(false);

                    // 2) Sonra runtime callback mantığını çalıştır.
                    var mtype = FastExtractType(line);

                    // Sadece önemli frame tiplerini dosyaya dök
                    if (string.Equals(mtype, "FusedState", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mtype, "Capability", StringComparison.OrdinalIgnoreCase))
                    {
                        var len = line.Length;
                        var head = line.Length <= 160 ? line : line.Substring(0, 160);
                        var tail = line.Length <= 160 ? line : line.Substring(Math.Max(0, line.Length - 160));

                        DumpIngress($"[IN] type={mtype} len={len}");
                        DumpIngress($"[IN-HEAD] {head}");
                        DumpIngress($"[IN-TAIL] {tail}");
                    }

                    if (!string.IsNullOrEmpty(mtype))
                    {
                        if (mtype.Equals("Capability", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryParseCapability(line, out var capPref, out var capSensors))
                            {
                                LogCapability(capPref, capSensors);

                                if (_onCapability is not null)
                                {
                                    var infos = new List<CapabilitySensorInfo>(capSensors.Count);
                                    foreach (var s in capSensors)
                                    {
                                        var ns = NormalizeCapSensor(s);

                                        infos.Add(new CapabilitySensorInfo(
                                            ns.Sensor,
                                            ns.Source,
                                            ns.FrameId,
                                            ns.RateHz,
                                            ns.Simulate,
                                            ns.Backend,
                                            ns.SimSource
                                        ));
                                    }

                                    _onCapability.Invoke(capPref, infos);
                                }
                            }
                            else
                            {
                                Console.WriteLine("[TCP] Capability parse FAILED.");
                                DumpIngress("[PARSE-FAIL] Capability parse FAILED.");
                            }

                            continue;
                        }
                        else if (mtype.Equals("FusedState", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryTranslateFusedStateToFrame(line, out var ff) && ff is not null)
                            {
                                _onFrame?.Invoke(ff);
                            }
                            else
                            {
                                Console.WriteLine("[TCP] FusedState translate FAILED.");
                                DumpIngress("[PARSE-FAIL] FusedState translate FAILED.");
                            }

                            continue;
                        }
                    }

                    // 3) Eski FusedFrame formatı desteği
                    if (TryDeserializeFrame(line, out var frame) && frame is not null)
                    {
                        _onFrame?.Invoke(frame);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[TCP] Client IO error: {ex.Message}");
                DumpIngress($"[TCP-IO-ERROR] {ex.Message}");
            }
            finally
            {
                UnregisterWriter(writer);
                Console.WriteLine("[TCP] Client disconnected.");
                DumpIngress("[TCP-DEBUG] Client disconnected");
            }
        }
    }

    public Task BroadcastAsync(string jsonLine)
    {
        var line = NormalizeNdjsonLine(jsonLine);

        List<StreamWriter> snap;
        lock (_writersGate)
            snap = new List<StreamWriter>(_writers);

        foreach (var w in snap)
            _ = SafeWriteAsync(w, line);

        return Task.CompletedTask;
    }

    public Task BroadcastAsync(object obj)
    {
        var s = JsonSerializer.Serialize(obj, JsonOpts);
        return BroadcastAsync(s);
    }

    private Task BroadcastExceptAsync(string jsonLine, StreamWriter exceptWriter)
    {
        var line = NormalizeNdjsonLine(jsonLine);

        List<StreamWriter> snap;
        lock (_writersGate)
            snap = new List<StreamWriter>(_writers);

        foreach (var w in snap)
        {
            if (ReferenceEquals(w, exceptWriter))
                continue;

            _ = SafeWriteAsync(w, line);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeNdjsonLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return "\n";

        if (line.Contains('\r'))
            line = line.Replace("\r", "");

        if (line.Contains('\n'))
            line = line.Replace("\n", "\\n");

        if (!line.EndsWith("\n", StringComparison.Ordinal))
            line += "\n";

        return line;
    }

    private async Task SafeWriteAsync(StreamWriter w, string lineWithNewline)
    {
        SemaphoreSlim? sem;
        lock (_writerLocksGate)
        {
            _writerLocks.TryGetValue(w, out sem);
        }

        if (sem is null)
        {
            UnregisterWriter(w);
            return;
        }

        await sem.WaitAsync().ConfigureAwait(false);
        try
        {
            await w.WriteAsync(lineWithNewline).ConfigureAwait(false);
            await w.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            UnregisterWriter(w);
        }
        finally
        {
            try
            {
                sem.Release();
            }
            catch
            {
            }
        }
    }

    private void RegisterWriter(StreamWriter w)
    {
        lock (_writersGate)
            _writers.Add(w);

        lock (_writerLocksGate)
            _writerLocks[w] = new SemaphoreSlim(1, 1);
    }

    private void UnregisterWriter(StreamWriter w)
    {
        lock (_writersGate)
            _writers.Remove(w);

        SemaphoreSlim? sem = null;
        lock (_writerLocksGate)
        {
            if (_writerLocks.TryGetValue(w, out sem))
                _writerLocks.Remove(w);
        }

        try
        {
            sem?.Dispose();
        }
        catch
        {
        }
    }

    private static async Task SendDefaultSubscribeAsync(StreamWriter w, DefaultSubscribeOptions opt)
    {
        var streams = new List<string>();
        var rate = new Dictionary<string, double>();

        if (opt.WantFusedState)
        {
            streams.Add("FusedState");
            rate["FusedState"] = opt.FusedHz;
        }

        if (opt.WantExternalState)
        {
            streams.Add("ExternalState");
            rate["ExternalState"] = opt.ExternalHz;
        }

        var payload = new
        {
            type = "StreamSubscribe",
            streams = streams.ToArray(),
            rate_hz = rate,
            publish_samples = opt.PublishSamples,
            external = opt.WantExternalState
        };

        var line = JsonSerializer.Serialize(payload, JsonOpts);
        try
        {
            await w.WriteAsync(NormalizeNdjsonLine(line)).ConfigureAwait(false);
            await w.FlushAsync().ConfigureAwait(false);

            Console.WriteLine(
                $"[TCP] → sent StreamSubscribe ({string.Join(",", streams)} | " +
                $"Fused={opt.FusedHz:0.#}Hz External={(opt.WantExternalState ? opt.ExternalHz.ToString("0.#") : "off")}Hz)"
            );
        }
        catch
        {
        }
    }

    private static string FastExtractType(string line)
    {
        try
        {
            int idx = line.IndexOf("\"type\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                idx = line.IndexOf("\"Type\"", StringComparison.Ordinal);

            if (idx < 0) return "";

            var colon = line.IndexOf(':', idx);
            if (colon < 0) return "";

            var q1 = line.IndexOf('"', colon + 1);
            if (q1 < 0) return "";

            var q2 = line.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";

            return line.Substring(q1 + 1, q2 - q1 - 1);
        }
        catch
        {
            return "";
        }
    }

    private static bool TryParseCapability(string line, out bool preferExternalState, out List<CapSensorDto> sensors)
    {
        preferExternalState = true;
        sensors = new List<CapSensorDto>();

        try
        {
            var dto = JsonSerializer.Deserialize<CapabilityDto>(line, JsonOpts);
            if (dto is null) return false;

            if (dto.PreferExternalState.HasValue)
                preferExternalState = dto.PreferExternalState.Value;

            if (dto.Sensors is not null)
                sensors = dto.Sensors;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogCapability(bool preferExternal, List<CapSensorDto> sensors)
    {
        if (!VerboseCapabilityLogging)
        {
            int count = sensors?.Count ?? 0;
            int simCount = 0;
            int realCount = 0;

            if (sensors is not null)
            {
                foreach (var s in sensors)
                {
                    if (s.Simulate.HasValue)
                    {
                        if (s.Simulate.Value) simCount++;
                        else realCount++;
                    }
                }
            }

            Console.WriteLine($"[CAP] prefer_external_state={preferExternal} sensors={count} sim={simCount} real={realCount}");
            return;
        }

        if (sensors is null || sensors.Count == 0)
        {
            Console.WriteLine($"[CAP] prefer_external_state={preferExternal} (sensors: none)");
            return;
        }

        Console.WriteLine($"[CAP] prefer_external_state={preferExternal} sensors={sensors.Count}");
        foreach (var s in sensors)
        {
            var ns = NormalizeCapSensor(s);

            var sens = ns.Sensor ?? "?";
            var src = ns.Source ?? "?";
            var fid = ns.FrameId ?? "?";
            var rate = ns.RateHz.HasValue ? $"{ns.RateHz:0.#}Hz" : "-";
            var sim = ns.Simulate.HasValue ? (ns.Simulate.Value ? "sim" : "real") : "?";
            var backend = ns.Backend ?? "-";
            var simsrc = ns.SimSource ?? "-";

            Console.WriteLine($"[CAP]  - {sens}:{src} frame={fid} rate={rate} mode={sim} backend={backend} sim_source={simsrc}");
        }
    }

    private static CapSensorDto NormalizeCapSensor(CapSensorDto s)
    {
        if (string.IsNullOrWhiteSpace(s.SimSource) && !string.IsNullOrWhiteSpace(s.Sim_Source))
            s.SimSource = s.Sim_Source;

        return s;
    }

    private static bool TryTranslateFusedStateToFrame(string line, out FusedFrame? frame)
    {
        frame = null;

        try
        {
            var dto = JsonSerializer.Deserialize<FusedStateDto>(line, JsonOpts);
            if (dto is null || dto.Pose is null) return false;

            var pos = new Vec2(dto.Pose.X, dto.Pose.Y);
            var headDeg = dto.Pose.Yaw;

            DateTime tsUtc = DateTime.UtcNow;
            if (dto.T.HasValue && dto.T.Value > 0)
            {
                tsUtc = DateTime.UnixEpoch.AddSeconds(dto.T.Value);
            }

            var obstacles = new List<Obstacle>();

            if (dto.Obstacles is not null)
            {
                foreach (var o in dto.Obstacles)
                {
                    if (o?.Position is null) continue;

                    obstacles.Add(new Obstacle(
                        new Vec2(o.Position.X, o.Position.Y),
                        o.RadiusM
                    ));
                }
            }

            if (dto.Inputs is not null)
            {
                foreach (var input in dto.Inputs)
                {
                    if (input is null) continue;

                    var sourceName = input.SourceName;
                    if (!string.Equals(sourceName, "runtime_obstacles", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(sourceName, "lidar_runtime_obstacles", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var obs = input.Data?.Obstacles;
                    if (obs is null) continue;

                    foreach (var ro in obs)
                    {
                        if (ro is null) continue;

                        obstacles.Add(new Obstacle(
                            new Vec2(ro.X, ro.Y),
                            ro.R
                        ));
                    }
                }
            }

            obstacles = DeduplicateObstacles(obstacles, 0.15);

            Vec2? target = null;
            if (dto.Target is not null)
            {
                target = new Vec2(dto.Target.X, dto.Target.Y);
            }
            else if (dto.Goal is not null)
            {
                target = new Vec2(dto.Goal.X, dto.Goal.Y);
            }

            if (VerboseObstacleDebug)
            {
                Console.WriteLine($"[TCP-OBS] pos=({pos.X:0.00},{pos.Y:0.00}) head={headDeg:0.0} obs={obstacles.Count}");
                foreach (var o in obstacles)
                {
                    Console.WriteLine($"[TCP-OBS] obstacle ({o.Position.X:0.00},{o.Position.Y:0.00}) r={o.RadiusM:0.00}");
                }
            }

            frame = new FusedFrame(
                TimestampUtc: tsUtc,
                Position: pos,
                HeadingDeg: headDeg,
                Obstacles: obstacles,
                Target: target
            );
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCP] FusedState parse exception: {ex.Message}");
            DumpIngress($"[PARSE-EX] FusedState parse exception: {ex.Message}");
            return false;
        }
    }

    private static List<Obstacle> DeduplicateObstacles(List<Obstacle> src, double mergeDistance)
    {
        if (src.Count <= 1) return src;

        var outList = new List<Obstacle>();

        foreach (var o in src)
        {
            bool merged = false;

            for (int i = 0; i < outList.Count; i++)
            {
                var k = outList[i];
                var dx = o.Position.X - k.Position.X;
                var dy = o.Position.Y - k.Position.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);

                if (d <= mergeDistance)
                {
                    var mx = (o.Position.X + k.Position.X) * 0.5;
                    var my = (o.Position.Y + k.Position.Y) * 0.5;
                    var mr = Math.Max(o.RadiusM, k.RadiusM);

                    outList[i] = new Obstacle(new Vec2(mx, my), mr);
                    merged = true;
                    break;
                }
            }

            if (!merged)
                outList.Add(o);
        }

        return outList;
    }

    private static bool TryDeserializeFrame(string line, out FusedFrame? frame)
    {
        frame = null;
        try
        {
            var dto = JsonSerializer.Deserialize<FusedFrameDto>(line, JsonOpts);
            if (dto == null || dto.Position is null) return false;

            var pos = new Vec2(dto.Position.X, dto.Position.Y);
            Vec2? target = dto.Target is null ? null : new Vec2(dto.Target.X, dto.Target.Y);

            var obsList = new List<Obstacle>();
            if (dto.Obstacles != null)
            {
                foreach (var o in dto.Obstacles)
                {
                    if (o.Position is null) continue;
                    var p = new Vec2(o.Position.X, o.Position.Y);
                    obsList.Add(new Obstacle(p, o.RadiusM));
                }
            }

            frame = new FusedFrame(
                TimestampUtc: dto.TimestampUtc == default ? DateTime.UtcNow : dto.TimestampUtc,
                Position: pos,
                HeadingDeg: dto.HeadingDeg,
                Obstacles: obsList,
                Target: target
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DumpIngress(string text)
    {
        try
        {
            lock (_ingressDumpGate)
            {
                File.AppendAllText("runtime_ingress_dump.log", text + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private sealed class CapabilityDto
    {
        public string? Type { get; set; }
        public string? Schema_Version { get; set; }
        public double? T { get; set; }
        public string? Node { get; set; }
        public string? Version { get; set; }
        public List<string>? Streams { get; set; }
        public bool? PreferExternalState { get; set; }
        public List<CapSensorDto>? Sensors { get; set; }
    }

    private sealed class CapSensorDto
    {
        public string? Sensor { get; set; }
        public string? Source { get; set; }
        public string? FrameId { get; set; }
        public double? RateHz { get; set; }
        public bool? Simulate { get; set; }
        public string? Backend { get; set; }
        public string? Sim_Source { get; set; }
        public string? SimSource { get; set; }
    }

    private sealed class FusedStateDto
    {
        public string? Type { get; set; }
        public double? T { get; set; }
        public PoseDto? Pose { get; set; }
        public TwistDto? Twist { get; set; }
        public List<object>? Landmarks { get; set; }
        public List<FusedInputDto>? Inputs { get; set; }
        public List<ObstacleDto>? Obstacles { get; set; }
        public Vec2Dto? Target { get; set; }
        public Vec2Dto? Goal { get; set; }
        public double? Confidence { get; set; }
    }

    private sealed class FusedInputDto
    {
        public string? _source { get; set; }
        public string? Source { get; set; }
        public RuntimeObstacleDataDto? Data { get; set; }

        public string? SourceName =>
            !string.IsNullOrWhiteSpace(Source) ? Source : _source;
    }

    private sealed class RuntimeObstacleDataDto
    {
        public List<RuntimeObstacleDto>? Obstacles { get; set; }
    }

    private sealed class RuntimeObstacleDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double R { get; set; }
    }

    private sealed class PoseDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Yaw { get; set; }
    }

    private sealed class TwistDto
    {
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Vz { get; set; }
        public double Yaw_Rate { get; set; }
    }

    private sealed class FusedFrameDto
    {
        public DateTime TimestampUtc { get; set; }
        public Vec2Dto? Position { get; set; }
        public double HeadingDeg { get; set; }
        public List<ObstacleDto>? Obstacles { get; set; }
        public Vec2Dto? Target { get; set; }
    }

    private sealed class Vec2Dto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    private sealed class ObstacleDto
    {
        public Vec2Dto? Position { get; set; }
        public double RadiusM { get; set; }
    }
}
