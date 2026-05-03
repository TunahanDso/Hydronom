namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Bir aracÄ±n tÃ¼m baÄŸlantÄ± saÄŸlÄ±ÄŸÄ±nÄ± Ã¶zetleyen snapshot.
/// </summary>
public sealed record VehicleLinkHealthSnapshot(
    string VehicleId,
    LinkHealthStatus OverallStatus,
    double OverallQualityScore,
    IReadOnlyList<LinkHealthSnapshot> Links
);
