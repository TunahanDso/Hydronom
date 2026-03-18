namespace HydronomOps.Gateway.Infrastructure.Time;

/// <summary>
/// Sistem zamanını sağlayan basit saat servisi.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <summary>
    /// UTC şimdi.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;
}