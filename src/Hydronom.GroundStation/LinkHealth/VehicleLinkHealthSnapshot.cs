namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Bir aracın tüm bağlantı sağlığını özetleyen snapshot.
/// </summary>
public sealed record VehicleLinkHealthSnapshot(
    string VehicleId,
    LinkHealthStatus OverallStatus,
    double OverallQualityScore,
    IReadOnlyList<LinkHealthSnapshot> Links
);