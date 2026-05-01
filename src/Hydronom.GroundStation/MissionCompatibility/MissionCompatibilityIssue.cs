namespace Hydronom.GroundStation.MissionCompatibility;

/// <summary>
/// Mission compatibility değerlendirmesinde bulunan tek bir problemi temsil eder.
/// </summary>
public sealed record MissionCompatibilityIssue
{
    /// <summary>
    /// Problem kodu.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// İnsan-okunabilir açıklama.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Bu problem göreve uygunluğu tamamen engeller mi?
    /// </summary>
    public bool IsBlocking { get; init; } = true;

    /// <summary>
    /// Problem üretilen UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public static MissionCompatibilityIssue Blocking(string code, string message)
    {
        return new MissionCompatibilityIssue
        {
            Code = code,
            Message = message,
            IsBlocking = true
        };
    }

    public static MissionCompatibilityIssue Warning(string code, string message)
    {
        return new MissionCompatibilityIssue
        {
            Code = code,
            Message = message,
            IsBlocking = false
        };
    }
}