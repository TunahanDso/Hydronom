using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.Scenarios.Runtime;

namespace Hydronom.Core.Scenarios.Judging;

/// <summary>
/// Varsayılan senaryo judge implementasyonudur.
/// Digital Proving Ground için temel hedef tamamlama, çarpışma, no-go zone,
/// süre aşımı, degraded/safety olayı ve skor hesabı yapar.
/// </summary>
public sealed class DefaultScenarioJudge : IScenarioJudge
{
    public string Name => "DefaultScenarioJudge";

    public string Version => "1.0.0";

    public ScenarioJudgeResult Evaluate(ScenarioJudgeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scenario = context.Scenario;
        var runState = context.RunState;
        var judge = scenario.Judge ?? new ScenarioJudgeDefinition();

        var now = context.TimestampUtc;
        var previousEvents = context.PreviousEvents ?? Array.Empty<ScenarioJudgeEvent>();
        var previousViolations = context.PreviousViolations ?? Array.Empty<ScenarioJudgeViolation>();

        var objectiveStates = BuildObjectiveStates(context);
        var events = new List<ScenarioJudgeEvent>(previousEvents);
        var violations = new List<ScenarioJudgeViolation>(previousViolations);

        var score = objectiveStates.Sum(x => x.ScoreEarned);
        var penalty = objectiveStates.Sum(x => x.PenaltyApplied);

        ApplyCollisionChecks(context, judge, events, violations, ref penalty);
        ApplyNoGoZoneChecks(context, judge, events, violations, ref penalty);
        ApplyDegradedChecks(context, judge, events, ref penalty);
        ApplySafetyChecks(context, events);

        var completedObjectiveCount = objectiveStates.Count(x => x.IsCompleted);
        var failedObjectiveCount = objectiveStates.Count(x => x.IsFailed);
        var totalObjectiveCount = objectiveStates.Count;

        var completionRatio = totalObjectiveCount <= 0
            ? 0.0
            : Clamp01((double)completedObjectiveCount / totalObjectiveCount);

        var elapsedSeconds = ResolveElapsedSeconds(runState, now);
        var timedOut = scenario.HasTimeLimit && elapsedSeconds > scenario.TimeLimitSeconds;

        if (timedOut)
        {
            AddUniqueViolation(
                violations,
                CreateViolation(
                    type: "Timeout",
                    severity: "Critical",
                    message: $"Senaryo süre sınırını aştı. Elapsed={elapsedSeconds:F2}s, Limit={scenario.TimeLimitSeconds:F2}s",
                    penalty: 0.0,
                    context: context,
                    objectiveId: runState.CurrentObjectiveId,
                    objectId: null));
        }

        var hasCollisionFailure =
            judge.FailOnCollision &&
            violations.Any(x => string.Equals(x.Type, "Collision", StringComparison.OrdinalIgnoreCase));

        var hasNoGoFailure =
            judge.FailOnNoGoZoneViolation &&
            violations.Any(x => string.Equals(x.Type, "NoGoZone", StringComparison.OrdinalIgnoreCase));

        var hasRequiredObjectiveFailure =
            judge.FailOnRequiredObjectiveFailure &&
            objectiveStates.Any(x => x.IsRequired && x.IsFailed);

        var isFailure =
            timedOut ||
            hasCollisionFailure ||
            hasNoGoFailure ||
            hasRequiredObjectiveFailure ||
            context.EmergencyStopActive;

        var netScore = score - penalty;
        var isSuccess =
            !isFailure &&
            totalObjectiveCount > 0 &&
            completedObjectiveCount == totalObjectiveCount &&
            netScore >= scenario.MinimumSuccessScore;

        var status = ResolveStatus(isSuccess, isFailure, timedOut, runState);

        var currentObjective = objectiveStates
            .Where(x => !x.IsCompleted && !x.IsFailed)
            .OrderBy(x => x.Order)
            .FirstOrDefault();

        var nextObjective = objectiveStates
            .Where(x => !x.IsCompleted && !x.IsFailed && x.Order > (currentObjective?.Order ?? -1))
            .OrderBy(x => x.Order)
            .FirstOrDefault();

        var failureReason = ResolveFailureReason(
            isFailure,
            timedOut,
            context,
            hasCollisionFailure,
            hasNoGoFailure,
            hasRequiredObjectiveFailure,
            violations);

        return new ScenarioJudgeResult
        {
            ScenarioId = scenario.Id,
            RunId = runState.RunId,
            TimestampUtc = now,

            Status = status,
            IsSuccess = isSuccess,
            IsFailure = isFailure,
            IsRunning = string.Equals(status, ScenarioJudgeStatus.Running, StringComparison.OrdinalIgnoreCase),

            Score = score,
            Penalty = penalty,
            CompletionRatio = completionRatio,

            TotalObjectiveCount = totalObjectiveCount,
            CompletedObjectiveCount = completedObjectiveCount,
            FailedObjectiveCount = failedObjectiveCount,

            CurrentObjectiveId = currentObjective?.ObjectiveId,
            CurrentObjectiveTitle = currentObjective?.Title,
            NextObjectiveId = nextObjective?.ObjectiveId,

            CollisionCount = violations.Count(x => string.Equals(x.Type, "Collision", StringComparison.OrdinalIgnoreCase)),
            NoGoZoneViolationCount = violations.Count(x => string.Equals(x.Type, "NoGoZone", StringComparison.OrdinalIgnoreCase)),
            DegradedEventCount = CountDegradedEvents(context, events),
            SafetyInterventionCount = CountSafetyEvents(context, events),

            StartedUtc = runState.StartedUtc,
            FinishedUtc = isSuccess || isFailure ? now : runState.FinishedUtc,
            ElapsedSeconds = elapsedSeconds,

            FailureReason = failureReason,
            Summary = BuildSummary(status, completedObjectiveCount, totalObjectiveCount, score, penalty, failureReason),

            Objectives = objectiveStates,
            Events = events,
            Violations = violations,

            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["score"] = score,
                ["penalty"] = penalty,
                ["netScore"] = netScore,
                ["completionRatio"] = completionRatio,
                ["elapsedSeconds"] = elapsedSeconds,
                ["collisionCount"] = violations.Count(x => string.Equals(x.Type, "Collision", StringComparison.OrdinalIgnoreCase)),
                ["noGoZoneViolationCount"] = violations.Count(x => string.Equals(x.Type, "NoGoZone", StringComparison.OrdinalIgnoreCase)),
                ["vehicle.x"] = context.VehicleX,
                ["vehicle.y"] = context.VehicleY,
                ["vehicle.z"] = context.VehicleZ,
                ["vehicle.yawDeg"] = context.VehicleYawDeg,
                ["vehicle.vx"] = context.VehicleVx,
                ["vehicle.vy"] = context.VehicleVy,
                ["vehicle.vz"] = context.VehicleVz,
                ["stateConfidence"] = context.StateConfidence,
                ["fusionConfidence"] = context.FusionConfidence
            },

            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["judge.name"] = Name,
                ["judge.version"] = Version,
                ["scenario.family"] = scenario.ScenarioFamily,
                ["vehicle.platform"] = scenario.VehiclePlatform,
                ["run.status"] = runState.Status,
                ["sensor.gpsHealthy"] = context.GpsHealthy.ToString(),
                ["sensor.imuHealthy"] = context.ImuHealthy.ToString(),
                ["sensor.obstacleHealthy"] = context.ObstacleSensorHealthy.ToString(),
                ["safety.degradedMode"] = context.IsDegradedMode.ToString(),
                ["safety.limiterActive"] = context.SafetyLimiterActive.ToString(),
                ["safety.estop"] = context.EmergencyStopActive.ToString()
            }
        };
    }

    private static List<ScenarioObjectiveJudgeState> BuildObjectiveStates(ScenarioJudgeContext context)
    {
        var scenario = context.Scenario;
        var objectives = scenario.Objectives
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var states = new List<ScenarioObjectiveJudgeState>(objectives.Count);

        foreach (var objective in objectives)
        {
            var target = FindObject(scenario, objective.TargetObjectId);

            double? distance = target is null
                ? null
                : CalculateDistance3D(context.VehicleX, context.VehicleY, context.VehicleZ, target.X, target.Y, target.Z);

            double? horizontalDistance = target is null
                ? null
                : CalculateDistance2D(context.VehicleX, context.VehicleY, target.X, target.Y);

            double? verticalDistance = target is null
                ? null
                : Math.Abs(context.VehicleZ - target.Z);

            var tolerance = ResolveObjectiveTolerance(objective, target);
            var isCompleted = IsObjectiveCompleted(context, objective, target, distance, horizontalDistance, verticalDistance, tolerance);
            var isFailed = IsObjectiveFailed(context, objective, target);

            var activeObjectiveId = context.RunState.CurrentObjectiveId;
            var isActive = !string.IsNullOrWhiteSpace(activeObjectiveId)
                ? string.Equals(activeObjectiveId, objective.Id, StringComparison.OrdinalIgnoreCase)
                : !isCompleted && !isFailed && states.All(x => x.IsCompleted || x.IsFailed);

            states.Add(new ScenarioObjectiveJudgeState
            {
                ObjectiveId = objective.Id,
                ObjectiveType = objective.Type,
                Title = objective.Title,
                Order = objective.Order,
                IsRequired = objective.IsRequired,
                IsCompleted = isCompleted,
                IsFailed = isFailed,
                IsActive = isActive,
                ScoreEarned = isCompleted ? objective.ScoreValue : 0.0,
                PenaltyApplied = isFailed && objective.IsRequired ? objective.ScoreValue : 0.0,
                DistanceToTargetMeters = distance,
                HorizontalDistanceToTargetMeters = horizontalDistance,
                VerticalDistanceToTargetMeters = verticalDistance,
                StartedUtc = isActive ? context.RunState.StartedUtc : null,
                CompletedUtc = isCompleted ? context.TimestampUtc : null,
                FailureReason = isFailed ? "Objective failed by judge rule." : null,
                TargetObjectId = objective.TargetObjectId,
                Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["toleranceMeters"] = tolerance,
                    ["scoreValue"] = objective.ScoreValue
                },
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["objective.type"] = objective.Type,
                    ["target.objectId"] = objective.TargetObjectId ?? string.Empty
                }
            });
        }

        return states;
    }

    private static bool IsObjectiveCompleted(
        ScenarioJudgeContext context,
        ScenarioMissionObjectiveDefinition objective,
        ScenarioWorldObjectDefinition? target,
        double? distance,
        double? horizontalDistance,
        double? verticalDistance,
        double tolerance)
    {
        var type = objective.Type ?? string.Empty;

        if (string.Equals(type, "avoid_zone", StringComparison.OrdinalIgnoreCase))
        {
            return !IsObjectReported(context.ReportedNoGoZoneObjectIds, objective.TargetObjectId);
        }

        if (string.Equals(type, "pass_gate", StringComparison.OrdinalIgnoreCase))
        {
            if (target is null)
            {
                return false;
            }

            var withinGateArea =
                horizontalDistance.HasValue &&
                horizontalDistance.Value <= Math.Max(tolerance, target.Width > 0.0 ? target.Width * 0.5 : tolerance);

            if (!withinGateArea)
            {
                return false;
            }

            if (!target.RequiresDirectionCheck)
            {
                return true;
            }

            var headingError = NormalizeAngleDeg(context.VehicleYawDeg - target.RequiredHeadingDeg);
            return Math.Abs(headingError) <= target.HeadingToleranceDeg;
        }

        if (string.Equals(type, "reach_zone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "reach_target", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "inspect_object", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "hold_position", StringComparison.OrdinalIgnoreCase))
        {
            return distance.HasValue && distance.Value <= tolerance;
        }

        if (string.Equals(type, "depth_reach", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "reach_depth", StringComparison.OrdinalIgnoreCase))
        {
            return verticalDistance.HasValue && verticalDistance.Value <= tolerance;
        }

        return distance.HasValue && distance.Value <= tolerance;
    }

    private static bool IsObjectiveFailed(
        ScenarioJudgeContext context,
        ScenarioMissionObjectiveDefinition objective,
        ScenarioWorldObjectDefinition? target)
    {
        if (objective.HasTimeLimit &&
            context.RunState.ElapsedSeconds > objective.TimeLimitSeconds)
        {
            return true;
        }

        if (string.Equals(objective.Type, "avoid_zone", StringComparison.OrdinalIgnoreCase) &&
            IsObjectReported(context.ReportedNoGoZoneObjectIds, objective.TargetObjectId))
        {
            return true;
        }

        if (target is not null &&
            target.IsNoGoZone &&
            IsObjectReported(context.ReportedNoGoZoneObjectIds, target.Id))
        {
            return true;
        }

        return false;
    }

    private static void ApplyCollisionChecks(
        ScenarioJudgeContext context,
        ScenarioJudgeDefinition judge,
        List<ScenarioJudgeEvent> events,
        List<ScenarioJudgeViolation> violations,
        ref double penalty)
    {
        if (!judge.Enabled || !judge.CheckCollisions)
        {
            return;
        }

        foreach (var objectId in context.ReportedCollisionObjectIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var violation = CreateViolation(
                type: "Collision",
                severity: "Critical",
                message: $"Çarpışma tespit edildi. Object={objectId}",
                penalty: judge.CollisionPenalty,
                context: context,
                objectiveId: context.RunState.CurrentObjectiveId,
                objectId: objectId);

            if (AddUniqueViolation(violations, violation))
            {
                penalty += judge.CollisionPenalty;

                AddUniqueEvent(
                    events,
                    CreateEvent(
                        type: "Collision",
                        severity: "Critical",
                        message: violation.Message,
                        context: context,
                        objectiveId: context.RunState.CurrentObjectiveId,
                        objectId: objectId));
            }
        }
    }

    private static void ApplyNoGoZoneChecks(
        ScenarioJudgeContext context,
        ScenarioJudgeDefinition judge,
        List<ScenarioJudgeEvent> events,
        List<ScenarioJudgeViolation> violations,
        ref double penalty)
    {
        if (!judge.Enabled || !judge.CheckNoGoZones)
        {
            return;
        }

        foreach (var objectId in context.ReportedNoGoZoneObjectIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var violation = CreateViolation(
                type: "NoGoZone",
                severity: "Critical",
                message: $"No-go zone ihlali tespit edildi. Zone={objectId}",
                penalty: judge.NoGoZonePenalty,
                context: context,
                objectiveId: context.RunState.CurrentObjectiveId,
                objectId: objectId);

            if (AddUniqueViolation(violations, violation))
            {
                penalty += judge.NoGoZonePenalty;

                AddUniqueEvent(
                    events,
                    CreateEvent(
                        type: "NoGoZoneViolation",
                        severity: "Critical",
                        message: violation.Message,
                        context: context,
                        objectiveId: context.RunState.CurrentObjectiveId,
                        objectId: objectId));
            }
        }
    }

    private static void ApplyDegradedChecks(
        ScenarioJudgeContext context,
        ScenarioJudgeDefinition judge,
        List<ScenarioJudgeEvent> events,
        ref double penalty)
    {
        var degraded =
            context.IsDegradedMode ||
            !context.GpsHealthy ||
            !context.ImuHealthy ||
            !context.ObstacleSensorHealthy ||
            context.ActiveFaultIds.Count > 0;

        if (!degraded)
        {
            return;
        }

        var message = BuildDegradedMessage(context);

        if (AddUniqueEvent(
                events,
                CreateEvent(
                    type: "DegradedOperation",
                    severity: "Warning",
                    message: message,
                    context: context,
                    objectiveId: context.RunState.CurrentObjectiveId,
                    objectId: null)))
        {
            penalty += judge.DegradedOperationPenalty;
        }
    }

    private static void ApplySafetyChecks(
        ScenarioJudgeContext context,
        List<ScenarioJudgeEvent> events)
    {
        if (context.SafetyLimiterActive)
        {
            AddUniqueEvent(
                events,
                CreateEvent(
                    type: "SafetyLimiterActive",
                    severity: "Info",
                    message: "Safety limiter bu tick içinde müdahale etti.",
                    context: context,
                    objectiveId: context.RunState.CurrentObjectiveId,
                    objectId: null));
        }

        if (context.EmergencyStopActive)
        {
            AddUniqueEvent(
                events,
                CreateEvent(
                    type: "EmergencyStop",
                    severity: "Critical",
                    message: "Emergency stop aktif.",
                    context: context,
                    objectiveId: context.RunState.CurrentObjectiveId,
                    objectId: null));
        }
    }

    private static ScenarioWorldObjectDefinition? FindObject(ScenarioDefinition scenario, string? objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        return scenario.Objects.FirstOrDefault(x =>
            string.Equals(x.Id, objectId, StringComparison.OrdinalIgnoreCase));
    }

    private static double ResolveObjectiveTolerance(
        ScenarioMissionObjectiveDefinition objective,
        ScenarioWorldObjectDefinition? target)
    {
        if (objective.ToleranceMeters > 0.0)
        {
            return objective.ToleranceMeters;
        }

        if (target is not null)
        {
            if (target.ToleranceMeters > 0.0)
            {
                return target.ToleranceMeters;
            }

            if (target.Radius > 0.0)
            {
                return target.Radius;
            }

            if (target.Width > 0.0)
            {
                return target.Width * 0.5;
            }
        }

        return 1.0;
    }

    private static ScenarioJudgeEvent CreateEvent(
        string type,
        string severity,
        string message,
        ScenarioJudgeContext context,
        string? objectiveId,
        string? objectId)
    {
        return new ScenarioJudgeEvent
        {
            TimestampUtc = context.TimestampUtc,
            Type = type,
            Severity = severity,
            ObjectiveId = objectiveId,
            ObjectId = objectId,
            Message = message,
            VehicleX = context.VehicleX,
            VehicleY = context.VehicleY,
            VehicleZ = context.VehicleZ,
            VehicleYawDeg = context.VehicleYawDeg
        };
    }

    private static ScenarioJudgeViolation CreateViolation(
        string type,
        string severity,
        string message,
        double penalty,
        ScenarioJudgeContext context,
        string? objectiveId,
        string? objectId)
    {
        return new ScenarioJudgeViolation
        {
            TimestampUtc = context.TimestampUtc,
            Type = type,
            Severity = severity,
            ObjectiveId = objectiveId,
            ObjectId = objectId,
            PenaltyApplied = penalty,
            Message = message,
            VehicleX = context.VehicleX,
            VehicleY = context.VehicleY,
            VehicleZ = context.VehicleZ,
            VehicleYawDeg = context.VehicleYawDeg
        };
    }

    private static bool AddUniqueEvent(List<ScenarioJudgeEvent> events, ScenarioJudgeEvent candidate)
    {
        var exists = events.Any(x =>
            string.Equals(x.Type, candidate.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ObjectiveId ?? string.Empty, candidate.ObjectiveId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ObjectId ?? string.Empty, candidate.ObjectId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return false;
        }

        events.Add(candidate);
        return true;
    }

    private static bool AddUniqueViolation(List<ScenarioJudgeViolation> violations, ScenarioJudgeViolation candidate)
    {
        var exists = violations.Any(x =>
            string.Equals(x.Type, candidate.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ObjectiveId ?? string.Empty, candidate.ObjectiveId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ObjectId ?? string.Empty, candidate.ObjectId ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return false;
        }

        violations.Add(candidate);
        return true;
    }

    private static string ResolveStatus(
        bool isSuccess,
        bool isFailure,
        bool timedOut,
        ScenarioRunState runState)
    {
        if (timedOut)
        {
            return ScenarioJudgeStatus.Timeout;
        }

        if (isSuccess)
        {
            return ScenarioJudgeStatus.Success;
        }

        if (isFailure)
        {
            return ScenarioJudgeStatus.Failed;
        }

        if (string.Equals(runState.Status, ScenarioRunStatus.NotStarted, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioJudgeStatus.NotStarted;
        }

        if (string.Equals(runState.Status, ScenarioRunStatus.Aborted, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioJudgeStatus.Aborted;
        }

        return ScenarioJudgeStatus.Running;
    }

    private static string? ResolveFailureReason(
        bool isFailure,
        bool timedOut,
        ScenarioJudgeContext context,
        bool hasCollisionFailure,
        bool hasNoGoFailure,
        bool hasRequiredObjectiveFailure,
        IReadOnlyList<ScenarioJudgeViolation> violations)
    {
        if (!isFailure)
        {
            return null;
        }

        if (timedOut)
        {
            return "Scenario time limit exceeded.";
        }

        if (context.EmergencyStopActive)
        {
            return "Emergency stop active.";
        }

        if (hasCollisionFailure)
        {
            return violations.FirstOrDefault(x => string.Equals(x.Type, "Collision", StringComparison.OrdinalIgnoreCase))?.Message
                ?? "Collision detected.";
        }

        if (hasNoGoFailure)
        {
            return violations.FirstOrDefault(x => string.Equals(x.Type, "NoGoZone", StringComparison.OrdinalIgnoreCase))?.Message
                ?? "No-go zone violation detected.";
        }

        if (hasRequiredObjectiveFailure)
        {
            return "Required objective failed.";
        }

        return "Scenario failed.";
    }

    private static string BuildSummary(
        string status,
        int completedObjectiveCount,
        int totalObjectiveCount,
        double score,
        double penalty,
        string? failureReason)
    {
        var baseText =
            $"{status}: objectives={completedObjectiveCount}/{totalObjectiveCount}, " +
            $"score={score:F1}, penalty={penalty:F1}, net={(score - penalty):F1}";

        return string.IsNullOrWhiteSpace(failureReason)
            ? baseText
            : $"{baseText}, failure={failureReason}";
    }

    private static string BuildDegradedMessage(ScenarioJudgeContext context)
    {
        var parts = new List<string>();

        if (context.IsDegradedMode)
        {
            parts.Add("degraded-mode");
        }

        if (!context.GpsHealthy)
        {
            parts.Add("gps-unhealthy");
        }

        if (!context.ImuHealthy)
        {
            parts.Add("imu-unhealthy");
        }

        if (!context.ObstacleSensorHealthy)
        {
            parts.Add("obstacle-sensor-unhealthy");
        }

        if (context.ActiveFaultIds.Count > 0)
        {
            parts.Add("active-faults=" + string.Join(",", context.ActiveFaultIds));
        }

        return "Degraded operation detected: " + string.Join(", ", parts);
    }

    private static int CountDegradedEvents(
        ScenarioJudgeContext context,
        IReadOnlyList<ScenarioJudgeEvent> events)
    {
        var count = events.Count(x => string.Equals(x.Type, "DegradedOperation", StringComparison.OrdinalIgnoreCase));
        return context.ActiveFaultIds.Count > 0 ? Math.Max(count, context.ActiveFaultIds.Count) : count;
    }

    private static int CountSafetyEvents(
        ScenarioJudgeContext context,
        IReadOnlyList<ScenarioJudgeEvent> events)
    {
        var count = events.Count(x =>
            string.Equals(x.Type, "SafetyLimiterActive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Type, "EmergencyStop", StringComparison.OrdinalIgnoreCase));

        if (context.SafetyLimiterActive)
        {
            count = Math.Max(count, 1);
        }

        if (context.EmergencyStopActive)
        {
            count = Math.Max(count, 1);
        }

        return count;
    }

    private static bool IsObjectReported(IReadOnlyList<string> objectIds, string? objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        return objectIds.Any(x => string.Equals(x, objectId, StringComparison.OrdinalIgnoreCase));
    }

    private static double ResolveElapsedSeconds(ScenarioRunState runState, DateTime now)
    {
        if (runState.ElapsedSeconds > 0.0)
        {
            return runState.ElapsedSeconds;
        }

        return runState.StartedUtc.HasValue
            ? Math.Max((now - runState.StartedUtc.Value).TotalSeconds, 0.0)
            : 0.0;
    }

    private static double CalculateDistance2D(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double CalculateDistance3D(double ax, double ay, double az, double bx, double by, double bz)
    {
        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static double NormalizeAngleDeg(double angle)
    {
        while (angle > 180.0)
        {
            angle -= 360.0;
        }

        while (angle < -180.0)
        {
            angle += 360.0;
        }

        return angle;
    }

    private static double Clamp01(double value)
    {
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