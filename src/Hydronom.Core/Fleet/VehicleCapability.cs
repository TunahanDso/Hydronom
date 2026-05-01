namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom aracının veya node'un sahip olduğu kabiliyetleri temsil eder.
/// 
/// Fleet & Ground Station mimarisinde yer istasyonu sadece aracın var olduğunu bilmemeli;
/// o aracın ne yapabildiğini de bilmelidir.
/// 
/// Örneğin:
/// - Bu araç navigation yapabiliyor mu?
/// - LiDAR var mı?
/// - Kamera var mı?
/// - Mapping destekliyor mu?
/// - RF veya LoRa linki var mı?
/// - Manipülatör / özel görev ekipmanı var mı?
/// 
/// FleetCoordinator ve MissionAllocator ileride görev dağıtırken bu kabiliyetleri kullanır.
/// </summary>
public sealed record VehicleCapability
{
    /// <summary>
    /// Kabiliyetin benzersiz adı.
    /// 
    /// Örnekler:
    /// - "navigation"
    /// - "lidar"
    /// - "camera"
    /// - "mapping"
    /// - "obstacle_detection"
    /// - "target_tracking"
    /// - "autonomous_mission"
    /// - "manual_control"
    /// - "relay"
    /// - "water_quality_sensor"
    /// 
    /// Küçük harfli ve snake_case tutulması önerilir.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Kabiliyetin kısa açıklaması.
    /// 
    /// Bu alan özellikle Hydronom Ops tarafında aracın detay ekranında
    /// operatöre okunabilir bilgi göstermek için kullanılabilir.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Kabiliyetin aktif olup olmadığını belirtir.
    /// 
    /// true:
    /// - Kabiliyet mevcut ve kullanılabilir.
    /// 
    /// false:
    /// - Kabiliyet araçta var ama şu anda devre dışı olabilir.
    /// - Sensör arızalı olabilir.
    /// - Yazılım modülü kapatılmış olabilir.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Kabiliyetin sağlık / kullanılabilirlik durumu.
    /// 
    /// Örnekler:
    /// - "OK"
    /// - "Warning"
    /// - "Fault"
    /// - "Unavailable"
    /// - "Simulated"
    /// 
    /// Şimdilik string bırakıyoruz.
    /// Çünkü health modelini ileride daha geniş bir yapıya bağlayabiliriz.
    /// </summary>
    public string Health { get; init; } = "OK";

    /// <summary>
    /// Bu kabiliyetin simülasyon üzerinden mi sağlandığını belirtir.
    /// 
    /// Örnek:
    /// - Sim LiDAR
    /// - Sim GPS
    /// - Mock actuator
    /// - FileReplay telemetry
    /// 
    /// FleetRegistry ve Ops UI bu bilgiyi kullanarak gerçek/sim ayrımı gösterebilir.
    /// </summary>
    public bool IsSimulated { get; init; }

    /// <summary>
    /// Bu kabiliyetin ilişkili olduğu haberleşme transport türleri.
    /// 
    /// Özellikle haberleşme kabiliyetleri için önemlidir.
    /// 
    /// Örnek:
    /// - LoRa modülü için: TransportKind.LoRa
    /// - RF modem için: TransportKind.RfModem
    /// - Wi-Fi/TCP bağlantı için: TransportKind.Tcp
    /// 
    /// Sensör kabiliyetlerinde boş kalabilir.
    /// </summary>
    public IReadOnlyList<TransportKind> RelatedTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Kabiliyetle ilgili ek metadata bilgileri.
    /// 
    /// Örnekler:
    /// - "rangeMeters": "2000"
    /// - "maxPayloadBytes": "240"
    /// - "bandwidthClass": "Low"
    /// - "latencyClass": "High"
    /// - "sensorModel": "RPLidar A1"
    /// 
    /// Şimdilik string/string dictionary kullanıyoruz.
    /// İleride daha güçlü capability schema'larına geçilebilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Kabiliyet kaydının temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından Name dolu olmalıdır.
    /// Çünkü görev dağıtımı ve filtreleme bu ad üzerinden yapılır.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name);
}