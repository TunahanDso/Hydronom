namespace HydronomOps.Gateway.Infrastructure.Time;

/// <summary>
/// Zaman erişimini soyutlar.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Şu anki UTC zamanı.
    /// </summary>
    DateTime UtcNow { get; }
}