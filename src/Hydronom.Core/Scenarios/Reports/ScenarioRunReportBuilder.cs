using Hydronom.Core.Scenarios.Judging;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.Scenarios.Runtime;

namespace Hydronom.Core.Scenarios.Reports;

/// <summary>
/// ScenarioJudgeResult, ScenarioRunState ve ScenarioDefinition verilerinden
/// final ScenarioRunReport üreten yardımcı sınıftır.
/// </summary>
public static class ScenarioRunReportBuilder
{
    /// <summary>
    /// Senaryo koşusu için final rapor üretir.
    /// </summary>
    public static ScenarioRunReport Build(
        ScenarioDefinition scenario,
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(judgeResult);

        var finalStatus = ResolveFinalStatus(runState, judgeResult);
        var isSuccess = judgeResult.IsSuccess;
        var isFailure = judgeResult.IsFailure || string.Equals(finalStatus, ScenarioRunStatus.Failed, StringComparison.OrdinalIgnoreCase);
        var isTimedOut = string.Equals(judgeResult.Status, ScenarioJudgeStatus.Timeout, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(finalStatus, ScenarioRunStatus.Timeout, StringComparison.OrdinalIgnoreCase);
        var isAborted = string.Equals(finalStatus, ScenarioRunStatus.Aborted, StringComparison.OrdinalIgnoreCase);

        var failureReason = ResolveFailureReason(runState, judgeResult, isTimedOut, isAborted);

        var metrics = MergeMetrics(runState, judgeResult);
        var fields = MergeFields(scenario, runState, judgeResult);

        return new ScenarioRunReport
        {
            ScenarioId = scenario.Id,
            ScenarioName = scenario.Name,
            ScenarioFamily = scenario.ScenarioFamily,
            RunId = runState.RunId,
            VehicleId = runState.VehicleId,
            VehiclePlatform = runState.VehiclePlatform,

            GeneratedUtc = DateTime.UtcNow,
            StartedUtc = runState.StartedUtc ?? judgeResult.StartedUtc,
            FinishedUtc = runState.FinishedUtc ?? judgeResult.FinishedUtc,
            ElapsedSeconds = ResolveElapsedSeconds(runState, judgeResult),

            FinalStatus = finalStatus,
            JudgeStatus = judgeResult.Status,

            IsSuccess = isSuccess,
            IsFailure = isFailure,
            IsTimedOut = isTimedOut,
            IsAborted = isAborted,
            FailureReason = failureReason,

            Score = judgeResult.Score,
            Penalty = judgeResult.Penalty,
            MinimumSuccessScore = scenario.MinimumSuccessScore,
            CompletionRatio = judgeResult.CompletionRatio,

            TotalObjectiveCount = judgeResult.TotalObjectiveCount,
            CompletedObjectiveCount = judgeResult.CompletedObjectiveCount,
            FailedObjectiveCount = judgeResult.FailedObjectiveCount,

            FinalObjectiveId = judgeResult.CurrentObjectiveId ?? runState.CurrentObjectiveId,
            FinalObjectiveTitle = judgeResult.CurrentObjectiveTitle ?? runState.CurrentObjectiveTitle,

            CollisionCount = judgeResult.CollisionCount,
            NoGoZoneViolationCount = judgeResult.NoGoZoneViolationCount,
            DegradedEventCount = judgeResult.DegradedEventCount,
            SafetyInterventionCount = judgeResult.SafetyInterventionCount,

            MaxSpeedMetersPerSecond = GetMetric(metrics, "run.maxSpeedMps"),
            AverageSpeedMetersPerSecond = GetMetric(metrics, "run.averageSpeedMps"),
            MaxYawRateDegPerSecond = GetMetric(metrics, "run.maxYawRateDegPerSecond"),
            TotalTravelDistanceMeters = GetMetric(metrics, "run.totalTravelDistanceMeters"),
            TotalHorizontalTravelDistanceMeters = GetMetric(metrics, "run.totalHorizontalTravelDistanceMeters"),
            TotalVerticalTravelDistanceMeters = GetMetric(metrics, "run.totalVerticalTravelDistanceMeters"),

            FinalVehicleX = runState.VehicleX,
            FinalVehicleY = runState.VehicleY,
            FinalVehicleZ = runState.VehicleZ,
            FinalVehicleYawDeg = runState.VehicleYawDeg,

            Summary = BuildSummary(
                finalStatus,
                judgeResult,
                scenario.MinimumSuccessScore,
                failureReason),

            JudgeResult = judgeResult,
            FinalRunState = runState,
            Objectives = judgeResult.Objectives,
            Events = judgeResult.Events,
            Violations = judgeResult.Violations,
            FaultEvents = BuildFaultEvents(scenario, runState, judgeResult),

            Metrics = metrics,
            Fields = fields
        };
    }

    private static string ResolveFinalStatus(ScenarioRunState runState, ScenarioJudgeResult judgeResult)
    {
        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Success, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Completed;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Timeout, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Timeout;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Aborted, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Aborted;
        }

        if (string.Equals(judgeResult.Status, ScenarioJudgeStatus.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioRunStatus.Failed;
        }

        if (!string.IsNullOrWhiteSpace(runState.Status))
        {
            return runState.Status;
        }

        return ScenarioRunStatus.NotStarted;
    }

    private static string? ResolveFailureReason(
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult,
        bool isTimedOut,
        bool isAborted)
    {
        if (!string.IsNullOrWhiteSpace(judgeResult.FailureReason))
        {
            return judgeResult.FailureReason;
        }

        if (!string.IsNullOrWhiteSpace(runState.FailureReason))
        {
            return runState.FailureReason;
        }

        if (isTimedOut)
        {
            return "Scenario timed out.";
        }

        if (isAborted)
        {
            return "Scenario aborted.";
        }

        return null;
    }

    private static double ResolveElapsedSeconds(
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult)
    {
        if (runState.ElapsedSeconds > 0.0)
        {
            return runState.ElapsedSeconds;
        }

        if (judgeResult.ElapsedSeconds > 0.0)
        {
            return judgeResult.ElapsedSeconds;
        }

        if (runState.StartedUtc.HasValue && runState.FinishedUtc.HasValue)
        {
            return Math.Max((runState.FinishedUtc.Value - runState.StartedUtc.Value).TotalSeconds, 0.0);
        }

        if (judgeResult.StartedUtc.HasValue && judgeResult.FinishedUtc.HasValue)
        {
            return Math.Max((judgeResult.FinishedUtc.Value - judgeResult.StartedUtc.Value).TotalSeconds, 0.0);
        }

        return 0.0;
    }

    private static Dictionary<string, double> MergeMetrics(
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult)
    {
        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in runState.Metrics)
        {
            metrics[pair.Key] = pair.Value;
        }

        foreach (var pair in judgeResult.Metrics)
        {
            metrics[pair.Key] = pair.Value;
        }

        metrics["report.score"] = judgeResult.Score;
        metrics["report.penalty"] = judgeResult.Penalty;
        metrics["report.netScore"] = judgeResult.NetScore;
        metrics["report.completionRatio"] = judgeResult.CompletionRatio;

        metrics["final.vehicle.x"] = runState.VehicleX;
        metrics["final.vehicle.y"] = runState.VehicleY;
        metrics["final.vehicle.z"] = runState.VehicleZ;
        metrics["final.vehicle.yawDeg"] = runState.VehicleYawDeg;

        metrics["final.objectives.total"] = judgeResult.TotalObjectiveCount;
        metrics["final.objectives.completed"] = judgeResult.CompletedObjectiveCount;
        metrics["final.objectives.failed"] = judgeResult.FailedObjectiveCount;

        return metrics;
    }

    private static Dictionary<string, string> MergeFields(
        ScenarioDefinition scenario,
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in scenario.Tags)
        {
            fields[$"scenario.tag.{pair.Key}"] = pair.Value;
        }

        foreach (var pair in runState.Fields)
        {
            fields[pair.Key] = pair.Value;
        }

        foreach (var pair in judgeResult.Fields)
        {
            fields[pair.Key] = pair.Value;
        }

        fields["scenario.id"] = scenario.Id;
        fields["scenario.name"] = scenario.Name;
        fields["scenario.family"] = scenario.ScenarioFamily;
        fields["scenario.version"] = scenario.Version;
        fields["scenario.coordinateFrame"] = scenario.CoordinateFrame;
        fields["scenario.runMode"] = scenario.RunMode;

        fields["run.id"] = runState.RunId;
        fields["run.status"] = runState.Status;
        fields["vehicle.id"] = runState.VehicleId;
        fields["vehicle.platform"] = runState.VehiclePlatform;

        fields["judge.status"] = judgeResult.Status;
        fields["judge.success"] = judgeResult.IsSuccess.ToString();
        fields["judge.failure"] = judgeResult.IsFailure.ToString();

        return fields;
    }

    private static IReadOnlyList<ScenarioRunFaultEvent> BuildFaultEvents(
        ScenarioDefinition scenario,
        ScenarioRunState runState,
        ScenarioJudgeResult judgeResult)
    {
        var faultEvents = new List<ScenarioRunFaultEvent>();

        foreach (var violation in judgeResult.Violations)
        {
            if (!IsFaultLikeViolation(violation))
            {
                continue;
            }

            faultEvents.Add(new ScenarioRunFaultEvent
            {
                ScenarioId = scenario.Id,
                RunId = runState.RunId,
                TimestampUtc = violation.TimestampUtc,
                Type = violation.Type,
                Target = violation.ObjectId ?? violation.ObjectiveId ?? "unknown",
                Severity = violation.Severity,
                Message = violation.Message,
                VehicleX = violation.VehicleX,
                VehicleY = violation.VehicleY,
                VehicleZ = violation.VehicleZ,
                ObjectiveId = violation.ObjectiveId,
                TriggeredDegradedMode = false,
                TriggeredSafetyIntervention = false,
                PenaltyApplied = violation.PenaltyApplied,
                Metrics = violation.Metrics,
                Fields = violation.Fields
            });
        }

        foreach (var ev in judgeResult.Events)
        {
            if (!IsFaultLikeEvent(ev))
            {
                continue;
            }

            faultEvents.Add(new ScenarioRunFaultEvent
            {
                ScenarioId = scenario.Id,
                RunId = runState.RunId,
                TimestampUtc = ev.TimestampUtc,
                Type = ev.Type,
                Target = ev.ObjectId ?? ev.ObjectiveId ?? "runtime",
                Severity = ev.Severity,
                Message = ev.Message,
                VehicleX = ev.VehicleX,
                VehicleY = ev.VehicleY,
                VehicleZ = ev.VehicleZ,
                ObjectiveId = ev.ObjectiveId,
                TriggeredDegradedMode = string.Equals(ev.Type, "DegradedOperation", StringComparison.OrdinalIgnoreCase),
                TriggeredSafetyIntervention =
                    string.Equals(ev.Type, "SafetyLimiterActive", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ev.Type, "EmergencyStop", StringComparison.OrdinalIgnoreCase),
                PenaltyApplied = GetMetric(ev.Metrics, "penalty"),
                Metrics = ev.Metrics,
                Fields = ev.Fields
            });
        }

        return faultEvents
            .OrderBy(x => x.TimestampUtc)
            .ToList();
    }

    private static bool IsFaultLikeViolation(ScenarioJudgeViolation violation)
    {
        return string.Equals(violation.Type, "Timeout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFaultLikeEvent(ScenarioJudgeEvent ev)
    {
        return string.Equals(ev.Type, "DegradedOperation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ev.Type, "SafetyLimiterActive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ev.Type, "EmergencyStop", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSummary(
        string finalStatus,
        ScenarioJudgeResult judgeResult,
        double minimumSuccessScore,
        string? failureReason)
    {
        var baseText =
            $"{finalStatus}: objectives={judgeResult.CompletedObjectiveCount}/{judgeResult.TotalObjectiveCount}, " +
            $"score={judgeResult.Score:F1}, penalty={judgeResult.Penalty:F1}, " +
            $"net={judgeResult.NetScore:F1}, minSuccess={minimumSuccessScore:F1}";

        return string.IsNullOrWhiteSpace(failureReason)
            ? baseText
            : $"{baseText}, failure={failureReason}";
    }

    private static double GetMetric(
        IReadOnlyDictionary<string, double> metrics,
        string key,
        double fallback = 0.0)
    {
        return metrics.TryGetValue(key, out var value)
            ? value
            : fallback;
    }
}