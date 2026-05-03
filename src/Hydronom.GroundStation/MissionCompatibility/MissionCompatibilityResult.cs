namespace Hydronom.GroundStation.MissionCompatibility;

using Hydronom.Core.Fleet;

/// <summary>
/// Bir aracÄ±n belirli bir gÃ¶reve uygunluk deÄŸerlendirme sonucunu temsil eder.
/// </summary>
public sealed record MissionCompatibilityResult
{
    /// <summary>
    /// DeÄŸerlendirilen aracÄ±n node id deÄŸeri.
    /// </summary>
    public string VehicleId { get; init; } = string.Empty;

    /// <summary>
    /// DeÄŸerlendirilen aracÄ±n tipi.
    /// </summary>
    public string VehicleType { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶rev tipi.
    /// </summary>
    public string MissionType { get; init; } = string.Empty;

    /// <summary>
    /// AraÃ§ bu gÃ¶reve uygun mu?
    /// </summary>
    public bool IsCompatible { get; init; }

    /// <summary>
    /// AraÃ§ bu gÃ¶rev iÃ§in reddedildi mi?
    /// </summary>
    public bool IsRejected => !IsCompatible;

    /// <summary>
    /// Uyumluluk skoru.
    /// 
    /// Ä°lk sÃ¼rÃ¼mde 0-100 arasÄ± normalize edilir.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// KÄ±sa sonuÃ§ aÃ§Ä±klamasÄ±.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// DeÄŸerlendirme sÄ±rasÄ±nda bulunan problemler.
    /// </summary>
    public IReadOnlyList<MissionCompatibilityIssue> Issues { get; init; } =
        Array.Empty<MissionCompatibilityIssue>();

    /// <summary>
    /// EÅŸleÅŸen capability adlarÄ±.
    /// </summary>
    public IReadOnlyList<string> MatchedCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Eksik required capability adlarÄ±.
    /// </summary>
    public IReadOnlyList<string> MissingRequiredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Blocking problem var mÄ±?
    /// </summary>
    public bool HasBlockingIssues => Issues.Any(x => x.IsBlocking);

    /// <summary>
    /// Warning problem var mÄ±?
    /// </summary>
    public bool HasWarnings => Issues.Any(x => !x.IsBlocking);

    public static MissionCompatibilityResult Rejected(
        VehicleNodeStatus? vehicle,
        string missionType,
        IEnumerable<MissionCompatibilityIssue> issues,
        string? reason = null)
    {
        var issueArray = issues?.ToArray() ?? Array.Empty<MissionCompatibilityIssue>();

        return new MissionCompatibilityResult
        {
            VehicleId = vehicle?.Identity.NodeId ?? string.Empty,
            VehicleType = vehicle?.Identity.VehicleType ?? string.Empty,
            MissionType = missionType,
            IsCompatible = false,
            Score = 0,
            Issues = issueArray,
            Reason = reason ?? "Vehicle rejected by mission compatibility evaluation."
        };
    }
}
