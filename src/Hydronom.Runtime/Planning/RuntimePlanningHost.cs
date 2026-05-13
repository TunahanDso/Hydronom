using System;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;
using Hydronom.Core.Planning.Planners;
using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Planning;

/// <summary>
/// Runtime tarafındaki planning orchestration host'u.
/// 
/// Bu sınıf runtime'a bağımlıdır; Core planner'lar ise bağımsız kalır.
/// Görevi:
/// - CurrentTask'i PlanningGoal'a çevirmek
/// - RuntimeWorldModel snapshot'ını PlanningContext'e taşımak
/// - GlobalPlanner + LocalPlanner + TrajectoryGenerator zincirini çalıştırmak
/// - RuntimePlanningSnapshot üretmek
/// </summary>
public sealed class RuntimePlanningHost
{
    private readonly IConfiguration _config;
    private readonly ITaskManager _taskManager;
    private readonly RuntimeWorldModel _worldModel;

    private readonly IGlobalPlanner _globalPlanner;
    private readonly ILocalPlanner _localPlanner;
    private readonly ITrajectoryGenerator _trajectoryGenerator;

    public RuntimePlanningHost(
        IConfiguration config,
        ITaskManager taskManager,
        RuntimeWorldModel worldModel,
        IGlobalPlanner? globalPlanner = null,
        ILocalPlanner? localPlanner = null,
        ITrajectoryGenerator? trajectoryGenerator = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _worldModel = worldModel ?? throw new ArgumentNullException(nameof(worldModel));

        _globalPlanner = globalPlanner ?? new DirectGlobalPlanner();
        _localPlanner = localPlanner ?? new CorridorLocalPlanner();
        _trajectoryGenerator = trajectoryGenerator ?? new SimpleTrajectoryGenerator();
    }

    public RuntimePlanningSnapshot Tick(
        VehicleState state,
        DateTime timestampUtc)
    {
        var task = _taskManager.CurrentTask;

        if (task is null || !task.HasTarget)
        {
            return RuntimePlanningSnapshot.Empty with
            {
                TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc,
                Summary = "NO_ACTIVE_TASK"
            };
        }

        var context = BuildContext(
            task,
            state,
            timestampUtc);

        if (context.Goal.PreferredMode == PlanningMode.Idle)
        {
            return RuntimePlanningSnapshot.Empty with
            {
                Context = context,
                TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc,
                Summary = "IDLE_GOAL"
            };
        }

        var globalPath = _globalPlanner.PlanGlobal(context).Sanitized();
        var localPath = _localPlanner.RefineLocal(context, globalPath).Sanitized();
        var trajectory = _trajectoryGenerator.GenerateTrajectory(context, localPath).Sanitized();

        return new RuntimePlanningSnapshot
        {
            Context = context,
            GlobalPath = globalPath,
            LocalPath = localPath,
            Trajectory = trajectory,
            TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc,
            HasPlan = trajectory.IsValid,
            IsValid = trajectory.IsValid,
            RequiresReplan = trajectory.RequiresReplan,
            RequiresSlowMode = trajectory.RequiresSlowMode,
            Summary =
                $"PLANNING_OK mode={trajectory.Mode} " +
                $"globalPts={globalPath.Points.Count} " +
                $"localPts={localPath.Points.Count} " +
                $"trajPts={trajectory.Points.Count} " +
                $"risk={trajectory.Risk.RiskScore:F2} " +
                $"lookahead={trajectory.LookAheadPoint?.Id ?? "none"}"
        }.Sanitized();
    }

    private PlanningContext BuildContext(
        TaskDefinition task,
        VehicleState state,
        DateTime timestampUtc)
    {
        var worldObjects = _worldModel.ActiveObjects();
        var diagnostics = _worldModel.GetDiagnostics();

        return new PlanningContext
        {
            TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc,
            VehicleId = ReadString("Runtime:TelemetrySummary:VehicleId", "hydronom-main"),
            VehicleState = state.Sanitized(),
            Goal = BuildGoal(task),
            WorldObjects = worldObjects,
            Diagnostics = diagnostics,
            LookAheadMeters = ReadDouble("Planning:LookAheadMeters", 8.0),
            SafetyMarginMeters = ReadDouble("Planning:SafetyMarginMeters", 1.25),
            VehicleRadiusMeters = ReadDouble("Planning:VehicleRadiusMeters", 0.75),
            MaxPlanSpeedMps = ReadDouble("Planning:MaxPlanSpeedMps", 1.6),
            MaxTurnRateDegPerSec = ReadDouble("Planning:MaxTurnRateDegPerSec", 90.0),
            Source = "runtime"
        }.Sanitized();
    }

    private PlanningGoal BuildGoal(TaskDefinition task)
    {
        var target = ResolveTaskTarget(task);

        if (target is null)
            return PlanningGoal.Idle;

        var preferredMode = ResolvePreferredMode(task);

        return new PlanningGoal
        {
            GoalId = ResolveGoalId(task),
            DisplayName = string.IsNullOrWhiteSpace(task.Name) ? "Runtime Task" : task.Name.Trim(),
            PreferredMode = preferredMode,
            TargetPosition = target.Value,
            AcceptanceRadiusMeters = ReadDouble("Planning:DefaultAcceptanceRadiusMeters", 1.0),
            DesiredCruiseSpeedMps = ReadDouble("Planning:DesiredCruiseSpeedMps", 1.2),
            DesiredArrivalSpeedMps = ReadDouble("Planning:DesiredArrivalSpeedMps", 0.35),
            PreferredHeadingDeg = double.NaN,
            RequiresHeadingAlignment = true,
            AllowReverse = preferredMode is PlanningMode.Arrival or PlanningMode.Hold,
            Required = true,
            Priority = 0,
            Source = task.IsExternallyCompleted ? "scenario" : "task",
            Reason = task.IsExternallyCompleted
                ? $"SCENARIO_OBJECTIVE:{task.ExternalObjectiveId ?? "unknown"}"
                : "TASK_TARGET"
        }.Sanitized();
    }

    private static Vec3? ResolveTaskTarget(TaskDefinition task)
    {
        if (task.Target is Vec3 directTarget)
            return directTarget;

        if (task.Waypoints.Count > 0)
            return task.Waypoints[0];

        return null;
    }

    private static string ResolveGoalId(TaskDefinition task)
    {
        if (!string.IsNullOrWhiteSpace(task.ExternalObjectiveId))
            return task.ExternalObjectiveId.Trim();

        if (!string.IsNullOrWhiteSpace(task.Name))
            return task.Name.Trim();

        return "runtime-goal";
    }

    private static PlanningMode ResolvePreferredMode(TaskDefinition task)
    {
        var name = task.Name ?? string.Empty;
        var objective = task.ExternalObjectiveId ?? string.Empty;

        if (name.Contains("hold", StringComparison.OrdinalIgnoreCase))
            return PlanningMode.Hold;

        if (objective.Contains("gate", StringComparison.OrdinalIgnoreCase) ||
            objective.Contains("corridor", StringComparison.OrdinalIgnoreCase) ||
            objective.Contains("slalom", StringComparison.OrdinalIgnoreCase))
            return PlanningMode.Corridor;

        if (objective.Contains("finish", StringComparison.OrdinalIgnoreCase))
            return PlanningMode.Arrival;

        return PlanningMode.Navigate;
    }

    private string ReadString(string key, string fallback)
    {
        var raw = _config[key];

        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : raw.Trim();
    }

    private double ReadDouble(string key, double fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return double.TryParse(
            raw,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }
}