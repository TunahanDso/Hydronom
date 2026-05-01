namespace Hydronom.GroundStation.Security;

/// <summary>
/// Komut doğrulama sürecinde bulunan tek bir problemi temsil eder.
/// </summary>
public sealed record CommandValidationIssue
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
    /// Bu problem komutu tamamen reddetmeli mi?
    /// </summary>
    public bool IsBlocking { get; init; } = true;

    /// <summary>
    /// Problem üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public static CommandValidationIssue Blocking(string code, string message)
    {
        return new CommandValidationIssue
        {
            Code = code,
            Message = message,
            IsBlocking = true
        };
    }

    public static CommandValidationIssue Warning(string code, string message)
    {
        return new CommandValidationIssue
        {
            Code = code,
            Message = message,
            IsBlocking = false
        };
    }
}