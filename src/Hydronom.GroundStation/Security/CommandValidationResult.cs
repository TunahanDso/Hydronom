namespace Hydronom.GroundStation.Security;

/// <summary>
/// Komut doğrulama, yetki ve safety gate sonucunu temsil eder.
/// </summary>
public sealed record CommandValidationResult
{
    /// <summary>
    /// Komut gönderilebilir mi?
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Komut tamamen reddedildi mi?
    /// </summary>
    public bool IsRejected => !IsAllowed;

    /// <summary>
    /// Sonuç kısa açıklaması.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Tespit edilen problemler.
    /// </summary>
    public IReadOnlyList<CommandValidationIssue> Issues { get; init; } =
        Array.Empty<CommandValidationIssue>();

    /// <summary>
    /// Blocking problem var mı?
    /// </summary>
    public bool HasBlockingIssues => Issues.Any(x => x.IsBlocking);

    /// <summary>
    /// Warning problem var mı?
    /// </summary>
    public bool HasWarnings => Issues.Any(x => !x.IsBlocking);

    public static CommandValidationResult Allowed(string reason)
    {
        return new CommandValidationResult
        {
            IsAllowed = true,
            Reason = reason
        };
    }

    public static CommandValidationResult FromIssues(
        IEnumerable<CommandValidationIssue> issues,
        string? reason = null)
    {
        var issueArray = issues?.ToArray() ?? Array.Empty<CommandValidationIssue>();
        var hasBlocking = issueArray.Any(x => x.IsBlocking);

        return new CommandValidationResult
        {
            IsAllowed = !hasBlocking,
            Issues = issueArray,
            Reason = reason ?? (hasBlocking
                ? "Command rejected by Ground Station safety/security validation."
                : "Command allowed with warnings.")
        };
    }
}