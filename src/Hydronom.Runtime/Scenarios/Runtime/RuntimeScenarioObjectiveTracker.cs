using Hydronom.Core.Domain;
using Hydronom.Runtime.Scenarios.Mission;

namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Runtime scenario objective ilerlemesini takip eder.
///
/// v2:
/// Objective completion artık sadece mesafeye bakmaz.
/// Completion için:
/// - hedef toleransı içinde olmak
/// - hızın düşmüş olması
/// - yaw rate'in sakinleşmiş olması
/// - heading error'ın kabul edilebilir olması
/// - bu koşulların belirli süre birlikte korunması
/// gerekir.
/// </summary>
public sealed class RuntimeScenarioObjectiveTracker
{
    private const double DefaultMaxArrivalHeadingErrorDeg = 18.0;

    private string? _settleObjectiveId;
    private DateTime? _settleSinceUtc;

    public RuntimeScenarioObjectiveTrackerResult Evaluate(
        RuntimeScenarioSession session,
        VehicleState vehicleState,
        RuntimeScenarioExecutionOptions options,
        long tickIndex,
        DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);

        var safeOptions = options.Sanitized();
        var now = utcNow ?? DateTime.UtcNow;

        var previousObjectiveId = session.CurrentObjectiveId;
        var currentTarget = session.CurrentTarget ?? session.NextTarget;

        if (currentTarget is null)
        {
            var allDone = session.CompletedCount >= session.TotalObjectiveCount &&
                          session.TotalObjectiveCount > 0;

            if (allDone && session.State == RuntimeScenarioSessionState.Running)
            {
                session.Complete(now);
            }

            ResetSettleState();

            return new RuntimeScenarioObjectiveTrackerResult
            {
                ScenarioId = session.Plan.ScenarioId,
                RunId = session.RunId,
                TimestampUtc = now,
                TickIndex = tickIndex,
                PreviousObjectiveId = previousObjectiveId,
                CurrentObjectiveId = session.CurrentObjectiveId,
                CurrentTarget = null,
                CompletedObjectiveId = null,
                DistanceToCurrentTargetMeters = 0.0,
                Distance3DToCurrentTargetMeters = 0.0,
                ToleranceMeters = safeOptions.DefaultToleranceMeters,
                InsideTolerance = false,
                SpeedSettled = true,
                YawRateSettled = true,
                HeadingErrorDeg = 0.0,
                HeadingSettled = true,
                SettleElapsedSeconds = 0.0,
                SettleSatisfied = true,
                ObjectiveCompleted = false,
                AllObjectivesCompleted = allDone,
                SessionState = session.State,
                Summary = allDone
                    ? "Runtime scenario objectives already completed."
                    : "Runtime scenario has no active target."
            };
        }

        if (!string.Equals(previousObjectiveId, currentTarget.ObjectiveId, StringComparison.OrdinalIgnoreCase))
        {
            session.SetCurrentObjective(currentTarget.ObjectiveId);
            previousObjectiveId = currentTarget.ObjectiveId;
            ResetSettleState();
        }

        var state = vehicleState.Sanitized();

        var distanceXY = ComputeDistanceXY(state.Position, currentTarget.Target);
        var distance3D = ComputeDistance3D(state.Position, currentTarget.Target);

        var tolerance = ResolveTolerance(currentTarget, safeOptions);

        var insideTolerance = distance3D <= tolerance;

        var speed = ComputeSpeed(state.LinearVelocity);
        var yawRateAbs = Math.Abs(Safe(state.AngularVelocity.Z));
        var headingErrorDeg = ComputeHeadingErrorDeg(state, currentTarget.Target);
        var headingErrorAbs = Math.Abs(headingErrorDeg);

        var speedSettled =
            safeOptions.MaxArrivalSpeedMps <= 0.0 ||
            speed <= safeOptions.MaxArrivalSpeedMps;

        var yawRateSettled =
            safeOptions.MaxArrivalYawRateDegPerSec <= 0.0 ||
            yawRateAbs <= safeOptions.MaxArrivalYawRateDegPerSec;

        var headingSettled =
            headingErrorAbs <= DefaultMaxArrivalHeadingErrorDeg ||
            distance3D <= Math.Max(0.35, tolerance * 0.55);

        var allSettleInputsSatisfied =
            insideTolerance &&
            speedSettled &&
            yawRateSettled &&
            headingSettled;

        UpdateSettleState(
            objectiveId: currentTarget.ObjectiveId,
            allSettleInputsSatisfied: allSettleInputsSatisfied,
            now: now);

        var settleElapsed = ComputeSettleElapsed(now);
        var settleSatisfied = IsSettleSatisfied(safeOptions, settleElapsed);

        var objectiveCompleted =
            safeOptions.UseDistanceTrackerForAdvance &&
            allSettleInputsSatisfied &&
            settleSatisfied;

        string? completedObjectiveId = null;
        ScenarioMissionTarget? nextTarget = null;

        if (objectiveCompleted)
        {
            completedObjectiveId = currentTarget.ObjectiveId;
            session.MarkObjectiveCompleted(currentTarget.ObjectiveId);

            nextTarget = session.NextTarget;

            if (nextTarget is null)
            {
                session.SetCurrentObjective(null);
                session.Complete(now);
                ResetSettleState();
            }
            else
            {
                session.SetCurrentObjective(nextTarget.ObjectiveId);
                ResetSettleState();
            }
        }

        var allObjectivesCompleted =
            session.CompletedCount >= session.TotalObjectiveCount &&
            session.TotalObjectiveCount > 0;

        return new RuntimeScenarioObjectiveTrackerResult
        {
            ScenarioId = session.Plan.ScenarioId,
            RunId = session.RunId,
            TimestampUtc = now,
            TickIndex = tickIndex,
            PreviousObjectiveId = previousObjectiveId,
            CurrentObjectiveId = session.CurrentObjectiveId,
            CurrentTarget = objectiveCompleted ? nextTarget : currentTarget,
            CompletedObjectiveId = completedObjectiveId,
            DistanceToCurrentTargetMeters = distanceXY,
            Distance3DToCurrentTargetMeters = distance3D,
            ToleranceMeters = tolerance,
            InsideTolerance = insideTolerance,
            SpeedSettled = speedSettled,
            YawRateSettled = yawRateSettled,
            HeadingErrorDeg = headingErrorDeg,
            HeadingSettled = headingSettled,
            SettleElapsedSeconds = settleElapsed,
            SettleSatisfied = settleSatisfied,
            ObjectiveCompleted = objectiveCompleted,
            AllObjectivesCompleted = allObjectivesCompleted,
            SessionState = session.State,
            Summary = BuildSummary(
                session,
                currentTarget,
                distance3D,
                tolerance,
                insideTolerance,
                speed,
                yawRateAbs,
                headingErrorDeg,
                headingSettled,
                settleElapsed,
                settleSatisfied,
                objectiveCompleted,
                completedObjectiveId)
        };
    }

    public void Reset()
    {
        ResetSettleState();
    }

    private void UpdateSettleState(
        string objectiveId,
        bool allSettleInputsSatisfied,
        DateTime now)
    {
        if (!allSettleInputsSatisfied)
        {
            ResetSettleState();
            return;
        }

        if (!string.Equals(_settleObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase))
        {
            _settleObjectiveId = objectiveId;
            _settleSinceUtc = now;
            return;
        }

        _settleSinceUtc ??= now;
    }

    private double ComputeSettleElapsed(DateTime now)
    {
        if (_settleSinceUtc is null)
        {
            return 0.0;
        }

        var elapsed = (now - _settleSinceUtc.Value).TotalSeconds;

        if (!double.IsFinite(elapsed) || elapsed < 0.0)
        {
            return 0.0;
        }

        return elapsed;
    }

    private static bool IsSettleSatisfied(
        RuntimeScenarioExecutionOptions options,
        double settleElapsedSeconds)
    {
        var safeOptions = options.Sanitized();

        if (safeOptions.SettleSeconds <= 0.0)
        {
            return true;
        }

        return settleElapsedSeconds >= safeOptions.SettleSeconds;
    }

    private void ResetSettleState()
    {
        _settleObjectiveId = null;
        _settleSinceUtc = null;
    }

    private static double ResolveTolerance(
        ScenarioMissionTarget target,
        RuntimeScenarioExecutionOptions options)
    {
        if (target.ToleranceMeters > 0.0 && double.IsFinite(target.ToleranceMeters))
        {
            return target.ToleranceMeters;
        }

        var safeOptions = options.Sanitized();

        return safeOptions.DefaultToleranceMeters;
    }

    private static double ComputeDistanceXY(Vec3 a, Vec3 b)
    {
        var dx = Safe(a.X) - Safe(b.X);
        var dy = Safe(a.Y) - Safe(b.Y);

        return SafeSqrt(dx * dx + dy * dy);
    }

    private static double ComputeDistance3D(Vec3 a, Vec3 b)
    {
        var dx = Safe(a.X) - Safe(b.X);
        var dy = Safe(a.Y) - Safe(b.Y);
        var dz = Safe(a.Z) - Safe(b.Z);

        return SafeSqrt(dx * dx + dy * dy + dz * dz);
    }

    private static double ComputeSpeed(Vec3 velocity)
    {
        var vx = Safe(velocity.X);
        var vy = Safe(velocity.Y);
        var vz = Safe(velocity.Z);

        return SafeSqrt(vx * vx + vy * vy + vz * vz);
    }

    private static double ComputeHeadingErrorDeg(
        VehicleState state,
        Vec3 target)
    {
        var dx = Safe(target.X) - Safe(state.Position.X);
        var dy = Safe(target.Y) - Safe(state.Position.Y);

        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
        {
            return 0.0;
        }

        var targetHeadingDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        return NormalizeDeg(targetHeadingDeg - Safe(state.Orientation.YawDeg));
    }

    private static double SafeSqrt(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0.0;
        }

        return Math.Sqrt(value);
    }

    private static double Safe(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }

    private static double NormalizeDeg(double deg)
    {
        if (!double.IsFinite(deg))
        {
            return 0.0;
        }

        deg %= 360.0;

        if (deg > 180.0)
        {
            deg -= 360.0;
        }

        if (deg < -180.0)
        {
            deg += 360.0;
        }

        return deg;
    }

    private static string BuildSummary(
        RuntimeScenarioSession session,
        ScenarioMissionTarget currentTarget,
        double distance3D,
        double tolerance,
        bool insideTolerance,
        double speed,
        double yawRateAbs,
        double headingErrorDeg,
        bool headingSettled,
        double settleElapsedSeconds,
        bool settleSatisfied,
        bool objectiveCompleted,
        string? completedObjectiveId)
    {
        if (objectiveCompleted)
        {
            return
                $"Objective completed: {completedObjectiveId}, " +
                $"next={session.CurrentObjectiveId ?? "none"}, " +
                $"completed={session.CompletedCount}/{session.TotalObjectiveCount}, " +
                $"settle={settleElapsedSeconds:F2}s";
        }

        return
            $"Tracking objective={currentTarget.ObjectiveId}, " +
            $"distance3D={distance3D:F2}m, tolerance={tolerance:F2}m, " +
            $"inside={insideTolerance}, speed={speed:F2}m/s, yawRate={yawRateAbs:F1}deg/s, " +
            $"headingErr={headingErrorDeg:F1}deg, headingSettled={headingSettled}, " +
            $"settle={settleElapsedSeconds:F2}s, settleOk={settleSatisfied}, " +
            $"completed={session.CompletedCount}/{session.TotalObjectiveCount}";
    }
}

public sealed record RuntimeScenarioObjectiveTrackerResult
{
    public string ScenarioId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public long TickIndex { get; init; }

    public string? PreviousObjectiveId { get; init; }

    public string? CurrentObjectiveId { get; init; }

    public ScenarioMissionTarget? CurrentTarget { get; init; }

    public string? CompletedObjectiveId { get; init; }

    public double DistanceToCurrentTargetMeters { get; init; }

    public double Distance3DToCurrentTargetMeters { get; init; }

    public double ToleranceMeters { get; init; }

    public bool InsideTolerance { get; init; }

    public bool SpeedSettled { get; init; }

    public bool YawRateSettled { get; init; }

    public double HeadingErrorDeg { get; init; }

    public bool HeadingSettled { get; init; }

    public double SettleElapsedSeconds { get; init; }

    public bool SettleSatisfied { get; init; }

    public bool ObjectiveCompleted { get; init; }

    public bool AllObjectivesCompleted { get; init; }

    public RuntimeScenarioSessionState SessionState { get; init; }

    public string Summary { get; init; } = string.Empty;
}