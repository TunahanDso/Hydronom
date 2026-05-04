using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Scenarios.Reports;
using Hydronom.Runtime.Scenarios.Mission;
using Hydronom.Runtime.Testing.Scenarios;

namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Scenario mission plan'i gerçek runtime task hattına bağlayan execution host.
///
/// Bu sınıf kinematic executor değildir.
/// Aracı kendi başına hareket ettirmez.
/// Görevi şudur:
///
/// ScenarioMissionPlan
///   → aktif objective
///   → ITaskManager.SetTask(TaskDefinition)
///   → runtime decision/control/actuator/physics loop
///   → VehicleState geri bildirimi
///   → objective tracker
///   → judge/report
///   → sıradaki objective
///
/// Böylece scenario hedefleri gerçek runtime görev/karar hattına girer.
/// </summary>
public sealed class RuntimeScenarioExecutionHost
{
    private readonly ITaskManager _taskManager;
    private readonly ScenarioMissionAdapter _missionAdapter;
    private readonly RuntimeScenarioObjectiveTracker _objectiveTracker;
    private readonly RuntimeScenarioTestRunner _judgeRunner;
    private readonly RuntimeScenarioExecutionOptions _options;

    private long _tickIndex;
    private string? _lastAppliedObjectiveId;
    private bool _hasClearedTerminalTask;

    public RuntimeScenarioExecutionHost(
        RuntimeScenarioSession session,
        ITaskManager taskManager,
        RuntimeScenarioExecutionOptions? options = null,
        ScenarioMissionAdapter? missionAdapter = null,
        RuntimeScenarioObjectiveTracker? objectiveTracker = null,
        RuntimeScenarioTestRunner? judgeRunner = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _options = (options ?? new RuntimeScenarioExecutionOptions()).Sanitized();
        _missionAdapter = missionAdapter ?? new ScenarioMissionAdapter();
        _objectiveTracker = objectiveTracker ?? new RuntimeScenarioObjectiveTracker();
        _judgeRunner = judgeRunner ?? new RuntimeScenarioTestRunner();
    }

    /// <summary>
    /// Yönetilen scenario oturumu.
    /// </summary>
    public RuntimeScenarioSession Session { get; }

    /// <summary>
    /// Oturum başlatılır ve seçenek izin veriyorsa ilk hedef task manager'a basılır.
    /// </summary>
    public RuntimeScenarioTickResult Start(
        VehicleState initialState,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        Session.Start(now);

        var appliedNewTask = false;
        ScenarioMissionTarget? appliedTarget = null;

        if (_options.AutoApplyFirstTarget)
        {
            appliedTarget = Session.CurrentTarget ?? Session.NextTarget;

            if (appliedTarget is not null)
            {
                ApplyTargetIfNeeded(appliedTarget, force: true);
                appliedNewTask = true;
            }
        }

        var report = EvaluateJudge(initialState, now);

        var tick = new RuntimeScenarioTickResult
        {
            ScenarioId = Session.Plan.ScenarioId,
            RunId = Session.RunId,
            TimestampUtc = now,
            TickIndex = _tickIndex,
            SessionState = Session.State,
            PreviousObjectiveId = Session.CurrentObjectiveId,
            CurrentObjectiveId = Session.CurrentObjectiveId,
            CompletedObjectiveId = null,
            AppliedNewTask = appliedNewTask,
            AppliedTarget = appliedTarget,
            VehicleState = initialState.Sanitized(),
            DistanceToCurrentTargetMeters = ComputeDistanceXY(initialState, appliedTarget),
            Distance3DToCurrentTargetMeters = ComputeDistance3D(initialState, appliedTarget),
            ToleranceMeters = appliedTarget?.ToleranceMeters ?? _options.DefaultToleranceMeters,
            InsideTolerance = false,
            SpeedSettled = false,
            YawRateSettled = false,
            SettleSatisfied = false,
            ObjectiveCompleted = false,
            AllObjectivesCompleted = false,
            TimedOut = false,
            Report = report,
            Summary = appliedTarget is null
                ? "Runtime scenario session started without active target."
                : $"Runtime scenario session started. active={appliedTarget.ObjectiveId}"
        };

        Session.AddTick(tick);
        return tick;
    }

    /// <summary>
    /// Runtime loop içinde her tick çağrılır.
    /// Bu metot mevcut VehicleState'e göre aktif objective ilerlemesini değerlendirir,
    /// gerekiyorsa sıradaki objective'i task manager'a yükler ve judge raporunu günceller.
    /// </summary>
    public RuntimeScenarioTickResult Tick(
        VehicleState vehicleState,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var state = vehicleState.Sanitized();

        if (Session.State == RuntimeScenarioSessionState.Created)
        {
            return Start(state, now);
        }

        if (Session.State == RuntimeScenarioSessionState.Paused)
        {
            var pausedTick = BuildPassiveTick(
                state,
                now,
                "Runtime scenario session is paused.");

            Session.AddTick(pausedTick);
            return pausedTick;
        }

        if (Session.IsTerminal)
        {
            ClearTaskIfNeededForTerminalState();

            var terminalTick = BuildPassiveTick(
                state,
                now,
                $"Runtime scenario session is terminal. state={Session.State}");

            Session.AddTick(terminalTick);
            return terminalTick;
        }

        _tickIndex++;

        var timedOut = CheckTimeout(now);

        if (timedOut)
        {
            Session.Timeout(now);
            ClearTaskIfNeededForTerminalState();

            var timeoutReport = EvaluateJudge(state, now);

            var timeoutTick = new RuntimeScenarioTickResult
            {
                ScenarioId = Session.Plan.ScenarioId,
                RunId = Session.RunId,
                TimestampUtc = now,
                TickIndex = _tickIndex,
                SessionState = Session.State,
                PreviousObjectiveId = Session.CurrentObjectiveId,
                CurrentObjectiveId = Session.CurrentObjectiveId,
                CompletedObjectiveId = null,
                AppliedNewTask = false,
                AppliedTarget = null,
                VehicleState = state,
                DistanceToCurrentTargetMeters = ComputeDistanceXY(state, Session.CurrentTarget),
                Distance3DToCurrentTargetMeters = ComputeDistance3D(state, Session.CurrentTarget),
                ToleranceMeters = Session.CurrentTarget?.ToleranceMeters ?? _options.DefaultToleranceMeters,
                InsideTolerance = false,
                SpeedSettled = false,
                YawRateSettled = false,
                SettleSatisfied = false,
                ObjectiveCompleted = false,
                AllObjectivesCompleted = false,
                TimedOut = true,
                Report = timeoutReport,
                Summary = $"Runtime scenario timed out. elapsed={Session.Elapsed.TotalSeconds:F1}s"
            };

            Session.AddTick(timeoutTick);
            return timeoutTick;
        }

        EnsureActiveTargetApplied();

        var trackerResult = _objectiveTracker.Evaluate(
            Session,
            state,
            _options,
            _tickIndex,
            now);

        var report = EvaluateJudge(state, now);

        var appliedNewTask = false;
        ScenarioMissionTarget? appliedTarget = null;

        if (trackerResult.ObjectiveCompleted)
        {
            if (trackerResult.AllObjectivesCompleted || Session.State == RuntimeScenarioSessionState.Completed)
            {
                if (_options.ClearTaskOnCompletion)
                {
                    ClearTaskIfNeededForTerminalState();
                }
            }
            else if (_options.AutoAdvanceObjectives)
            {
                appliedTarget = Session.CurrentTarget ?? Session.NextTarget;

                if (appliedTarget is not null)
                {
                    appliedNewTask = ApplyTargetIfNeeded(appliedTarget, force: true);
                }
            }
        }

        var tick = new RuntimeScenarioTickResult
        {
            ScenarioId = Session.Plan.ScenarioId,
            RunId = Session.RunId,
            TimestampUtc = now,
            TickIndex = _tickIndex,
            SessionState = Session.State,
            PreviousObjectiveId = trackerResult.PreviousObjectiveId,
            CurrentObjectiveId = Session.CurrentObjectiveId,
            CompletedObjectiveId = trackerResult.CompletedObjectiveId,
            AppliedNewTask = appliedNewTask,
            AppliedTarget = appliedTarget,
            VehicleState = state,
            DistanceToCurrentTargetMeters = trackerResult.DistanceToCurrentTargetMeters,
            Distance3DToCurrentTargetMeters = trackerResult.Distance3DToCurrentTargetMeters,
            ToleranceMeters = trackerResult.ToleranceMeters,
            InsideTolerance = trackerResult.InsideTolerance,
            SpeedSettled = trackerResult.SpeedSettled,
            YawRateSettled = trackerResult.YawRateSettled,
            SettleSatisfied = trackerResult.SettleSatisfied,
            ObjectiveCompleted = trackerResult.ObjectiveCompleted,
            AllObjectivesCompleted = trackerResult.AllObjectivesCompleted,
            TimedOut = false,
            Report = report,
            Summary = BuildTickSummary(trackerResult, appliedNewTask, appliedTarget)
        };

        Session.AddTick(tick);
        return tick;
    }

    /// <summary>
    /// Scenario oturumunu dış müdahale ile iptal eder.
    /// </summary>
    public RuntimeScenarioTickResult Abort(
        VehicleState vehicleState,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        Session.Abort(now);
        ClearTaskIfNeededForTerminalState();

        var report = EvaluateJudge(vehicleState, now);

        var tick = new RuntimeScenarioTickResult
        {
            ScenarioId = Session.Plan.ScenarioId,
            RunId = Session.RunId,
            TimestampUtc = now,
            TickIndex = _tickIndex,
            SessionState = Session.State,
            PreviousObjectiveId = Session.CurrentObjectiveId,
            CurrentObjectiveId = Session.CurrentObjectiveId,
            CompletedObjectiveId = null,
            AppliedNewTask = false,
            AppliedTarget = null,
            VehicleState = vehicleState.Sanitized(),
            DistanceToCurrentTargetMeters = ComputeDistanceXY(vehicleState, Session.CurrentTarget),
            Distance3DToCurrentTargetMeters = ComputeDistance3D(vehicleState, Session.CurrentTarget),
            ToleranceMeters = Session.CurrentTarget?.ToleranceMeters ?? _options.DefaultToleranceMeters,
            InsideTolerance = false,
            SpeedSettled = false,
            YawRateSettled = false,
            SettleSatisfied = false,
            ObjectiveCompleted = false,
            AllObjectivesCompleted = false,
            TimedOut = false,
            Report = report,
            Summary = "Runtime scenario aborted."
        };

        Session.AddTick(tick);
        return tick;
    }

    private void EnsureActiveTargetApplied()
    {
        var active = Session.CurrentTarget ?? Session.NextTarget;

        if (active is null)
        {
            return;
        }

        ApplyTargetIfNeeded(active, force: false);
    }

    private bool ApplyTargetIfNeeded(
        ScenarioMissionTarget target,
        bool force)
    {
        if (!force &&
            string.Equals(_lastAppliedObjectiveId, target.ObjectiveId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _missionAdapter.ApplyTarget(target, _taskManager);
        _lastAppliedObjectiveId = target.ObjectiveId;

        return true;
    }

    private ScenarioRunReport? EvaluateJudge(
        VehicleState state,
        DateTime timestampUtc)
    {
        if (!_options.EvaluateJudgeEveryTick)
        {
            return null;
        }

        var scenario = Session.Plan.SourceScenario;

        if (scenario is null)
        {
            return null;
        }

        var runtimeState = ToRuntimeScenarioVehicleState(
            Session.Plan.VehicleId,
            state,
            timestampUtc);

        var currentObjectiveId =
            Session.CurrentObjectiveId ??
            Session.NextTarget?.ObjectiveId ??
            Session.Plan.LastTarget?.ObjectiveId;

        var report = _judgeRunner.RunSingleEvaluation(
            scenario,
            runtimeState,
            new RuntimeScenarioTestOptions
            {
                CurrentObjectiveId = currentObjectiveId,
                StartedUtc = Session.StartedUtc ?? timestampUtc,
                TimestampUtc = timestampUtc
            });

        Session.SetReport(report);
        return report;
    }

    private bool CheckTimeout(DateTime now)
    {
        if (Session.StartedUtc is null)
        {
            return false;
        }

        var maxSeconds = ResolveMaxDurationSeconds();

        if (maxSeconds is null)
        {
            return false;
        }

        return (now - Session.StartedUtc.Value).TotalSeconds >= maxSeconds.Value;
    }

    private double? ResolveMaxDurationSeconds()
    {
        if (_options.MaxDurationSecondsOverride.HasValue)
        {
            return _options.MaxDurationSecondsOverride;
        }

        if (Session.Plan.TimeLimitSeconds.HasValue &&
            Session.Plan.TimeLimitSeconds.Value > 0.0 &&
            double.IsFinite(Session.Plan.TimeLimitSeconds.Value))
        {
            return Session.Plan.TimeLimitSeconds;
        }

        return null;
    }

    private void ClearTaskIfNeededForTerminalState()
    {
        if (_hasClearedTerminalTask)
        {
            return;
        }

        var shouldClear =
            (Session.State == RuntimeScenarioSessionState.Completed && _options.ClearTaskOnCompletion) ||
            (Session.State is RuntimeScenarioSessionState.Failed
                or RuntimeScenarioSessionState.TimedOut
                or RuntimeScenarioSessionState.Aborted && _options.ClearTaskOnStop);

        if (!shouldClear)
        {
            return;
        }

        _taskManager.ClearTask();
        _hasClearedTerminalTask = true;
    }

    private RuntimeScenarioTickResult BuildPassiveTick(
        VehicleState state,
        DateTime now,
        string summary)
    {
        var active = Session.CurrentTarget;

        return new RuntimeScenarioTickResult
        {
            ScenarioId = Session.Plan.ScenarioId,
            RunId = Session.RunId,
            TimestampUtc = now,
            TickIndex = _tickIndex,
            SessionState = Session.State,
            PreviousObjectiveId = Session.CurrentObjectiveId,
            CurrentObjectiveId = Session.CurrentObjectiveId,
            CompletedObjectiveId = null,
            AppliedNewTask = false,
            AppliedTarget = null,
            VehicleState = state.Sanitized(),
            DistanceToCurrentTargetMeters = ComputeDistanceXY(state, active),
            Distance3DToCurrentTargetMeters = ComputeDistance3D(state, active),
            ToleranceMeters = active?.ToleranceMeters ?? _options.DefaultToleranceMeters,
            InsideTolerance = false,
            SpeedSettled = false,
            YawRateSettled = false,
            SettleSatisfied = false,
            ObjectiveCompleted = false,
            AllObjectivesCompleted = Session.CompletedCount >= Session.TotalObjectiveCount,
            TimedOut = Session.State == RuntimeScenarioSessionState.TimedOut,
            Report = Session.LastReport,
            Summary = summary
        };
    }

    private static RuntimeScenarioVehicleState ToRuntimeScenarioVehicleState(
        string vehicleId,
        VehicleState state,
        DateTime timestampUtc)
    {
        var safe = state.Sanitized();

        return new RuntimeScenarioVehicleState
        {
            VehicleId = string.IsNullOrWhiteSpace(vehicleId) ? "hydronom-main" : vehicleId,
            TimestampUtc = timestampUtc,
            X = safe.Position.X,
            Y = safe.Position.Y,
            Z = safe.Position.Z,
            YawDeg = safe.Orientation.YawDeg,
            Vx = safe.LinearVelocity.X,
            Vy = safe.LinearVelocity.Y,
            Vz = safe.LinearVelocity.Z
        };
    }

    private static double ComputeDistanceXY(
        VehicleState state,
        ScenarioMissionTarget? target)
    {
        if (target is null)
        {
            return 0.0;
        }

        var safe = state.Sanitized();

        var dx = safe.Position.X - target.Target.X;
        var dy = safe.Position.Y - target.Target.Y;

        return SafeSqrt(dx * dx + dy * dy);
    }

    private static double ComputeDistance3D(
        VehicleState state,
        ScenarioMissionTarget? target)
    {
        if (target is null)
        {
            return 0.0;
        }

        var safe = state.Sanitized();

        var dx = safe.Position.X - target.Target.X;
        var dy = safe.Position.Y - target.Target.Y;
        var dz = safe.Position.Z - target.Target.Z;

        return SafeSqrt(dx * dx + dy * dy + dz * dz);
    }

    private static double SafeSqrt(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0.0;
        }

        return Math.Sqrt(value);
    }

    private static string BuildTickSummary(
        RuntimeScenarioObjectiveTrackerResult trackerResult,
        bool appliedNewTask,
        ScenarioMissionTarget? appliedTarget)
    {
        var applyText = appliedNewTask && appliedTarget is not null
            ? $", appliedTask={appliedTarget.ObjectiveId}"
            : string.Empty;

        return $"{trackerResult.Summary}{applyText}";
    }
}