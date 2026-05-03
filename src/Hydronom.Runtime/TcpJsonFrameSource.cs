using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Buses; // IExternalPoseProvider, ExternalPose

//
// TCP TABANLI FRAME KAYNAÄI
// ------------------------------------------------------------
// AmaÃ§:
//  - TcpJsonServer Ã¼zerinden gelen FusedFrame'leri alÄ±p "son taze Ã§erÃ§eve" olarak saklamak.
//  - IFrameSource arayÃ¼zÃ¼ ile runtime'a kaynak-agnostik tÃ¼ketim saÄŸlamak.
//  - Gerekirse aynÄ± TCP sunucusu Ã¼zerinden twin publisher gibi ek modÃ¼llerin de
//    yayÄ±n yapabilmesi iÃ§in server Ã¶rneÄŸini dÄ±ÅŸarÄ± aÃ§mak.
//
// DeÄŸiÅŸiklikler:
//  - TcpJsonServer arka planda Task ile Ã§alÄ±ÅŸÄ±r. âœ…
//  - IExternalPoseProvider: FusedFrame â†’ ExternalPose tÃ¼retilir. âœ…
//  - PreferExternal/FreshMs destekleri. âœ…
//  - Capability geldiÄŸinde ApplyCapability otomatik Ã§aÄŸrÄ±lÄ±r. âœ…
//  - Capability loglarÄ± sade veya detaylÄ± basÄ±labilir. âœ…
//  - Python FusedState akÄ±ÅŸÄ±ndan seyrek [PY] Ã¶zet satÄ±rÄ± Ã¼retilir. âœ…
//  - Twin publisher iÃ§in Server property eklendi. âœ…
//
public sealed class TcpJsonFrameSource : IFrameSource, IExternalPoseProvider, IAsyncDisposable
{
    // ----------------------------------------------------------------
    // LOG AYARI
    // ----------------------------------------------------------------
    // true  -> Her sensÃ¶r iÃ§in detaylÄ± [CAP] satÄ±rlarÄ±
    // false -> Tek satÄ±r Ã¶zet [CAP]
    private const bool VerboseCapabilityLogging = false;

    // Ã–zet [PY] satÄ±rlarÄ± arasÄ±nda minimum sÃ¼re
    private static readonly TimeSpan SummaryLogInterval = TimeSpan.FromSeconds(1);
    private DateTime _lastSummaryLogUtc = DateTime.MinValue;

    // ----------------------------------------------------------------
    // YAPILANDIRMA
    // ----------------------------------------------------------------
    private readonly string _host;
    private readonly int _port;

    /// <summary>
    /// Bir Ã§erÃ§evenin taze kabul edileceÄŸi sÃ¼re penceresi.
    /// </summary>
    public TimeSpan FreshnessWindow { get; }

    /// <summary>
    /// IExternalPoseProvider iÃ§in freshness deÄŸeri (ms).
    /// </summary>
    public int FreshMs => (int)FreshnessWindow.TotalMilliseconds;

    /// <summary>
    /// DÄ±ÅŸ durumu tercih et bayraÄŸÄ±. Capability geldikÃ§e gÃ¼ncellenebilir.
    /// </summary>
    public bool PreferExternal { get; private set; } = true;

    // ----------------------------------------------------------------
    // Ä°Ã‡ BÄ°LEÅENLER
    // ----------------------------------------------------------------
    private readonly TcpJsonServer _server;

    /// <summary>
    /// Twin publisher gibi ek modÃ¼llerin aynÄ± TCP sunucusu Ã¼zerinden yayÄ±n yapabilmesi iÃ§in
    /// alttaki TcpJsonServer Ã¶rneÄŸini dÄ±ÅŸarÄ± aÃ§ar.
    /// </summary>
    public TcpJsonServer Server => _server;

    // Son Ã§erÃ§eve / son dÄ±ÅŸ poz (thread-safe)
    private readonly object _gate = new();
    private FusedFrame? _lastFrame = null;
    private ExternalPose? _lastExternalPose = null;

    /// <summary>
    /// Bu kaynak Ã¼zerinden en az bir kere frame alÄ±ndÄ± mÄ±?
    /// </summary>
    public bool HasEverReceivedFrame { get; private set; } = false;

    /// <summary>
    /// Åu anda FreshnessWindow iÃ§inde taze bir frame var mÄ±?
    /// </summary>
    public bool HasFreshFrame
    {
        get
        {
            FusedFrame? _;
            return TryGetLatestFrame(out _);
        }
    }

    // YaÅŸam dÃ¶ngÃ¼sÃ¼ kontrolÃ¼
    private CancellationTokenSource? _linkedCts;
    private Task? _serverTask;

    // Capability logunun bir kez basÄ±lmasÄ± iÃ§in bayrak
    private bool _capabilityLogged = false;

    public TcpJsonFrameSource(string host, int port, TimeSpan? freshnessWindow = null)
    {
        _host = host;
        _port = port;
        FreshnessWindow = freshnessWindow ?? TimeSpan.FromMilliseconds(300);

        // Ham frame loglarÄ±nÄ± kapalÄ± tutuyoruz; parse edilenleri callback ile alÄ±yoruz.
        _server = new TcpJsonServer(
            _host,
            _port,
            onFrame: OnFrameParsed,
            onCapability: OnCapabilityReceived,
            logRawFrames: false
        );
    }

    /// <summary>
    /// Son frame'in tazelik yaÅŸÄ± (ms). Veri yoksa null dÃ¶ner.
    /// </summary>
    public double? LatestAgeMs
    {
        get
        {
            DateTime ts;
            lock (_gate)
            {
                ts = _lastFrame?.TimestampUtc ?? DateTime.MinValue;
            }

            if (ts == DateTime.MinValue)
                return null;

            return (DateTime.UtcNow - ts).TotalMilliseconds;
        }
    }

    /// <summary>
    /// TcpJsonServer yeni bir frame parse ettiÄŸinde Ã§aÄŸrÄ±lÄ±r.
    /// Son frame'i gÃ¼nceller, ExternalPose tÃ¼retir ve seyrek [PY] logu basar.
    /// </summary>
    private void OnFrameParsed(FusedFrame frame)
    {
        ExternalPose? poseSnapshot = null;
        DateTime nowUtc = DateTime.UtcNow;

        // Frame timestamp'i default ise fallback olarak now kullan
        var frameTs = frame.TimestampUtc == default ? nowUtc : frame.TimestampUtc;

        lock (_gate)
        {
            _lastFrame = frame;
            HasEverReceivedFrame = true;

            // XY + heading bilgisinden external pose tÃ¼ret
            var p = frame.Position;
            var pose = new ExternalPose(
                X: p.X,
                Y: p.Y,
                Z: 0.0,
                HeadingDeg: frame.HeadingDeg,
                YawRate: 0.0,
                TimestampUtc: frameTs
            );

            _lastExternalPose = pose;
            poseSnapshot = pose;
        }

        // Seyrek Python akÄ±ÅŸ Ã¶zeti
        if (poseSnapshot is not null && nowUtc - _lastSummaryLogUtc >= SummaryLogInterval)
        {
            _lastSummaryLogUtc = nowUtc;

            var ageMs = (nowUtc - poseSnapshot.Value.TimestampUtc).TotalMilliseconds;
            if (ageMs < 0) ageMs = 0;

            Console.WriteLine(
                $"[PY] pos=({poseSnapshot.Value.X:0.00},{poseSnapshot.Value.Y:0.00}) " +
                $"head={poseSnapshot.Value.HeadingDeg:0.0}Â° age={ageMs:0}ms src=py-data"
            );
        }
    }

    /// <summary>
    /// TcpJsonServer'dan gelen Capability bilgisini ApplyCapability'ye aktarÄ±r.
    /// </summary>
    private void OnCapabilityReceived(bool preferExternal, IReadOnlyList<TcpJsonServer.CapabilitySensorInfo> sensors)
    {
        var list = new List<CapSensor>(sensors?.Count ?? 0);

        if (sensors is not null)
        {
            foreach (var s in sensors)
            {
                // Backend / SimSource zorunlu deÄŸilse reflection ile almaya Ã§alÄ±ÅŸ
                string? backend = null;
                string? simSource = null;

                try
                {
                    var t = s.GetType();
                    backend = t.GetProperty("Backend")?.GetValue(s) as string;
                    simSource = t.GetProperty("SimSource")?.GetValue(s) as string;
                }
                catch
                {
                    // Capability logu hata nedeniyle bozulmasÄ±n
                }

                list.Add(new CapSensor(
                    Name: s.Sensor,
                    Type: s.Source,
                    Backend: backend,
                    FrameId: s.FrameId,
                    RateHz: s.RateHz,
                    Simulate: s.Simulate,
                    SimSource: simSource
                ));
            }
        }

        ApplyCapability(preferExternal, list);
    }

    /// <summary>
    /// Sunucuyu baÅŸlatÄ±r ve frame toplamaya baÅŸlar.
    /// Ä°dempotent Ã§alÄ±ÅŸÄ±r; zaten aÃ§Ä±ksa aynÄ± Task dÃ¶ner.
    /// </summary>
    public Task StartAsync(CancellationToken externalToken = default)
    {
        if (_serverTask is { IsCompleted: false })
            return _serverTask;

        _linkedCts?.Dispose();
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        _serverTask = _server.StartAsync(_linkedCts.Token);
        return _serverTask;
    }

    /// <summary>
    /// IFrameSource: Taze frame varsa dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool TryGetLatestFrame(out FusedFrame? frame)
    {
        frame = null;

        FusedFrame? snap;
        lock (_gate)
            snap = _lastFrame;

        if (snap is null)
            return false;

        var ts = snap.TimestampUtc == default ? DateTime.UtcNow : snap.TimestampUtc;
        var isFresh = (DateTime.UtcNow - ts) <= FreshnessWindow;

        if (!isFresh)
            return false;

        frame = snap;
        return true;
    }

    /// <summary>
    /// IExternalPoseProvider: Son dÄ±ÅŸ poz varsa dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool TryGetLatestExternal(out ExternalPose pose)
    {
        ExternalPose? p;
        lock (_gate)
            p = _lastExternalPose;

        if (p is ExternalPose v)
        {
            var ts = v.TimestampUtc == default ? DateTime.UtcNow : v.TimestampUtc;
            if ((DateTime.UtcNow - ts) <= FreshnessWindow)
            {
                pose = v;
                return true;
            }
        }

        pose = default;
        return false;
    }

    /// <summary>
    /// KaynaÄŸÄ± durdurur ve arka plan task'Ä±nÄ± bekler.
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            _linkedCts?.Cancel();

            var t = _serverTask;
            if (t is not null)
            {
                try
                {
                    await t.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // normal kapanÄ±ÅŸ
                }
                catch (ObjectDisposedException)
                {
                    // erken dispose durumlarÄ± olabilir
                }
            }
        }
        finally
        {
            _serverTask = null;
            _linkedCts?.Dispose();
            _linkedCts = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    // ----------------------------------------------------------------
    // Capability verisini taÅŸÄ±yan hafif tip
    // ----------------------------------------------------------------
    public readonly record struct CapSensor(
        string? Name,
        string? Type,
        string? Backend,
        string? FrameId,
        double? RateHz,
        bool? Simulate,
        string? SimSource
    );

    /// <summary>
    /// Capability mesajÄ±nÄ± uygular: PreferExternal'Ä± gÃ¼nceller ve capability logunu bir kez basar.
    /// </summary>
    public void ApplyCapability(bool preferExternalState, IReadOnlyList<CapSensor>? sensors)
    {
        PreferExternal = preferExternalState;

        if (_capabilityLogged)
            return;

        _capabilityLogged = true;

        try
        {
            int count = sensors?.Count ?? 0;

            if (!VerboseCapabilityLogging)
            {
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

                Console.WriteLine(
                    $"[CAP] prefer_external_state={PreferExternal} sensors={count} " +
                    $"sim={simCount} real={realCount}"
                );
                return;
            }

            if (sensors is null || sensors.Count == 0)
            {
                Console.WriteLine($"[CAP] prefer_external_state={PreferExternal} (sensors: none)");
                return;
            }

            Console.WriteLine($"[CAP] prefer_external_state={PreferExternal} sensors={sensors.Count}");

            foreach (var s in sensors)
            {
                var name = s.Name ?? "?";
                var type = s.Type ?? "?";
                var backend = s.Backend ?? "?";
                var fid = s.FrameId ?? "?";
                var rate = s.RateHz.HasValue ? $"{s.RateHz:0.#}Hz" : "-";
                var simMode = s.Simulate.HasValue ? (s.Simulate.Value ? "sim" : "real") : "?";
                var simSource = s.SimSource ?? "?";

                Console.WriteLine(
                    $"[CAP]  - {name} type={type} backend={backend} frame={fid} " +
                    $"rate={rate} mode={simMode} sim_source={simSource}"
                );
            }
        }
        catch
        {
            // log sÃ¼reci hatadan etkilenmesin
        }
    }
}
