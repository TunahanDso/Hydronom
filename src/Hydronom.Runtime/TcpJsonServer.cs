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

            var position3D = new Vec3(
                Sanitize(dto.Pose.X),
                Sanitize(dto.Pose.Y),
                Sanitize(dto.Pose.Z)
            );

            var pos2D = new Vec2(position3D.X, position3D.Y);

            var rollDeg = PickFirstFinite(
                dto.Pose.RollDeg,
                dto.Pose.Roll,
                dto.Pose.R
            );

            var pitchDeg = PickFirstFinite(
                dto.Pose.PitchDeg,
                dto.Pose.Pitch,
                dto.Pose.P
            );

            var yawDeg = PickFirstFinite(
                dto.Pose.YawDeg,
                dto.Pose.Yaw,
                dto.Pose.HeadingDeg,
                dto.Pose.Heading
            );

            var orientation = new Orientation(rollDeg, pitchDeg, yawDeg);

            var linearVelocity = dto.Twist is null
                ? Vec3.Zero
                : new Vec3(
                    Sanitize(dto.Twist.Vx),
                    Sanitize(dto.Twist.Vy),
                    Sanitize(dto.Twist.Vz)
                );

            var angularVelocityDegSec = dto.Twist is null
                ? Vec3.Zero
                : new Vec3(
                    PickFirstFinite(dto.Twist.RollRateDegSec, dto.Twist.Roll_Rate_Deg_Sec, dto.Twist.Roll_Rate, dto.Twist.Wx),
                    PickFirstFinite(dto.Twist.PitchRateDegSec, dto.Twist.Pitch_Rate_Deg_Sec, dto.Twist.Pitch_Rate, dto.Twist.Wy),
                    PickFirstFinite(dto.Twist.YawRateDegSec, dto.Twist.Yaw_Rate_Deg_Sec, dto.Twist.Yaw_Rate, dto.Twist.Wz)
                );

            DateTime tsUtc = DateTime.UtcNow;
            if (dto.T.HasValue && dto.T.Value > 0)
            {
                tsUtc = DateTime.UnixEpoch.AddSeconds(dto.T.Value);
            }

            var obstacles = new List<Obstacle>();
            var spatialObstacles = new List<SpatialObstacle>();

            if (dto.Obstacles is not null)
            {
                foreach (var o in dto.Obstacles)
                {
                    if (o is null) continue;

                    if (!TryBuildSpatialObstacle(o, "fused-state", out var spatial))
                        continue;

                    spatialObstacles.Add(spatial);
                    obstacles.Add(spatial.ToLegacy2D());
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

                        var roPosition = new Vec3(
                            Sanitize(ro.X),
                            Sanitize(ro.Y),
                            Sanitize(ro.Z)
                        );

                        var spatial = new SpatialObstacle(
                            Position: roPosition,
                            RadiusM: SafeRadius(ro.R),
                            SizeM: null,
                            Orientation: null,
                            Kind: ro.Kind ?? "RuntimeObstacle",
                            Medium: ro.Medium,
                            Source: sourceName
                        );

                        spatialObstacles.Add(spatial);
                        obstacles.Add(spatial.ToLegacy2D());
                    }
                }
            }

            spatialObstacles = DeduplicateSpatialObstacles(spatialObstacles, 0.15);
            obstacles = new List<Obstacle>(spatialObstacles.Count);
            foreach (var so in spatialObstacles)
                obstacles.Add(so.ToLegacy2D());

            Vec2? target2D = null;
            Vec3? target3D = null;

            if (dto.Target is not null)
            {
                target3D = ReadVec3(dto.Target);
                target2D = new Vec2(target3D.Value.X, target3D.Value.Y);
            }
            else if (dto.Goal is not null)
            {
                target3D = ReadVec3(dto.Goal);
                target2D = new Vec2(target3D.Value.X, target3D.Value.Y);
            }

            if (VerboseObstacleDebug)
            {
                Console.WriteLine(
                    $"[TCP-OBS] pos=({pos2D.X:0.00},{pos2D.Y:0.00},z={position3D.Z:0.00}) " +
                    $"rpy=({orientation.RollDeg:0.0},{orientation.PitchDeg:0.0},{orientation.YawDeg:0.0}) " +
                    $"obs2d={obstacles.Count} obs3d={spatialObstacles.Count}"
                );

                foreach (var o in spatialObstacles)
                {
                    Console.WriteLine(
                        $"[TCP-OBS] obstacle3d ({o.Position.X:0.00},{o.Position.Y:0.00},{o.Position.Z:0.00}) " +
                        $"r={o.RadiusM:0.00} kind={o.Kind ?? "-"} medium={o.Medium ?? "-"} src={o.Source ?? "-"}"
                    );
                }
            }

            frame = new FusedFrame(
                TimestampUtc: tsUtc,
                Position: pos2D,
                HeadingDeg: orientation.YawDeg,
                Obstacles: obstacles,
                Target: target2D
            )
            {
                Position3D = position3D,
                Orientation = orientation,
                LinearVelocity = linearVelocity,
                AngularVelocityDegSec = angularVelocityDegSec,
                SpatialObstacles = spatialObstacles,
                Target3D = target3D
            };

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

    private static List<SpatialObstacle> DeduplicateSpatialObstacles(List<SpatialObstacle> src, double mergeDistance)
    {
        if (src.Count <= 1) return src;

        var outList = new List<SpatialObstacle>();

        foreach (var o in src)
        {
            bool merged = false;

            for (int i = 0; i < outList.Count; i++)
            {
                var k = outList[i];

                var dx = o.Position.X - k.Position.X;
                var dy = o.Position.Y - k.Position.Y;
                var dz = o.Position.Z - k.Position.Z;
                var d = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (d <= mergeDistance)
                {
                    var mergedPosition = new Vec3(
                        (o.Position.X + k.Position.X) * 0.5,
                        (o.Position.Y + k.Position.Y) * 0.5,
                        (o.Position.Z + k.Position.Z) * 0.5
                    );

                    outList[i] = new SpatialObstacle(
                        Position: mergedPosition,
                        RadiusM: Math.Max(o.RadiusM, k.RadiusM),
                        SizeM: o.SizeM ?? k.SizeM,
                        Orientation: o.Orientation ?? k.Orientation,
                        Kind: o.Kind ?? k.Kind,
                        Medium: o.Medium ?? k.Medium,
                        Source: o.Source ?? k.Source
                    );

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

            var position3D = ReadVec3(dto.Position);
            var pos2D = new Vec2(position3D.X, position3D.Y);

            Vec3? target3D = dto.Target is null ? null : ReadVec3(dto.Target);
            Vec2? target2D = target3D.HasValue
                ? new Vec2(target3D.Value.X, target3D.Value.Y)
                : null;

            var obsList = new List<Obstacle>();
            var spatialList = new List<SpatialObstacle>();

            if (dto.Obstacles != null)
            {
                foreach (var o in dto.Obstacles)
                {
                    if (o is null) continue;

                    if (!TryBuildSpatialObstacle(o, "legacy-frame", out var spatial))
                        continue;

                    spatialList.Add(spatial);
                    obsList.Add(spatial.ToLegacy2D());
                }
            }

            var rollDeg = PickFirstFinite(dto.RollDeg);
            var pitchDeg = PickFirstFinite(dto.PitchDeg);
            var yawDeg = PickFirstFinite(dto.YawDeg, dto.HeadingDeg);

            frame = new FusedFrame(
                TimestampUtc: dto.TimestampUtc == default ? DateTime.UtcNow : dto.TimestampUtc,
                Position: pos2D,
                HeadingDeg: yawDeg,
                Obstacles: obsList,
                Target: target2D
            )
            {
                Position3D = position3D,
                Orientation = new Orientation(rollDeg, pitchDeg, yawDeg),
                LinearVelocity = dto.LinearVelocity is null ? Vec3.Zero : ReadVec3(dto.LinearVelocity),
                AngularVelocityDegSec = dto.AngularVelocityDegSec is null ? Vec3.Zero : ReadVec3(dto.AngularVelocityDegSec),
                SpatialObstacles = spatialList,
                Target3D = target3D
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildSpatialObstacle(ObstacleDto dto, string source, out SpatialObstacle spatial)
    {
        spatial = new SpatialObstacle(Vec3.Zero, 0.0);

        Vec3 position;
        if (dto.Position3D is not null)
        {
            position = ReadVec3(dto.Position3D);
        }
        else if (dto.Position is not null)
        {
            position = ReadVec3(dto.Position);
        }
        else
        {
            return false;
        }

        Orientation? orientation = dto.Orientation is null
            ? null
            : new Orientation(
                PickFirstFinite(dto.Orientation.RollDeg, dto.Orientation.Roll),
                PickFirstFinite(dto.Orientation.PitchDeg, dto.Orientation.Pitch),
                PickFirstFinite(dto.Orientation.YawDeg, dto.Orientation.Yaw, dto.Orientation.HeadingDeg)
            );

        spatial = new SpatialObstacle(
            Position: position,
            RadiusM: SafeRadius(dto.RadiusM),
            SizeM: dto.SizeM is null ? null : ReadVec3(dto.SizeM),
            Orientation: orientation,
            Kind: dto.Kind,
            Medium: dto.Medium,
            Source: dto.Source ?? source
        );

        return true;
    }

    private static Vec3 ReadVec3(Vec3Dto dto)
    {
        return new Vec3(
            Sanitize(dto.X),
            Sanitize(dto.Y),
            Sanitize(dto.Z)
        );
    }

    private static double Sanitize(double value, double fallback = 0.0)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double SafeRadius(double value)
    {
        value = Sanitize(value, 0.0);
        return value < 0.0 ? 0.0 : value;
    }

    private static double PickFirstFinite(params double?[] values)
    {
        foreach (var value in values)
        {
            if (value.HasValue && double.IsFinite(value.Value))
                return value.Value;
        }

        return 0.0;
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
        public Vec3Dto? Target { get; set; }
        public Vec3Dto? Goal { get; set; }
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
        public double Z { get; set; }
        public double R { get; set; }
        public string? Kind { get; set; }
        public string? Medium { get; set; }
    }

    private sealed class PoseDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double? Roll { get; set; }
        public double? Pitch { get; set; }
        public double? Yaw { get; set; }

        public double? R { get; set; }
        public double? P { get; set; }

        public double? RollDeg { get; set; }
        public double? PitchDeg { get; set; }
        public double? YawDeg { get; set; }

        public double? Heading { get; set; }
        public double? HeadingDeg { get; set; }
    }

    private sealed class TwistDto
    {
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Vz { get; set; }

        public double? Wx { get; set; }
        public double? Wy { get; set; }
        public double? Wz { get; set; }

        public double? Roll_Rate { get; set; }
        public double? Pitch_Rate { get; set; }
        public double? Yaw_Rate { get; set; }

        public double? RollRateDegSec { get; set; }
        public double? PitchRateDegSec { get; set; }
        public double? YawRateDegSec { get; set; }

        public double? Roll_Rate_Deg_Sec { get; set; }
        public double? Pitch_Rate_Deg_Sec { get; set; }
        public double? Yaw_Rate_Deg_Sec { get; set; }
    }

    private sealed class FusedFrameDto
    {
        public DateTime TimestampUtc { get; set; }

        public Vec3Dto? Position { get; set; }

        public double HeadingDeg { get; set; }

        public double? RollDeg { get; set; }
        public double? PitchDeg { get; set; }
        public double? YawDeg { get; set; }

        public Vec3Dto? LinearVelocity { get; set; }
        public Vec3Dto? AngularVelocityDegSec { get; set; }

        public List<ObstacleDto>? Obstacles { get; set; }

        public Vec3Dto? Target { get; set; }
    }

    private sealed class Vec3Dto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    private sealed class ObstacleDto
    {
        public Vec3Dto? Position { get; set; }
        public Vec3Dto? Position3D { get; set; }

        public double RadiusM { get; set; }

        public Vec3Dto? SizeM { get; set; }
        public OrientationDto? Orientation { get; set; }

        public string? Kind { get; set; }
        public string? Medium { get; set; }
        public string? Source { get; set; }
    }

    private sealed class OrientationDto
    {
        public double? Roll { get; set; }
        public double? Pitch { get; set; }
        public double? Yaw { get; set; }

        public double? RollDeg { get; set; }
        public double? PitchDeg { get; set; }
        public double? YawDeg { get; set; }

        public double? HeadingDeg { get; set; }
    }
}