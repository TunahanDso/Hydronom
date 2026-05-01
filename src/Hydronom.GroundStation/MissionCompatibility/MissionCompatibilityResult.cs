namespace Hydronom.GroundStation.MissionCompatibility;

using Hydronom.Core.Fleet;

/// <summary>
/// Bir aracın belirli bir göreve uygunluk değerlendirme sonucunu temsil eder.
/// </summary>
public sealed record MissionCompatibilityResult
{
    /// <summary>
    /// Değerlendirilen aracın node id değeri.
    /// </summary>
    public string VehicleId { get; init; } = string.Empty;

    /// <summary>
    /// Değerlendirilen aracın tipi.
    /// </summary>
    public string VehicleType { get; init; } = string.Empty;

    /// <summary>
    /// Görev tipi.
    /// </summary>
    public string MissionType { get; init; } = string.Empty;

    /// <summary>
    /// Araç bu göreve uygun mu?
    /// </summary>
    public bool IsCompatible { get; init; }

    /// <summary>
    /// Araç bu görev için reddedildi mi?
    /// </summary>
    public bool IsRejected => !IsCompatible;

    /// <summary>
    /// Uyumluluk skoru.
    /// 
    /// İlk sürümde 0-100 arası normalize edilir.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Kısa sonuç açıklaması.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Değerlendirme sırasında bulunan problemler.
    /// </summary>
    public IReadOnlyList<MissionCompatibilityIssue> Issues { get; init; } =
        Array.Empty<MissionCompatibilityIssue>();

    /// <summary>
    /// Eşleşen capability adları.
    /// </summary>
    public IReadOnlyList<string> MatchedCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Eksik required capability adları.
    /// </summary>
    public IReadOnlyList<string> MissingRequiredCapabilities { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Blocking problem var mı?
    /// </summary>
    public bool HasBlockingIssues => Issues.Any(x => x.IsBlocking);

    /// <summary>
    /// Warning problem var mı?
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