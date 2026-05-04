namespace Hydronom.Core.World.Layers;

/// <summary>
/// Hydronom world model içinde kullanılan mantıksal katman.
/// </summary>
public sealed record HydronomWorldLayer
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsVisible { get; init; } = true;

    public bool IsOperational { get; init; } = true;

    public static HydronomWorldLayer Mission { get; } = new()
    {
        Id = "mission",
        Name = "Mission",
        IsVisible = true,
        IsOperational = true
    };

    public static HydronomWorldLayer Safety { get; } = new()
    {
        Id = "safety",
        Name = "Safety",
        IsVisible = true,
        IsOperational = true
    };

    public static HydronomWorldLayer Obstacle { get; } = new()
    {
        Id = "obstacle",
        Name = "Obstacle",
        IsVisible = true,
        IsOperational = true
    };
}