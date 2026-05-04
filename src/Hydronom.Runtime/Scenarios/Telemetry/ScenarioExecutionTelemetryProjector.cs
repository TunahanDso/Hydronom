using Hydronom.Runtime.Scenarios.Execution;
using Hydronom.Runtime.Telemetry;
using Hydronom.Runtime.Testing.Scenarios;

namespace Hydronom.Runtime.Scenarios.Telemetry;

/// <summary>
/// ScenarioExecutionResult ve kinematic timeline state'lerini RuntimeTelemetrySummary formatına dönüştürür.
/// Bu sınıf Digital Proving Ground çıktısını Gateway/Ops tarafına taşımaya hazırlayan ilk köprüdür.
/// </summary>
public sealed class ScenarioExecutionTelemetryProjector
{
    /// <summary>
    /// Scenario execution final sonucundan tek bir final RuntimeTelemetrySummary üretir.
    /// </summary>
    public RuntimeTelemetrySummary ProjectFinalSummary(ScenarioExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var report = result.Report;
        var finalState = result.Timeline.Count > 0
            ? result.Timeline[^1]
            : new RuntimeScenarioVehicleState
            {
                VehicleId = string.IsNullOrWhiteSpace(report.VehicleId) ? "hydronom-main" : report.VehicleId,
                TimestampUtc = result.FinishedUtc,
                X = report.FinalVehicleX,
                Y = report.FinalVehicleY,
                Z = report.FinalVehicleZ,
                YawDeg = report.FinalVehicleYawDeg
            };

        return ProjectState(
            scenarioId: result.ScenarioId,
            runId: result.RunId,
            state: finalState,
            sequenceIndex: Math.Max(result.TickCount, 0),
            totalSequenceCount: Math.Max(result.TickCount, 1),
            overallHealth: ResolveOverallHealth(result),
            hasCriticalIssue: result.IsFailure || result.IsTimedOut || result.IsAborted,
            hasWarnings: result.Report.Penalty > 0.0,
            fusionConfidence: GetMetric(result.Metrics, "fusionConfidence", 1.0),
            stateConfidence: GetMetric(result.Metrics, "stateConfidence", 1.0),
            summary: BuildFinalSummary(result));
    }

    /// <summary>
    /// Scenario execution timeline'ını RuntimeTelemetrySummary listesine dönüştürür.
    /// Bu çıktı ileride Gateway'e canlı akıtılabilir veya replay olarak kullanılabilir.
    /// </summary>
    public IReadOnlyList<RuntimeTelemetrySummary> ProjectTimeline(ScenarioExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Timeline.Count == 0)
        {
            return Array.Empty<RuntimeTelemetrySummary>();
        }

        var summaries = new List<RuntimeTelemetrySummary>(result.Timeline.Count);
        var total = result.Timeline.Count;

        for (var i = 0; i < result.Timeline.Count; i++)
        {
            var state = result.Timeline[i];

            summaries.Add(
                ProjectState(
                    scenarioId: result.ScenarioId,
                    runId: result.RunId,
                    state: state,
                    sequenceIndex: i,
                    totalSequenceCount: total,
                    overallHealth: "Healthy",
                    hasCriticalIssue: false,
                    hasWarnings: false,
                    fusionConfidence: 1.0,
                    stateConfidence: 1.0,
                    summary: $"Scenario replay tick {i + 1}/{total}: scenario={result.ScenarioId}, run={result.RunId}"));
        }

        return summaries;
    }

    /// <summary>
    /// Tek bir RuntimeScenarioVehicleState'i RuntimeTelemetrySummary formatına dönüştürür.
    /// </summary>
    public RuntimeTelemetrySummary ProjectState(
        string scenarioId,
        string runId,
        RuntimeScenarioVehicleState state,
        int sequenceIndex,
        int totalSequenceCount,
        string overallHealth = "Healthy",
        bool hasCriticalIssue = false,
        bool hasWarnings = false,
        double fusionConfidence = 1.0,
        double stateConfidence = 1.0,
        string? summary = null)
    {
        var safeScenarioId = Normalize(scenarioId, "unknown_scenario");
        var safeRunId = Normalize(runId, "unknown_run");
        var timestamp = state.TimestampUtc ?? DateTime.UtcNow;

        return new RuntimeTelemetrySummary(
            RuntimeId: "hydronom_scenario_executor",
            TimestampUtc: timestamp,
            OverallHealth: Normalize(overallHealth, "Healthy"),
            HasCriticalIssue: hasCriticalIssue,
            HasWarnings: hasWarnings,

            // Bu projector şimdilik gerçek sensör runtime'ı temsil etmiyor.
            // Scenario execution/replay state'ini Ops'a göstermek için state telemetry üretiyor.
            SensorCount: 0,
            HealthySensorCount: 0,

            FusionEngineName: "scenario_kinematic_executor",
            FusionProducedCandidate: true,
            FusionConfidence: Clamp01(fusionConfidence),

            VehicleId: Normalize(state.VehicleId, "hydronom-main"),
            HasState: true,
            StateX: Safe(state.X),
            StateY: Safe(state.Y),
            StateZ: Safe(state.Z),
            StateYawDeg: Safe(state.YawDeg),
            StateConfidence: Clamp01(stateConfidence),

            LastStateDecision: "ScenarioReplay",
            LastStateAccepted: true,
            AcceptedStateUpdateCount: Math.Max(sequenceIndex + 1, 1),
            RejectedStateUpdateCount: 0,

            Summary: summary ??
                     $"Scenario telemetry: scenario={safeScenarioId}, run={safeRunId}, tick={sequenceIndex + 1}/{Math.Max(totalSequenceCount, 1)}"
        ).Sanitized();
    }

    private static string ResolveOverallHealth(ScenarioExecutionResult result)
    {
        if (result.IsFailure || result.IsTimedOut || result.IsAborted)
        {
            return "Critical";
        }

        if (result.Report.Penalty > 0.0)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static string BuildFinalSummary(ScenarioExecutionResult result)
    {
        return
            $"Scenario execution final: scenario={result.ScenarioId}, run={result.RunId}, " +
            $"status={result.FinalStatus}, success={result.IsSuccess}, " +
            $"objectives={result.Report.CompletedObjectiveCount}/{result.Report.TotalObjectiveCount}, " +
            $"score={result.Report.Score:F1}, net={result.Report.NetScore:F1}, " +
            $"elapsed={result.SimulatedElapsedSeconds:F2}s, ticks={result.TickCount}";
    }

    private static double GetMetric(
        IReadOnlyDictionary<string, double> metrics,
        string key,
        double fallback)
    {
        return metrics.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    private static double Safe(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
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

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}