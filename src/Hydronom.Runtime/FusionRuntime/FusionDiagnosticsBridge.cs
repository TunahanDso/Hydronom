using Hydronom.Core.Fusion.Diagnostics;

namespace Hydronom.Runtime.FusionRuntime;

/// <summary>
/// Fusion diagnostics bilgisini runtime katmanında sade özet metne çevirir.
/// İleride operation snapshot ve runtime diagnostics'e bağlanacaktır.
/// </summary>
public sealed class FusionDiagnosticsBridge
{
    public string BuildSummary(FusionDiagnostics diagnostics)
    {
        var safe = diagnostics.Sanitized();

        return
            $"Fusion={safe.FusionEngineName}, " +
            $"inputs={safe.InputSampleCount}, " +
            $"used={safe.UsedSampleCount}, " +
            $"rejected={safe.RejectedSampleCount}, " +
            $"candidate={safe.ProducedCandidate}, " +
            $"confidence={safe.Confidence:F2}, " +
            $"summary={safe.Summary}";
    }
}