namespace HydronomOps.Gateway.Infrastructure.Time;

/// <summary>
/// Zaman eriÅŸimini soyutlar.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Åu anki UTC zamanÄ±.
    /// </summary>
    DateTime UtcNow { get; }
}
