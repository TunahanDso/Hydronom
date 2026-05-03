namespace HydronomOps.Gateway.Infrastructure.Time;

/// <summary>
/// Sistem zamanÄ±nÄ± saÄŸlayan basit saat servisi.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <summary>
    /// UTC ÅŸimdi.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;
}
