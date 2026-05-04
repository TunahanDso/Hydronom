using Hydronom.Core.Fusion.Diagnostics;

namespace Hydronom.Runtime.FusionRuntime;

/// <summary>
/// Fusion çıktısını Gateway/Ops debug telemetry formatına dönüştürmek için ilk sade köprü.
/// Bu paket içinde DTO üretmek yerine güvenli debug summary üretir.
/// </summary>
public sealed class FusionTelemetryBridge
{
    private readonly FusionDiagnosticsBridge _diagnosticsBridge = new();

    public string ProjectDebugSummary(FusionRuntimeHostTickResult tickResult)
    {
        var safe = tickResult.Sanitized();

        return
            $"FusionTick samples={safe.InputSampleCount}, " +
            $"candidate={safe.CandidateProduced}, " +
            $"submitted={safe.StateUpdateSubmitted}, " +
            $"accepted={safe.StateUpdateAccepted}, " +
            $"decision={safe.Decision}, " +
            $"stateVehicle={safe.Telemetry.VehicleId}, " +
            $"stateConfidence={safe.Telemetry.Confidence:F2}, " +
            _diagnosticsBridge.BuildSummary(safe.FusionDiagnostics);
    }

    public string ProjectDiagnosticsSummary(FusionDiagnostics diagnostics)
    {
        return _diagnosticsBridge.BuildSummary(diagnostics);
    }
}