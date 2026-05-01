namespace Hydronom.Core.Fleet;

/// <summary>
/// Hydronom Fleet mimarisinde bir düğümün kimlik bilgisini temsil eder.
/// 
/// Düğüm; yalnızca araç olmak zorunda değildir.
/// Şunlar da birer node olabilir:
/// - Otonom araç
/// - Yer istasyonu
/// - Ops Gateway
/// - Replay sistemi
/// - Simülasyon node'u
/// - Relay görevi yapan ara düğüm
/// 
/// Bu modelin amacı, FleetRegistry ve haberleşme katmanının
/// sistemdeki her bileşeni tekil ve anlaşılır şekilde tanıyabilmesidir.
/// </summary>
public sealed record NodeIdentity
{
    /// <summary>
    /// Node'un benzersiz kimliği.
    /// 
    /// Örnekler:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// - "SIM-VEHICLE-001"
    /// 
    /// Bu alan mesajlaşmada SourceNodeId / TargetNodeId ile eşleşir.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// Node'un insan tarafından okunabilir adı.
    /// 
    /// Örnekler:
    /// - "Alpha"
    /// - "Beta"
    /// - "Main Ground Station"
    /// - "Hydronom Ops Gateway"
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Node'un genel türü.
    /// 
    /// Örnekler:
    /// - "Vehicle"
    /// - "GroundStation"
    /// - "Gateway"
    /// - "Simulator"
    /// - "Relay"
    /// 
    /// Şimdilik string bırakıyoruz.
    /// Çünkü ileride farklı node türleri eklenebilir.
    /// Gerekirse daha sonra enum'a çevrilebilir.
    /// </summary>
    public string NodeType { get; init; } = "Unknown";

    /// <summary>
    /// Araç node'ları için araç tipi.
    /// 
    /// Örnekler:
    /// - "SurfaceVessel"
    /// - "Submarine"
    /// - "SailingVessel"
    /// - "AerialVehicle"
    /// - "GroundVehicle"
    /// 
    /// Yer istasyonu veya gateway gibi araç olmayan node'larda boş kalabilir.
    /// </summary>
    public string VehicleType { get; init; } = string.Empty;

    /// <summary>
    /// Node'un aktif operasyon rolü.
    /// 
    /// Örnekler:
    /// - "Leader"
    /// - "Follower"
    /// - "Scout"
    /// - "Relay"
    /// - "Mapper"
    /// - "Idle"
    /// 
    /// FleetCoordinator ileride bu rolü görev dağıtımı ve koordinasyon için kullanır.
    /// </summary>
    public string Role { get; init; } = "Idle";

    /// <summary>
    /// Node'un yazılım sürümü.
    /// 
    /// Kullanım alanları:
    /// - Farklı araçların hangi Hydronom sürümünde çalıştığını görmek,
    /// - Uyumluluk kontrolü yapmak,
    /// - Hata ayıklamada sürüm farklarını takip etmek.
    /// </summary>
    public string SoftwareVersion { get; init; } = string.Empty;

    /// <summary>
    /// Node'un donanım profili veya platform adı.
    /// 
    /// Örnekler:
    /// - "JetsonNano"
    /// - "RaspberryPi5"
    /// - "WindowsGroundStation"
    /// - "STM32Bridge"
    /// - "Simulation"
    /// </summary>
    public string HardwareProfile { get; init; } = string.Empty;

    /// <summary>
    /// Bu node'un simülasyon node'u olup olmadığını belirtir.
    /// 
    /// true ise:
    /// - Fiziksel araç olmayabilir.
    /// - Test/replay/simülasyon amaçlı kullanılabilir.
    /// - FleetRegistry bunu gerçek araçlardan ayrı gösterebilir.
    /// </summary>
    public bool IsSimulation { get; init; }

    /// <summary>
    /// Kimlik bilgisinin temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından NodeId dolu olmalıdır.
    /// Çünkü Fleet mimarisinde her node benzersiz bir ID ile takip edilir.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(NodeId);
}