namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom node'unun yer istasyonuna veya baÅŸka bir node'a
/// "ben hayattayÄ±m ve ÅŸu durumdayÄ±m" demesi iÃ§in kullanÄ±lan heartbeat mesajÄ±dÄ±r.
/// 
/// Fleet & Ground Station mimarisinde heartbeat Ã§ok kritiktir.
/// Ã‡Ã¼nkÃ¼ yer istasyonu araÃ§larÄ±n:
/// - HÃ¢lÃ¢ online olup olmadÄ±ÄŸÄ±nÄ±,
/// - En son ne zaman gÃ¶rÃ¼ldÃ¼ÄŸÃ¼nÃ¼,
/// - Hangi durumda olduÄŸunu,
/// - Hangi transport kanallarÄ±nÄ± kullanabildiÄŸini,
/// - Basit health/batarya/gÃ¶rev bilgisini
/// bu mesajlarla gÃ¼ncel tutar.
/// 
/// Bu model genellikle HydronomEnvelope.Payload iÃ§inde taÅŸÄ±nÄ±r.
/// MessageType Ã¶rneÄŸi:
/// "FleetHeartbeat"
/// </summary>
public sealed record FleetHeartbeat
{
    /// <summary>
    /// Heartbeat gÃ¶nderen node'un kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - VEHICLE-ALPHA-001
    /// - GROUND-001
    /// - OPS-GATEWAY-001
    /// 
    /// FleetRegistry bu kimlik Ã¼zerinden node durumunu gÃ¼nceller.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new();

    /// <summary>
    /// Heartbeat mesajÄ±nÄ±n Ã¼retildiÄŸi UTC zaman damgasÄ±.
    /// 
    /// Bu alan, mesajÄ±n ne kadar taze olduÄŸunu anlamak iÃ§in kullanÄ±lÄ±r.
    /// Yer istasyonu bu zamanla kendi aldÄ±ÄŸÄ± zamanÄ± karÅŸÄ±laÅŸtÄ±rarak:
    /// - Gecikme,
    /// - Saat farkÄ±,
    /// - BaÄŸlantÄ± tazeliÄŸi,
    /// - Replay/stale mesaj
    /// kontrolÃ¼ yapabilir.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Node'un genel Ã§alÄ±ÅŸma modu.
    /// 
    /// Ã–rnekler:
    /// - "Idle"
    /// - "Autonomous"
    /// - "Manual"
    /// - "Mission"
    /// - "ReturnHome"
    /// - "SafeStop"
    /// - "EmergencyStop"
    /// - "Simulation"
    /// 
    /// Bu bilgi Hydronom Ops Ã¼zerinde araÃ§ kartÄ±nda hÄ±zlÄ±ca gÃ¶sterilebilir.
    /// </summary>
    public string Mode { get; init; } = "Unknown";

    /// <summary>
    /// Node'un genel saÄŸlÄ±k durumu.
    /// 
    /// Ã–rnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Critical"
    /// - "Fault"
    /// - "Unknown"
    /// 
    /// DetaylÄ± health analizi ayrÄ± mesajlarla taÅŸÄ±nabilir.
    /// Heartbeat iÃ§inde bu alan sadece hÄ±zlÄ± Ã¶zet iÃ§indir.
    /// </summary>
    public string Health { get; init; } = "Unknown";

    /// <summary>
    /// Batarya yÃ¼zdesi.
    /// 
    /// DeÄŸer normalde 0-100 arasÄ±dÄ±r.
    /// null ise:
    /// - Batarya bilgisi yoktur.
    /// - Node araÃ§ deÄŸildir.
    /// - Power sistemi henÃ¼z rapor Ã¼retmemiÅŸtir.
    /// </summary>
    public double? BatteryPercent { get; init; }

    /// <summary>
    /// Aktif gÃ¶rev kimliÄŸi.
    /// 
    /// BoÅŸ olabilir.
    /// Ã–rnek:
    /// - "MISSION-2026-001"
    /// - "SURVEY-AREA-A"
    /// - "RETURN-HOME"
    /// </summary>
    public string ActiveMissionId { get; init; } = string.Empty;

    /// <summary>
    /// Aktif gÃ¶revin kÄ±sa durum Ã¶zeti.
    /// 
    /// Ã–rnekler:
    /// - "Idle"
    /// - "Running"
    /// - "Paused"
    /// - "Completed"
    /// - "Failed"
    /// - "ReturningHome"
    /// </summary>
    public string MissionState { get; init; } = "Idle";

    /// <summary>
    /// Son bilinen enlem.
    /// 
    /// GPS veya global konum yoksa null kalabilir.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Son bilinen boylam.
    /// 
    /// GPS veya global konum yoksa null kalabilir.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Son bilinen heading deÄŸeri.
    /// 
    /// Derece cinsindendir.
    /// </summary>
    public double? HeadingDeg { get; init; }

    /// <summary>
    /// Son bilinen hÄ±z.
    /// 
    /// Metre/saniye cinsindendir.
    /// </summary>
    public double? SpeedMps { get; init; }

    /// <summary>
    /// Heartbeat anÄ±nda node'un kullanÄ±labilir gÃ¶rdÃ¼ÄŸÃ¼ transport kanallarÄ±.
    /// 
    /// Ã–rnek:
    /// - Tcp
    /// - WebSocket
    /// - LoRa
    /// - RfModem
    /// 
    /// Yer istasyonu bu bilgiyle hangi araca hangi kanaldan ulaÅŸabileceÄŸini anlayabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> AvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Node'un Ã¶zet kabiliyet listesi.
    /// 
    /// Bu heartbeat iÃ§inde gÃ¶nderilebilir; fakat Ã§ok bÃ¼yÃ¼k capability listeleri iÃ§in
    /// ileride ayrÄ± CapabilityAnnouncement mesajÄ± da kullanÄ±labilir.
    /// 
    /// Ä°lk sÃ¼rÃ¼mde heartbeat ile beraber gÃ¶ndermek pratik olacaktÄ±r.
    /// </summary>
    public IReadOnlyList<VehicleCapability> Capabilities { get; init; } =
        Array.Empty<VehicleCapability>();

    /// <summary>
    /// Ek heartbeat metadata alanÄ±.
    /// 
    /// Ã–rnek:
    /// - "frameAgeMs": "24"
    /// - "cpuLoad": "32"
    /// - "runtimeHz": "19"
    /// - "linkQuality": "Good"
    /// - "source": "runtime"
    /// 
    /// Bu alan ilk fazda esneklik saÄŸlar.
    /// Daha sonra sabit ihtiyaÃ§lar netleÅŸirse ayrÄ± gÃ¼Ã§lÃ¼ tiplere taÅŸÄ±nabilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Heartbeat bilgisinden VehicleNodeStatus Ã¼retir.
    /// 
    /// FleetRegistry heartbeat aldÄ±ÄŸÄ±nda node durumunu gÃ¼ncellemek iÃ§in
    /// bu yardÄ±mcÄ± metodu kullanabilir.
    /// </summary>
    public VehicleNodeStatus ToStatus()
    {
        return new VehicleNodeStatus
        {
            Identity = Identity,
            IsOnline = true,
            LastSeenUtc = TimestampUtc,
            BatteryPercent = BatteryPercent,
            Health = Health,
            ActiveMissionId = ActiveMissionId,
            MissionState = MissionState,
            Latitude = Latitude,
            Longitude = Longitude,
            HeadingDeg = HeadingDeg,
            SpeedMps = SpeedMps,
            AvailableTransports = AvailableTransports,
            Capabilities = Capabilities,
            Metadata = Metadata
        };
    }

    /// <summary>
    /// Heartbeat mesajÄ±nÄ±n temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// GeÃ§erli bir heartbeat iÃ§in en azÄ±ndan node kimliÄŸi geÃ§erli olmalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        Identity.IsValid;
}
