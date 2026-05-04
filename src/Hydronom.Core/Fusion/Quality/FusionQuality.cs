namespace Hydronom.Core.Fusion.Quality;

/// <summary>
/// Fusion çıktısının güven, tazelik ve kullanılabilirlik kalitesi.
/// </summary>
public readonly record struct FusionQuality(
    bool Valid,
    double Confidence,
    double InputFreshnessMs,
    int UsedSampleCount,
    int RejectedSampleCount,
    string Summary
)
{
    public static FusionQuality Invalid(string reason)
    {
        return new FusionQuality(
            Valid: false,
            Confidence: 0.0,
            InputFreshnessMs: double.PositiveInfinity,
            UsedSampleCount: 0,
            RejectedSampleCount: 0,
            Summary: string.IsNullOrWhiteSpace(reason) ? "Invalid fusion output." : reason.Trim()
        );
    }

    public static FusionQuality FromInputs(
        double confidence,
        double freshnessMs,
        int usedSampleCount,
        int rejectedSampleCount,
        string summary)
    {
        return new FusionQuality(
            Valid: usedSampleCount > 0 && confidence > 0.0,
            Confidence: confidence,
            InputFreshnessMs: freshnessMs,
            UsedSampleCount: usedSampleCount < 0 ? 0 : usedSampleCount,
            RejectedSampleCount: rejectedSampleCount < 0 ? 0 : rejectedSampleCount,
            Summary: string.IsNullOrWhiteSpace(summary) ? "Fusion quality." : summary.Trim()
        ).Sanitized();
    }

    public FusionQuality Sanitized()
    {
        return new FusionQuality(
            Valid: Valid,
            Confidence: Clamp01(Confidence),
            InputFreshnessMs: SafeNonNegativeOrInfinity(InputFreshnessMs),
            UsedSampleCount: UsedSampleCount < 0 ? 0 : UsedSampleCount,
            RejectedSampleCount: RejectedSampleCount < 0 ? 0 : RejectedSampleCount,
            Summary: string.IsNullOrWhiteSpace(Summary) ? "Fusion quality." : Summary.Trim()
        );
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

    private static double SafeNonNegativeOrInfinity(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return value;
        }

        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        return value < 0.0 ? 0.0 : value;
    }
}