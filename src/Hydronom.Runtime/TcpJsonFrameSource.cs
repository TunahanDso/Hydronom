using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Buses; // IExternalPoseProvider, ExternalPose

//
// TCP TABANLI FRAME KAYNAĞI
// ------------------------------------------------------------
// Amaç:
//  - TcpJsonServer üzerinden gelen FusedFrame'leri alıp "son taze çerçeve" olarak saklamak.
//  - IFrameSource arayüzü ile runtime'a kaynak-agnostik tüketim sağlamak.
//  - Gerekirse aynı TCP sunucusu üzerinden twin publisher gibi ek modüllerin de
//    yayın yapabilmesi için server örneğini dışarı açmak.
//
// Değişiklikler:
//  - TcpJsonServer arka planda Task ile çalışır. ✅
//  - IExternalPoseProvider: FusedFrame → ExternalPose türetilir. ✅
//  - PreferExternal/FreshMs destekleri. ✅
//  - Capability geldiğinde ApplyCapability otomatik çağrılır. ✅
//  - Capability logları sade veya detaylı basılabilir. ✅
//  - Python FusedState akışından seyrek [PY] özet satırı üretilir. ✅
//  - Twin publisher için Server property eklendi. ✅
//
public sealed class TcpJsonFrameSource : IFrameSource, IExternalPoseProvider, IAsyncDisposable
{
    // ----------------------------------------------------------------
    // LOG AYARI
    // ----------------------------------------------------------------
    // true  -> Her sensör için detaylı [CAP] satırları
    // false -> Tek satır özet [CAP]
    private const bool VerboseCapabilityLogging = false;

    // Özet [PY] satırları arasında minimum süre
    private static readonly TimeSpan SummaryLogInterval = TimeSpan.FromSeconds(1);
    private DateTime _lastSummaryLogUtc = DateTime.MinValue;

    // ----------------------------------------------------------------
    // YAPILANDIRMA
    // ----------------------------------------------------------------
    private readonly string _host;
    private readonly int _port;

    /// <summary>
    /// Bir çerçevenin taze kabul edileceği süre penceresi.
    /// </summary>
    public TimeSpan FreshnessWindow { get; }

    /// <summary>
    /// IExternalPoseProvider için freshness değeri (ms).
    /// </summary>
    public int FreshMs => (int)FreshnessWindow.TotalMilliseconds;

    /// <summary>
    /// Dış durumu tercih et bayrağı. Capability geldikçe güncellenebilir.
    /// </summary>
    public bool PreferExternal { get; private set; } = true;

    // ----------------------------------------------------------------
    // İÇ BİLEŞENLER
    // ----------------------------------------------------------------
    private readonly TcpJsonServer _server;

    /// <summary>
    /// Twin publisher gibi ek modüllerin aynı TCP sunucusu üzerinden yayın yapabilmesi için
    /// alttaki TcpJsonServer örneğini dışarı açar.
    /// </summary>
    public TcpJsonServer Server => _server;

    // Son çerçeve / son dış poz (thread-safe)
    private readonly object _gate = new();
    private FusedFrame? _lastFrame = null;
    private ExternalPose? _lastExternalPose = null;

    /// <summary>
    /// Bu kaynak üzerinden en az bir kere frame alındı mı?
    /// </summary>
    public bool HasEverReceivedFrame { get; private set; } = false;

    /// <summary>
    /// Şu anda FreshnessWindow içinde taze bir frame var mı?
    /// </summary>
    public bool HasFreshFrame
    {
        get
        {
            FusedFrame? _;
            return TryGetLatestFrame(out _);
        }
    }

    // Yaşam döngüsü kontrolü
    private CancellationTokenSource? _linkedCts;
    private Task? _serverTask;

    // Capability logunun bir kez basılması için bayrak
    private bool _capabilityLogged = false;

    public TcpJsonFrameSource(string host, int port, TimeSpan? freshnessWindow = null)
    {
        _host = host;
        _port = port;
        FreshnessWindow = freshnessWindow ?? TimeSpan.FromMilliseconds(300);

        // Ham frame loglarını kapalı tutuyoruz; parse edilenleri callback ile alıyoruz.
        _server = new TcpJsonServer(
            _host,
            _port,
            onFrame: OnFrameParsed,
            onCapability: OnCapabilityReceived,
            logRawFrames: false
        );
    }

    /// <summary>
    /// Son frame'in tazelik yaşı (ms). Veri yoksa null döner.
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
    /// TcpJsonServer yeni bir frame parse ettiğinde çağrılır.
    /// Son frame'i günceller, ExternalPose türetir ve seyrek [PY] logu basar.
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

            // XY + heading bilgisinden external pose türet
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

        // Seyrek Python akış özeti
        if (poseSnapshot is not null && nowUtc - _lastSummaryLogUtc >= SummaryLogInterval)
        {
            _lastSummaryLogUtc = nowUtc;

            var ageMs = (nowUtc - poseSnapshot.Value.TimestampUtc).TotalMilliseconds;
            if (ageMs < 0) ageMs = 0;

            Console.WriteLine(
                $"[PY] pos=({poseSnapshot.Value.X:0.00},{poseSnapshot.Value.Y:0.00}) " +
                $"head={poseSnapshot.Value.HeadingDeg:0.0}° age={ageMs:0}ms src=py-data"
            );
        }
    }

    /// <summary>
    /// TcpJsonServer'dan gelen Capability bilgisini ApplyCapability'ye aktarır.
    /// </summary>
    private void OnCapabilityReceived(bool preferExternal, IReadOnlyList<TcpJsonServer.CapabilitySensorInfo> sensors)
    {
        var list = new List<CapSensor>(sensors?.Count ?? 0);

        if (sensors is not null)
        {
            foreach (var s in sensors)
            {
                // Backend / SimSource zorunlu değilse reflection ile almaya çalış
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
                    // Capability logu hata nedeniyle bozulmasın
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
    /// Sunucuyu başlatır ve frame toplamaya başlar.
    /// İdempotent çalışır; zaten açıksa aynı Task döner.
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
    /// IFrameSource: Taze frame varsa döndürür.
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
    /// IExternalPoseProvider: Son dış poz varsa döndürür.
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
    /// Kaynağı durdurur ve arka plan task'ını bekler.
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
                    // normal kapanış
                }
                catch (ObjectDisposedException)
                {
                    // erken dispose durumları olabilir
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
    // Capability verisini taşıyan hafif tip
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
    /// Capability mesajını uygular: PreferExternal'ı günceller ve capability logunu bir kez basar.
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
            // log süreci hatadan etkilenmesin
        }
    }
}