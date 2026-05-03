namespace Hydronom.GroundStation.MissionCompatibility;

/// <summary>
/// Bir gÃ¶revin araÃ§tan beklediÄŸi capability gereksinimini temsil eder.
/// </summary>
public sealed record MissionCapabilityRequirement
{
    /// <summary>
    /// Capability adÄ±.
    /// 
    /// Ã–rnek:
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
    /// true ise yokluÄŸu gÃ¶revi engeller.
    /// false ise preferred/bonus capability gibi deÄŸerlendirilir.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Capability'nin enabled olmasÄ± zorunlu mu?
    /// </summary>
    public bool RequireEnabled { get; init; } = true;

    /// <summary>
    /// Capability health deÄŸerinin OK olmasÄ± zorunlu mu?
    /// </summary>
    public bool RequireHealthy { get; init; } = true;

    /// <summary>
    /// Bu capability eÅŸleÅŸince skora eklenecek aÄŸÄ±rlÄ±k.
    /// </summary>
    public double Weight { get; init; } = 1.0;
}
