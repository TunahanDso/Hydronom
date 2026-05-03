namespace Hydronom.Core.Fleet;

/// <summary>
/// Hydronom Fleet mimarisinde bir dÃ¼ÄŸÃ¼mÃ¼n kimlik bilgisini temsil eder.
/// 
/// DÃ¼ÄŸÃ¼m; yalnÄ±zca araÃ§ olmak zorunda deÄŸildir.
/// Åunlar da birer node olabilir:
/// - Otonom araÃ§
/// - Yer istasyonu
/// - Ops Gateway
/// - Replay sistemi
/// - SimÃ¼lasyon node'u
/// - Relay gÃ¶revi yapan ara dÃ¼ÄŸÃ¼m
/// 
/// Bu modelin amacÄ±, FleetRegistry ve haberleÅŸme katmanÄ±nÄ±n
/// sistemdeki her bileÅŸeni tekil ve anlaÅŸÄ±lÄ±r ÅŸekilde tanÄ±yabilmesidir.
/// </summary>
public sealed record NodeIdentity
{
    /// <summary>
    /// Node'un benzersiz kimliÄŸi.
    /// 
    /// Ã–rnekler:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// - "SIM-VEHICLE-001"
    /// 
    /// Bu alan mesajlaÅŸmada SourceNodeId / TargetNodeId ile eÅŸleÅŸir.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// Node'un insan tarafÄ±ndan okunabilir adÄ±.
    /// 
    /// Ã–rnekler:
    /// - "Alpha"
    /// - "Beta"
    /// - "Main Ground Station"
    /// - "Hydronom Ops Gateway"
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Node'un genel tÃ¼rÃ¼.
    /// 
    /// Ã–rnekler:
    /// - "Vehicle"
    /// - "GroundStation"
    /// - "Gateway"
    /// - "Simulator"
    /// - "Relay"
    /// 
    /// Åimdilik string bÄ±rakÄ±yoruz.
    /// Ã‡Ã¼nkÃ¼ ileride farklÄ± node tÃ¼rleri eklenebilir.
    /// Gerekirse daha sonra enum'a Ã§evrilebilir.
    /// </summary>
    public string NodeType { get; init; } = "Unknown";

    /// <summary>
    /// AraÃ§ node'larÄ± iÃ§in araÃ§ tipi.
    /// 
    /// Ã–rnekler:
    /// - "SurfaceVessel"
    /// - "Submarine"
    /// - "SailingVessel"
    /// - "AerialVehicle"
    /// - "GroundVehicle"
    /// 
    /// Yer istasyonu veya gateway gibi araÃ§ olmayan node'larda boÅŸ kalabilir.
    /// </summary>
    public string VehicleType { get; init; } = string.Empty;

    /// <summary>
    /// Node'un aktif operasyon rolÃ¼.
    /// 
    /// Ã–rnekler:
    /// - "Leader"
    /// - "Follower"
    /// - "Scout"
    /// - "Relay"
    /// - "Mapper"
    /// - "Idle"
    /// 
    /// FleetCoordinator ileride bu rolÃ¼ gÃ¶rev daÄŸÄ±tÄ±mÄ± ve koordinasyon iÃ§in kullanÄ±r.
    /// </summary>
    public string Role { get; init; } = "Idle";

    /// <summary>
    /// Node'un yazÄ±lÄ±m sÃ¼rÃ¼mÃ¼.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - FarklÄ± araÃ§larÄ±n hangi Hydronom sÃ¼rÃ¼mÃ¼nde Ã§alÄ±ÅŸtÄ±ÄŸÄ±nÄ± gÃ¶rmek,
    /// - Uyumluluk kontrolÃ¼ yapmak,
    /// - Hata ayÄ±klamada sÃ¼rÃ¼m farklarÄ±nÄ± takip etmek.
    /// </summary>
    public string SoftwareVersion { get; init; } = string.Empty;

    /// <summary>
    /// Node'un donanÄ±m profili veya platform adÄ±.
    /// 
    /// Ã–rnekler:
    /// - "JetsonNano"
    /// - "RaspberryPi5"
    /// - "WindowsGroundStation"
    /// - "STM32Bridge"
    /// - "Simulation"
    /// </summary>
    public string HardwareProfile { get; init; } = string.Empty;

    /// <summary>
    /// Bu node'un simÃ¼lasyon node'u olup olmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// true ise:
    /// - Fiziksel araÃ§ olmayabilir.
    /// - Test/replay/simÃ¼lasyon amaÃ§lÄ± kullanÄ±labilir.
    /// - FleetRegistry bunu gerÃ§ek araÃ§lardan ayrÄ± gÃ¶sterebilir.
    /// </summary>
    public bool IsSimulation { get; init; }

    /// <summary>
    /// Kimlik bilgisinin temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan NodeId dolu olmalÄ±dÄ±r.
    /// Ã‡Ã¼nkÃ¼ Fleet mimarisinde her node benzersiz bir ID ile takip edilir.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(NodeId);
}
