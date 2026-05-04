namespace Hydronom.Core.Fusion.Diagnostics;

/// <summary>
/// Fusion input sayısı, kullanılan sensörler, reddedilen sample'lar ve confidence bilgisini taşır.
/// </summary>
public readonly record struct FusionDiagnostics(
    string FusionEngineName,
    DateTime TimestampUtc,
    int InputSampleCount,
    int UsedSampleCount,
    int RejectedSampleCount,
    IReadOnlyList<string> UsedSensorIds,
    IReadOnlyList<string> RejectedSampleIds,
    double Confidence,
    bool ProducedCandidate,
    string Summary
)
{
    public static FusionDiagnostics Empty(string engineName = "fusion")
    {
        return new FusionDiagnostics(
            FusionEngineName: Normalize(engineName, "fusion"),
            TimestampUtc: DateTime.UtcNow,
            InputSampleCount: 0,
            UsedSampleCount: 0,
            RejectedSampleCount: 0,
            UsedSensorIds: Array.Empty<string>(),
            RejectedSampleIds: Array.Empty<string>(),
            Confidence: 0.0,
            ProducedCandidate: false,
            Summary: "No fusion input."
        );
    }

    public FusionDiagnostics Sanitized()
    {
        return new FusionDiagnostics(
            FusionEngineName: Normalize(FusionEngineName, "fusion"),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            InputSampleCount: Math.Max(0, InputSampleCount),
            UsedSampleCount: Math.Max(0, UsedSampleCount),
            RejectedSampleCount: Math.Max(0, RejectedSampleCount),
            UsedSensorIds: NormalizeList(UsedSensorIds),
            RejectedSampleIds: NormalizeList(RejectedSampleIds),
            Confidence: Clamp01(Confidence),
            ProducedCandidate: ProducedCandidate,
            Summary: Normalize(Summary, "Fusion diagnostics.")
        );
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}