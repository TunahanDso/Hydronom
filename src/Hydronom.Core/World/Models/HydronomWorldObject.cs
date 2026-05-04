namespace Hydronom.Core.World.Models;

/// <summary>
/// Runtime içinde kullanılan genel dünya objesi.
/// Scenario objeleri bu modele bind edilir.
/// </summary>
public sealed record HydronomWorldObject
{
    public string Id { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Layer { get; init; } = "mission";

    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    public double Radius { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public double YawDeg { get; init; }

    public bool IsActive { get; init; } = true;

    public bool IsBlocking { get; init; }

    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsObstacleLike =>
        IsBlocking ||
        Kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase) ||
        Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase) ||
        Kind.Equals("no_go_zone", StringComparison.OrdinalIgnoreCase);
}