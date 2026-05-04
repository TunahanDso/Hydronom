namespace Hydronom.Core.World.Diagnostics;

/// <summary>
/// World model için basit diagnostik özet.
/// </summary>
public sealed record WorldDiagnostics
{
    public int ObjectCount { get; init; }

    public int ActiveObjectCount { get; init; }

    public int BlockingObjectCount { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool HasWorldObjects => ObjectCount > 0;

    public string Summary =>
        $"World objects={ObjectCount}, active={ActiveObjectCount}, blocking={BlockingObjectCount}";
}