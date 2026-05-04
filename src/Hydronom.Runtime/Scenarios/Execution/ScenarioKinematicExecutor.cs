using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.Scenarios.Runtime;
using Hydronom.Runtime.Testing.Scenarios;

namespace Hydronom.Runtime.Scenarios.Execution;

/// <summary>
/// ScenarioDefinition içindeki objective'leri basit kinematic hareket modeliyle icra eden ilk executor.
/// Bu sınıf gerçek actuator/fusion/physics zinciri değildir.
/// Ama Digital Proving Ground için "senaryoyu kendi timeline'ını üreterek koşturma" temelini sağlar.
/// </summary>
public sealed class ScenarioKinematicExecutor
{
    private readonly RuntimeScenarioTestRunner _runner;

    public ScenarioKinematicExecutor()
        : this(new RuntimeScenarioTestRunner())
    {
    }

    public ScenarioKinematicExecutor(RuntimeScenarioTestRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <summary>
    /// Verilen senaryoyu basit kinematic modelle koşturur ve final rapor üretir.
    /// </summary>
    public ScenarioExecutionResult Execute(
        ScenarioDefinition scenario,
        ScenarioExecutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var safeOptions = (options ?? new ScenarioExecutionOptions()).Sanitized();

        var startedUtc = safeOptions.StartedUtc ?? DateTime.UtcNow;
        var maxDurationSeconds = ResolveMaxDurationSeconds(scenario, safeOptions);
        var maxTicks = Math.Max(1, (int)Math.Ceiling(maxDurationSeconds / safeOptions.DtSeconds));

        var timeline = new List<RuntimeScenarioVehicleState>();
        var storedTimeline = new List<RuntimeScenarioVehicleState>();

        var current = CreateInitialVehicleState(scenario, startedUtc);
        var activeObjective = ResolveFirstObjective(scenario);

        timeline.Add(current);
        AddStoredTimelineSample(storedTimeline, current, safeOptions);

        var completedObjectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tickCount = 0;
        var simulatedElapsed = 0.0;

        for (var tick = 0; tick < maxTicks; tick++)
        {
            tickCount = tick + 1;
            simulatedElapsed = tick * safeOptions.DtSeconds;

            if (activeObjective is null)
            {
                break;
            }

            var target = FindTargetObject(scenario, activeObjective);
            if (target is null)
            {
                break;
            }

            current = StepTowardsTarget(
                current,
                target,
                safeOptions,
                startedUtc.AddSeconds(simulatedElapsed + safeOptions.DtSeconds));

            timeline.Add(current);
            AddStoredTimelineSample(storedTimeline, current, safeOptions);

            if (IsObjectiveReached(current, activeObjective, target, safeOptions))
            {
                completedObjectiveIds.Add(activeObjective.Id);
                activeObjective = ResolveNextObjective(scenario, completedObjectiveIds);
            }

            if (completedObjectiveIds.Count >= scenario.Objectives.Count && scenario.Objectives.Count > 0)
            {
                break;
            }
        }

        var report = _runner.RunTimelineEvaluation(
            scenario,
            timeline,
            new RuntimeScenarioTestOptions
            {
                RunId = safeOptions.RunId,
                StartedUtc = startedUtc,
                TimestampUtc = startedUtc.AddSeconds(simulatedElapsed),
                CurrentObjectiveId = ResolveFirstObjective(scenario)?.Id,
                VehicleRadiusMeters = safeOptions.VehicleRadiusMeters,
                VehicleVerticalToleranceMeters = safeOptions.VehicleVerticalToleranceMeters,
                StateConfidence = safeOptions.StateConfidence,
                FusionConfidence = safeOptions.FusionConfidence,
                GpsHealthy = safeOptions.GpsHealthy,
                ImuHealthy = safeOptions.ImuHealthy,
                ObstacleSensorHealthy = safeOptions.ObstacleSensorHealthy,
                IsDegradedMode = safeOptions.IsDegradedMode,
                SafetyLimiterActive = safeOptions.SafetyLimiterActive,
                EmergencyStopActive = safeOptions.EmergencyStopActive
            });

        var isTimedOut =
            !report.IsSuccess &&
            simulatedElapsed >= maxDurationSeconds;

        var finalStatus = isTimedOut
            ? ScenarioRunStatus.Timeout
            : report.FinalStatus;

        return new ScenarioExecutionResult
        {
            ScenarioId = scenario.Id,
            RunId = report.RunId,
            StartedUtc = startedUtc,
            FinishedUtc = DateTime.UtcNow,
            SimulatedElapsedSeconds = simulatedElapsed,
            TickCount = tickCount,
            FinalStatus = finalStatus,
            IsSuccess = report.IsSuccess,
            IsFailure = report.IsFailure || isTimedOut,
            IsTimedOut = isTimedOut,
            IsAborted = report.IsAborted,
            Report = report,
            Timeline = safeOptions.KeepTimelineSamples
                ? storedTimeline
                : Array.Empty<RuntimeScenarioVehicleState>(),
            Summary = BuildSummary(finalStatus, report, simulatedElapsed, tickCount),
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["executor.simulatedElapsedSeconds"] = simulatedElapsed,
                ["executor.tickCount"] = tickCount,
                ["executor.dtSeconds"] = safeOptions.DtSeconds,
                ["executor.cruiseSpeedMps"] = safeOptions.CruiseSpeedMetersPerSecond,
                ["executor.verticalSpeedMps"] = safeOptions.VerticalSpeedMetersPerSecond,
                ["report.score"] = report.Score,
                ["report.penalty"] = report.Penalty,
                ["report.netScore"] = report.NetScore,
                ["report.completionRatio"] = report.CompletionRatio,
                ["timeline.sampleCount"] = storedTimeline.Count
            },
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["executor"] = nameof(ScenarioKinematicExecutor),
                ["scenario.id"] = scenario.Id,
                ["scenario.family"] = scenario.ScenarioFamily,
                ["vehicle.platform"] = scenario.VehiclePlatform,
                ["report.finalStatus"] = report.FinalStatus,
                ["report.judgeStatus"] = report.JudgeStatus
            }
        };
    }

    private static RuntimeScenarioVehicleState CreateInitialVehicleState(
        ScenarioDefinition scenario,
        DateTime timestampUtc)
    {
        return new RuntimeScenarioVehicleState
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = timestampUtc,
            X = scenario.StartX,
            Y = scenario.StartY,
            Z = scenario.StartZ,
            RollDeg = scenario.StartRollDeg,
            PitchDeg = scenario.StartPitchDeg,
            YawDeg = scenario.StartYawDeg,
            Vx = 0.0,
            Vy = 0.0,
            Vz = 0.0,
            YawRateDegPerSecond = 0.0
        };
    }

    private static RuntimeScenarioVehicleState StepTowardsTarget(
        RuntimeScenarioVehicleState current,
        ScenarioWorldObjectDefinition target,
        ScenarioExecutionOptions options,
        DateTime timestampUtc)
    {
        var dx = target.X - current.X;
        var dy = target.Y - current.Y;
        var dz = target.Z - current.Z;

        var horizontalDistance = Math.Sqrt((dx * dx) + (dy * dy));
        var verticalDistance = Math.Abs(dz);

        var horizontalStep = Math.Min(horizontalDistance, options.CruiseSpeedMetersPerSecond * options.DtSeconds);
        var verticalStep = Math.Min(verticalDistance, options.VerticalSpeedMetersPerSecond * options.DtSeconds);

        var nx = horizontalDistance > 0.000001 ? dx / horizontalDistance : 0.0;
        var ny = horizontalDistance > 0.000001 ? dy / horizontalDistance : 0.0;
        var nz = dz > 0.000001 ? 1.0 : dz < -0.000001 ? -1.0 : 0.0;

        var newX = current.X + (nx * horizontalStep);
        var newY = current.Y + (ny * horizontalStep);
        var newZ = current.Z + (nz * verticalStep);

        var vx = options.DtSeconds > 0.0 ? (newX - current.X) / options.DtSeconds : 0.0;
        var vy = options.DtSeconds > 0.0 ? (newY - current.Y) / options.DtSeconds : 0.0;
        var vz = options.DtSeconds > 0.0 ? (newZ - current.Z) / options.DtSeconds : 0.0;

        var yaw = horizontalDistance > 0.000001
            ? Math.Atan2(dy, dx) * 180.0 / Math.PI
            : current.YawDeg;

        var yawRate = options.DtSeconds > 0.0
            ? NormalizeAngleDeg(yaw - current.YawDeg) / options.DtSeconds
            : 0.0;

        return current with
        {
            TimestampUtc = timestampUtc,
            X = newX,
            Y = newY,
            Z = newZ,
            YawDeg = NormalizeAngleDeg(yaw),
            Vx = vx,
            Vy = vy,
            Vz = vz,
            YawRateDegPerSecond = yawRate
        };
    }

    private static bool IsObjectiveReached(
        RuntimeScenarioVehicleState state,
        ScenarioMissionObjectiveDefinition objective,
        ScenarioWorldObjectDefinition target,
        ScenarioExecutionOptions options)
    {
        var tolerance = ResolveTolerance(objective, target, options);

        var dx = state.X - target.X;
        var dy = state.Y - target.Y;
        var dz = state.Z - target.Z;

        var distance3d = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

        return distance3d <= tolerance;
    }

    private static double ResolveTolerance(
        ScenarioMissionObjectiveDefinition objective,
        ScenarioWorldObjectDefinition target,
        ScenarioExecutionOptions options)
    {
        if (objective.ToleranceMeters > 0.0)
        {
            return objective.ToleranceMeters;
        }

        if (target.ToleranceMeters > 0.0)
        {
            return target.ToleranceMeters;
        }

        if (target.Radius > 0.0)
        {
            return target.Radius;
        }

        return options.DefaultToleranceMeters;
    }

    private static ScenarioMissionObjectiveDefinition? ResolveFirstObjective(ScenarioDefinition scenario)
    {
        return scenario.Objectives
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static ScenarioMissionObjectiveDefinition? ResolveNextObjective(
        ScenarioDefinition scenario,
        HashSet<string> completedObjectiveIds)
    {
        return scenario.Objectives
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => !completedObjectiveIds.Contains(x.Id));
    }

    private static ScenarioWorldObjectDefinition? FindTargetObject(
        ScenarioDefinition scenario,
        ScenarioMissionObjectiveDefinition objective)
    {
        if (string.IsNullOrWhiteSpace(objective.TargetObjectId))
        {
            return null;
        }

        return scenario.Objects.FirstOrDefault(x =>
            string.Equals(x.Id, objective.TargetObjectId, StringComparison.OrdinalIgnoreCase));
    }

    private static double ResolveMaxDurationSeconds(
        ScenarioDefinition scenario,
        ScenarioExecutionOptions options)
    {
        if (scenario.HasTimeLimit)
        {
            return Math.Min(scenario.TimeLimitSeconds, options.MaxDurationSeconds);
        }

        return options.MaxDurationSeconds;
    }

    private static void AddStoredTimelineSample(
        List<RuntimeScenarioVehicleState> storedTimeline,
        RuntimeScenarioVehicleState state,
        ScenarioExecutionOptions options)
    {
        if (!options.KeepTimelineSamples)
        {
            return;
        }

        if (storedTimeline.Count >= options.MaxStoredTimelineSamples)
        {
            return;
        }

        storedTimeline.Add(state);
    }

    private static string BuildSummary(
        string finalStatus,
        Hydronom.Core.Scenarios.Reports.ScenarioRunReport report,
        double simulatedElapsed,
        int tickCount)
    {
        return
            $"{finalStatus}: objectives={report.CompletedObjectiveCount}/{report.TotalObjectiveCount}, " +
            $"score={report.Score:F1}, net={report.NetScore:F1}, " +
            $"elapsed={simulatedElapsed:F2}s, ticks={tickCount}";
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
}