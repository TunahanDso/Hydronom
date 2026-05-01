namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom node'unun yer istasyonuna veya başka bir node'a
/// "ben hayattayım ve şu durumdayım" demesi için kullanılan heartbeat mesajıdır.
/// 
/// Fleet & Ground Station mimarisinde heartbeat çok kritiktir.
/// Çünkü yer istasyonu araçların:
/// - Hâlâ online olup olmadığını,
/// - En son ne zaman görüldüğünü,
/// - Hangi durumda olduğunu,
/// - Hangi transport kanallarını kullanabildiğini,
/// - Basit health/batarya/görev bilgisini
/// bu mesajlarla güncel tutar.
/// 
/// Bu model genellikle HydronomEnvelope.Payload içinde taşınır.
/// MessageType örneği:
/// "FleetHeartbeat"
/// </summary>
public sealed record FleetHeartbeat
{
    /// <summary>
    /// Heartbeat gönderen node'un kimliği.
    /// 
    /// Örnek:
    /// - VEHICLE-ALPHA-001
    /// - GROUND-001
    /// - OPS-GATEWAY-001
    /// 
    /// FleetRegistry bu kimlik üzerinden node durumunu günceller.
    /// </summary>
    public NodeIdentity Identity { get; init; } = new();

    /// <summary>
    /// Heartbeat mesajının üretildiği UTC zaman damgası.
    /// 
    /// Bu alan, mesajın ne kadar taze olduğunu anlamak için kullanılır.
    /// Yer istasyonu bu zamanla kendi aldığı zamanı karşılaştırarak:
    /// - Gecikme,
    /// - Saat farkı,
    /// - Bağlantı tazeliği,
    /// - Replay/stale mesaj
    /// kontrolü yapabilir.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Node'un genel çalışma modu.
    /// 
    /// Örnekler:
    /// - "Idle"
    /// - "Autonomous"
    /// - "Manual"
    /// - "Mission"
    /// - "ReturnHome"
    /// - "SafeStop"
    /// - "EmergencyStop"
    /// - "Simulation"
    /// 
    /// Bu bilgi Hydronom Ops üzerinde araç kartında hızlıca gösterilebilir.
    /// </summary>
    public string Mode { get; init; } = "Unknown";

    /// <summary>
    /// Node'un genel sağlık durumu.
    /// 
    /// Örnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Critical"
    /// - "Fault"
    /// - "Unknown"
    /// 
    /// Detaylı health analizi ayrı mesajlarla taşınabilir.
    /// Heartbeat içinde bu alan sadece hızlı özet içindir.
    /// </summary>
    public string Health { get; init; } = "Unknown";

    /// <summary>
    /// Batarya yüzdesi.
    /// 
    /// Değer normalde 0-100 arasıdır.
    /// null ise:
    /// - Batarya bilgisi yoktur.
    /// - Node araç değildir.
    /// - Power sistemi henüz rapor üretmemiştir.
    /// </summary>
    public double? BatteryPercent { get; init; }

    /// <summary>
    /// Aktif görev kimliği.
    /// 
    /// Boş olabilir.
    /// Örnek:
    /// - "MISSION-2026-001"
    /// - "SURVEY-AREA-A"
    /// - "RETURN-HOME"
    /// </summary>
    public string ActiveMissionId { get; init; } = string.Empty;

    /// <summary>
    /// Aktif görevin kısa durum özeti.
    /// 
    /// Örnekler:
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
    /// Son bilinen heading değeri.
    /// 
    /// Derece cinsindendir.
    /// </summary>
    public double? HeadingDeg { get; init; }

    /// <summary>
    /// Son bilinen hız.
    /// 
    /// Metre/saniye cinsindendir.
    /// </summary>
    public double? SpeedMps { get; init; }

    /// <summary>
    /// Heartbeat anında node'un kullanılabilir gördüğü transport kanalları.
    /// 
    /// Örnek:
    /// - Tcp
    /// - WebSocket
    /// - LoRa
    /// - RfModem
    /// 
    /// Yer istasyonu bu bilgiyle hangi araca hangi kanaldan ulaşabileceğini anlayabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> AvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Node'un özet kabiliyet listesi.
    /// 
    /// Bu heartbeat içinde gönderilebilir; fakat çok büyük capability listeleri için
    /// ileride ayrı CapabilityAnnouncement mesajı da kullanılabilir.
    /// 
    /// İlk sürümde heartbeat ile beraber göndermek pratik olacaktır.
    /// </summary>
    public IReadOnlyList<VehicleCapability> Capabilities { get; init; } =
        Array.Empty<VehicleCapability>();

    /// <summary>
    /// Ek heartbeat metadata alanı.
    /// 
    /// Örnek:
    /// - "frameAgeMs": "24"
    /// - "cpuLoad": "32"
    /// - "runtimeHz": "19"
    /// - "linkQuality": "Good"
    /// - "source": "runtime"
    /// 
    /// Bu alan ilk fazda esneklik sağlar.
    /// Daha sonra sabit ihtiyaçlar netleşirse ayrı güçlü tiplere taşınabilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Heartbeat bilgisinden VehicleNodeStatus üretir.
    /// 
    /// FleetRegistry heartbeat aldığında node durumunu güncellemek için
    /// bu yardımcı metodu kullanabilir.
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
    /// Heartbeat mesajının temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// Geçerli bir heartbeat için en azından node kimliği geçerli olmalıdır.
    /// </summary>
    public bool IsValid =>
        Identity.IsValid;
}