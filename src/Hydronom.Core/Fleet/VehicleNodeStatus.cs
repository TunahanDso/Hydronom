namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// FleetRegistry içinde takip edilecek araç/node durum özetini temsil eder.
/// 
/// Bu model, yer istasyonunun bir aracı hızlıca anlayabilmesi için tasarlanmıştır.
/// Hydronom Ops tarafındaki araç kartları, filo listesi ve canlı durum panelleri
/// bu modelden beslenebilir.
/// 
/// Amaç:
/// - Araç bağlı mı?
/// - En son ne zaman görüldü?
/// - Batarya durumu ne?
/// - Sağlık durumu ne?
/// - Aktif görevi var mı?
/// - Hangi role sahip?
/// - Hangi haberleşme kanalları kullanılabilir?
/// - Hangi kabiliyetlere sahip?
/// 
/// Bu sınıf full telemetry değildir.
/// Sadece Fleet seviyesinde hızlı durum özeti verir.
/// </summary>
public sealed record VehicleNodeStatus
{
    /// <summary>
    /// Araç veya node kimliği.
    /// 
    /// Bu alan:
    /// - NodeId
    /// - DisplayName
    /// - NodeType
    /// - VehicleType
    /// - Role
    /// gibi temel kimlik bilgilerini taşır.
    /// 
    /// FleetRegistry araçları bu kimlik üzerinden takip eder.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new();

    /// <summary>
    /// Node'un yer istasyonu tarafından bağlı kabul edilip edilmediğini belirtir.
    /// 
    /// true:
    /// - Son heartbeat/status mesajı taze.
    /// - En az bir transport üzerinden erişilebilir.
    /// 
    /// false:
    /// - Araç uzun süredir mesaj göndermemiş olabilir.
    /// - Bağlantı kopmuş olabilir.
    /// - Araç offline olabilir.
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Bu node'dan alınan son mesajın UTC zamanı.
    /// 
    /// Kullanım alanları:
    /// - Bağlantı tazeliği hesaplama
    /// - Offline araç tespiti
    /// - Fleet dashboard üzerinde "son görüldü" bilgisi
    /// - Watchdog / bağlantı kaybı uyarıları
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Araç batarya yüzdesi.
    /// 
    /// Değer aralığı normalde 0-100 olmalıdır.
    /// 
    /// null ise:
    /// - Araç batarya bilgisi göndermiyor olabilir.
    /// - Bu node araç dışı bir sistem olabilir.
    /// - Veri henüz alınmamış olabilir.
    /// </summary>
    public double? BatteryPercent { get; init; }

    /// <summary>
    /// Genel sağlık durumu.
    /// 
    /// Örnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Critical"
    /// - "Fault"
    /// - "Unknown"
    /// 
    /// Şimdilik string bırakıyoruz.
    /// Çünkü ileride health sistemi daha detaylı power/sensor/actuator analizleriyle
    /// genişletilecek.
    /// </summary>
    public string Health { get; init; } = "Unknown";

    /// <summary>
    /// Araçta aktif olan görev kimliği.
    /// 
    /// Örnek:
    /// - "MISSION-2026-001"
    /// - "SEARCH-AREA-A"
    /// - "RETURN-HOME"
    /// 
    /// Boş ise araçta aktif görev olmayabilir.
    /// </summary>
    public string ActiveMissionId { get; init; } = string.Empty;

    /// <summary>
    /// Araçta aktif olan görev durum bilgisi.
    /// 
    /// Örnekler:
    /// - "Idle"
    /// - "Running"
    /// - "Paused"
    /// - "Completed"
    /// - "Failed"
    /// - "ReturningHome"
    /// 
    /// Bu bilgi Ops tarafındaki görev kartlarında gösterilebilir.
    /// </summary>
    public string MissionState { get; init; } = "Idle";

    /// <summary>
    /// Aracın son bilinen enlem değeri.
    /// 
    /// null ise:
    /// - GPS yoktur.
    /// - Konum henüz alınmamıştır.
    /// - Araç simülasyon/kapalı ortamda çalışıyor olabilir.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Aracın son bilinen boylam değeri.
    /// 
    /// null ise:
    /// - GPS yoktur.
    /// - Konum henüz alınmamıştır.
    /// - Araç simülasyon/kapalı ortamda çalışıyor olabilir.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Aracın son bilinen baş açısı.
    /// 
    /// Derece cinsindendir.
    /// 0-360 veya -180/+180 formatı kullanılabilir; bu formatı ileride standardize edebiliriz.
    /// </summary>
    public double? HeadingDeg { get; init; }

    /// <summary>
    /// Aracın son bilinen hızı.
    /// 
    /// Metre/saniye cinsindendir.
    /// </summary>
    public double? SpeedMps { get; init; }

    /// <summary>
    /// Node'un kullanılabilir haberleşme kanalları.
    /// 
    /// Örnek:
    /// - Tcp
    /// - WebSocket
    /// - LoRa
    /// - RfModem
    /// - Cellular
    /// 
    /// CommunicationRouter bu listeyi mesaj yönlendirme kararlarında kullanabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> AvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Node'un bildirdiği kabiliyetler.
    /// 
    /// Örnek:
    /// - navigation
    /// - lidar
    /// - camera
    /// - mapping
    /// - relay
    /// - autonomous_mission
    /// 
    /// MissionAllocator ileride görevleri bu kabiliyetlere göre dağıtabilir.
    /// </summary>
    public IReadOnlyList<VehicleCapability> Capabilities { get; init; } =
        Array.Empty<VehicleCapability>();

    /// <summary>
    /// Fleet tarafında bu node için ek bilgi alanı.
    /// 
    /// Örnek:
    /// - "linkQuality": "Good"
    /// - "operator": "Tunahan"
    /// - "area": "TestPool"
    /// - "mode": "Autonomous"
    /// 
    /// Bu alan, ilk fazda esneklik sağlar.
    /// Daha sonra gerekli alanlar netleşirse güçlü tiplere ayrılabilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Bu status bilgisinin temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// Kimliği geçerli olmayan bir node FleetRegistry içine alınmamalıdır.
    /// </summary>
    public bool IsValid =>
        Identity.IsValid;
}