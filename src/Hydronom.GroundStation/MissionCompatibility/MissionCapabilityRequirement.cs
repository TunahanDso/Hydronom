namespace Hydronom.GroundStation.MissionCompatibility;

/// <summary>
/// Bir görevin araçtan beklediği capability gereksinimini temsil eder.
/// </summary>
public sealed record MissionCapabilityRequirement
{
    /// <summary>
    /// Capability adı.
    /// 
    /// Örnek:
    /// - navigation
    /// - lidar
    /// - camera
    /// - underwater_navigation
    /// - aerial_mapping
    /// - relay
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Bu capability zorunlu mu?
    /// 
    /// true ise yokluğu görevi engeller.
    /// false ise preferred/bonus capability gibi değerlendirilir.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Capability'nin enabled olması zorunlu mu?
    /// </summary>
    public bool RequireEnabled { get; init; } = true;

    /// <summary>
    /// Capability health değerinin OK olması zorunlu mu?
    /// </summary>
    public bool RequireHealthy { get; init; } = true;

    /// <summary>
    /// Bu capability eşleşince skora eklenecek ağırlık.
    /// </summary>
    public double Weight { get; init; } = 1.0;
}