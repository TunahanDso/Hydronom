namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// FleetRegistry iÃ§inde takip edilecek araÃ§/node durum Ã¶zetini temsil eder.
/// 
/// Bu model, yer istasyonunun bir aracÄ± hÄ±zlÄ±ca anlayabilmesi iÃ§in tasarlanmÄ±ÅŸtÄ±r.
/// Hydronom Ops tarafÄ±ndaki araÃ§ kartlarÄ±, filo listesi ve canlÄ± durum panelleri
/// bu modelden beslenebilir.
/// 
/// AmaÃ§:
/// - AraÃ§ baÄŸlÄ± mÄ±?
/// - En son ne zaman gÃ¶rÃ¼ldÃ¼?
/// - Batarya durumu ne?
/// - SaÄŸlÄ±k durumu ne?
/// - Aktif gÃ¶revi var mÄ±?
/// - Hangi role sahip?
/// - Hangi haberleÅŸme kanallarÄ± kullanÄ±labilir?
/// - Hangi kabiliyetlere sahip?
/// 
/// Bu sÄ±nÄ±f full telemetry deÄŸildir.
/// Sadece Fleet seviyesinde hÄ±zlÄ± durum Ã¶zeti verir.
/// </summary>
public sealed record VehicleNodeStatus
{
    /// <summary>
    /// AraÃ§ veya node kimliÄŸi.
    /// 
    /// Bu alan:
    /// - NodeId
    /// - DisplayName
    /// - NodeType
    /// - VehicleType
    /// - Role
    /// gibi temel kimlik bilgilerini taÅŸÄ±r.
    /// 
    /// FleetRegistry araÃ§larÄ± bu kimlik Ã¼zerinden takip eder.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new();

    /// <summary>
    /// Node'un yer istasyonu tarafÄ±ndan baÄŸlÄ± kabul edilip edilmediÄŸini belirtir.
    /// 
    /// true:
    /// - Son heartbeat/status mesajÄ± taze.
    /// - En az bir transport Ã¼zerinden eriÅŸilebilir.
    /// 
    /// false:
    /// - AraÃ§ uzun sÃ¼redir mesaj gÃ¶ndermemiÅŸ olabilir.
    /// - BaÄŸlantÄ± kopmuÅŸ olabilir.
    /// - AraÃ§ offline olabilir.
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Bu node'dan alÄ±nan son mesajÄ±n UTC zamanÄ±.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - BaÄŸlantÄ± tazeliÄŸi hesaplama
    /// - Offline araÃ§ tespiti
    /// - Fleet dashboard Ã¼zerinde "son gÃ¶rÃ¼ldÃ¼" bilgisi
    /// - Watchdog / baÄŸlantÄ± kaybÄ± uyarÄ±larÄ±
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// AraÃ§ batarya yÃ¼zdesi.
    /// 
    /// DeÄŸer aralÄ±ÄŸÄ± normalde 0-100 olmalÄ±dÄ±r.
    /// 
    /// null ise:
    /// - AraÃ§ batarya bilgisi gÃ¶ndermiyor olabilir.
    /// - Bu node araÃ§ dÄ±ÅŸÄ± bir sistem olabilir.
    /// - Veri henÃ¼z alÄ±nmamÄ±ÅŸ olabilir.
    /// </summary>
    public double? BatteryPercent { get; init; }

    /// <summary>
    /// Genel saÄŸlÄ±k durumu.
    /// 
    /// Ã–rnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Critical"
    /// - "Fault"
    /// - "Unknown"
    /// 
    /// Åimdilik string bÄ±rakÄ±yoruz.
    /// Ã‡Ã¼nkÃ¼ ileride health sistemi daha detaylÄ± power/sensor/actuator analizleriyle
    /// geniÅŸletilecek.
    /// </summary>
    public string Health { get; init; } = "Unknown";

    /// <summary>
    /// AraÃ§ta aktif olan gÃ¶rev kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - "MISSION-2026-001"
    /// - "SEARCH-AREA-A"
    /// - "RETURN-HOME"
    /// 
    /// BoÅŸ ise araÃ§ta aktif gÃ¶rev olmayabilir.
    /// </summary>
    public string ActiveMissionId { get; init; } = string.Empty;

    /// <summary>
    /// AraÃ§ta aktif olan gÃ¶rev durum bilgisi.
    /// 
    /// Ã–rnekler:
    /// - "Idle"
    /// - "Running"
    /// - "Paused"
    /// - "Completed"
    /// - "Failed"
    /// - "ReturningHome"
    /// 
    /// Bu bilgi Ops tarafÄ±ndaki gÃ¶rev kartlarÄ±nda gÃ¶sterilebilir.
    /// </summary>
    public string MissionState { get; init; } = "Idle";

    /// <summary>
    /// AracÄ±n son bilinen enlem deÄŸeri.
    /// 
    /// null ise:
    /// - GPS yoktur.
    /// - Konum henÃ¼z alÄ±nmamÄ±ÅŸtÄ±r.
    /// - AraÃ§ simÃ¼lasyon/kapalÄ± ortamda Ã§alÄ±ÅŸÄ±yor olabilir.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// AracÄ±n son bilinen boylam deÄŸeri.
    /// 
    /// null ise:
    /// - GPS yoktur.
    /// - Konum henÃ¼z alÄ±nmamÄ±ÅŸtÄ±r.
    /// - AraÃ§ simÃ¼lasyon/kapalÄ± ortamda Ã§alÄ±ÅŸÄ±yor olabilir.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// AracÄ±n son bilinen baÅŸ aÃ§Ä±sÄ±.
    /// 
    /// Derece cinsindendir.
    /// 0-360 veya -180/+180 formatÄ± kullanÄ±labilir; bu formatÄ± ileride standardize edebiliriz.
    /// </summary>
    public double? HeadingDeg { get; init; }

    /// <summary>
    /// AracÄ±n son bilinen hÄ±zÄ±.
    /// 
    /// Metre/saniye cinsindendir.
    /// </summary>
    public double? SpeedMps { get; init; }

    /// <summary>
    /// Node'un kullanÄ±labilir haberleÅŸme kanallarÄ±.
    /// 
    /// Ã–rnek:
    /// - Tcp
    /// - WebSocket
    /// - LoRa
    /// - RfModem
    /// - Cellular
    /// 
    /// CommunicationRouter bu listeyi mesaj yÃ¶nlendirme kararlarÄ±nda kullanabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> AvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Node'un bildirdiÄŸi kabiliyetler.
    /// 
    /// Ã–rnek:
    /// - navigation
    /// - lidar
    /// - camera
    /// - mapping
    /// - relay
    /// - autonomous_mission
    /// 
    /// MissionAllocator ileride gÃ¶revleri bu kabiliyetlere gÃ¶re daÄŸÄ±tabilir.
    /// </summary>
    public IReadOnlyList<VehicleCapability> Capabilities { get; init; } =
        Array.Empty<VehicleCapability>();

    /// <summary>
    /// Fleet tarafÄ±nda bu node iÃ§in ek bilgi alanÄ±.
    /// 
    /// Ã–rnek:
    /// - "linkQuality": "Good"
    /// - "operator": "Tunahan"
    /// - "area": "TestPool"
    /// - "mode": "Autonomous"
    /// 
    /// Bu alan, ilk fazda esneklik saÄŸlar.
    /// Daha sonra gerekli alanlar netleÅŸirse gÃ¼Ã§lÃ¼ tiplere ayrÄ±labilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Bu status bilgisinin temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// KimliÄŸi geÃ§erli olmayan bir node FleetRegistry iÃ§ine alÄ±nmamalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        Identity.IsValid;
}
