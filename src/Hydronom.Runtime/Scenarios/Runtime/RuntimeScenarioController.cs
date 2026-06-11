
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Runtime.Scenarios.Mission;
using Hydronom.Runtime.Vehicles;
using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed partial class RuntimeScenarioController
{
    private const string DefaultRuntimeVehicleId = "hydronom-main";

    private readonly IConfiguration _config;
    private readonly ITaskManager _taskManager;
    private readonly RuntimeWorldModel? _runtimeWorld;
    private readonly ActiveVehicleContext? _activeVehicleContext;
    private readonly object _gate = new();

    private RuntimeScenarioExecutionHost? _host;
    private RuntimeScenarioSession? _session;
    private ScenarioDefinition? _scenario;
    private ScenarioMissionPlan? _plan;
    private ScenarioMissionAdapter? _adapter;
    private RuntimeScenarioTickResult? _lastTick;
    private VehicleState _lastState = VehicleState.Zero;

    private string? _lastReassertedObjectiveId;
    private long _lastReassertedTickIndex = -1;

    public RuntimeScenarioController(
        IConfiguration config,
        ITaskManager taskManager,
        RuntimeWorldModel? runtimeWorld = null,
        ActiveVehicleContext? activeVehicleContext = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _runtimeWorld = runtimeWorld;
        _activeVehicleContext = activeVehicleContext;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _session?.State == RuntimeScenarioSessionState.Running ||
                       _session?.State == RuntimeScenarioSessionState.Paused;
            }
        }
    }

    public async Task<RuntimeScenarioSnapshot> AutoStartFromConfigAsync(
        VehicleState initialState,
        CancellationToken cancellationToken = default)
    {
        _lastState = initialState;

        if (!ReadBool("ScenarioRuntime:Enabled", false))
            return GetSnapshot("Scenario runtime auto-start disabled.");

        var scenarioPath = ResolveScenarioPath(null);

        return await StartScenarioAsync(
            scenarioPath,
            "config",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<RuntimeScenarioSnapshot> StartScenarioAsync(
        string? scenarioPath = null,
        string requestedBy = "command",
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolveScenarioPath(scenarioPath);

        if (!ScenarioPathExists(resolvedPath))
            return GetSnapshot($"Scenario file/package not found: {resolvedPath}");

        var loader = new ScenarioLoader();
        var scenario = await loader.LoadAsync(resolvedPath).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var adapter = new ScenarioMissionAdapter();
        var plan = adapter.BuildPlan(scenario);

        if (!plan.HasTargets)
            return GetSnapshot($"Scenario has no mission targets: {scenario.Id}");

        var options = ReadOptions();
        var session = new RuntimeScenarioSession(plan);

        var host = new RuntimeScenarioExecutionHost(
            session,
            _taskManager,
            options,
            adapter);

        RuntimeScenarioTickResult startResult;

        lock (_gate)
        {
            if (_session?.State == RuntimeScenarioSessionState.Running)
                return GetSnapshotUnsafe("Scenario already running.");

            _scenario = scenario;
            _plan = plan;
            _adapter = adapter;
            _session = session;
            _host = host;
            _lastReassertedObjectiveId = null;
            _lastReassertedTickIndex = -1;

            startResult = _host.Start(_lastState);
            _lastTick = startResult;

            UpdateRuntimeWorldModelUnsafe();
        }

        Console.WriteLine(
            $"[SCN-RUNTIME] Started by={requestedBy} scenario={plan.ScenarioId}, " +
            $"scenarioVehicle={NormalizeText(plan.VehicleId, "none")}, " +
            $"runtimeVehicle={ResolveRuntimeVehicleId()}, " +
            $"vehicleProfile={NormalizeText(_activeVehicleContext?.ProfileId, "none")}, " +
            $"platform={NormalizeText(_activeVehicleContext?.PlatformKind.ToString(), "none")}, " +
            $"targets={plan.Targets.Count}, state={startResult.SessionState}, " +
            $"objective={startResult.CurrentObjectiveId ?? "none"}, " +
            $"appliedTask={startResult.AppliedNewTask}"
        );

        return GetSnapshot("Scenario started.");
    }

    public RuntimeScenarioSnapshot StopScenario(string reason = "Stopped by command.")
    {
        RuntimeScenarioExecutionHost? host;
        RuntimeScenarioSession? session;

        lock (_gate)
        {
            host = _host;
            session = _session;

            if (host is null || session is null)
            {
                _scenario = null;
                _taskManager.ClearTask();
                ClearRuntimeWorldUnsafe();
                return GetSnapshotUnsafe("No active scenario.");
            }

            try
            {
                var tick = host.Abort(_lastState);
                _lastTick = tick;
            }
            catch
            {
                // Abort sÄ±rasÄ±nda beklenmeyen hata olsa bile gÃ¼venli biÃ§imde gÃ¶revi temizliyoruz.
            }

            _host = null;
            _scenario = null;
            _taskManager.ClearTask();
            _lastReassertedObjectiveId = null;
            _lastReassertedTickIndex = -1;
            ClearRuntimeWorldUnsafe();
        }

        Console.WriteLine($"[SCN-RUNTIME] Stopped. reason={reason}");
        return GetSnapshot(reason);
    }

    public RuntimeScenarioTickResult? Tick(VehicleState state, long tickIndex)
    {
        RuntimeScenarioExecutionHost? host;

        lock (_gate)
        {
            _lastState = state;
            host = _host;
        }

        if (host is null)
            return null;

        var tick = host.Tick(state);

        lock (_gate)
        {
            _lastTick = tick;

            if (tick.SessionState is RuntimeScenarioSessionState.Completed or
                RuntimeScenarioSessionState.Failed or
                RuntimeScenarioSessionState.TimedOut or
                RuntimeScenarioSessionState.Aborted)
            {
                _host = null;
                _lastReassertedObjectiveId = null;
                _lastReassertedTickIndex = -1;
                UpdateRuntimeWorldModelUnsafe();
            }
            else
            {
                EnsureActiveObjectiveTaskUnsafe(tickIndex);
                UpdateRuntimeWorldModelUnsafe();
            }
        }

        if (tick.ObjectiveCompleted || tick.AppliedNewTask || tick.AllObjectivesCompleted)
        {
            Console.WriteLine(
                $"[SCN-RUNTIME] tick={tickIndex} state={tick.SessionState} " +
                $"completed={tick.CompletedObjectiveId ?? "none"} " +
                $"current={tick.CurrentObjectiveId ?? "none"} " +
                $"appliedTask={tick.AppliedNewTask} " +
                $"distXY={tick.DistanceToCurrentTargetMeters:F2} " +
                $"dist3D={tick.Distance3DToCurrentTargetMeters:F2}"
            );
        }

        if (tick.SessionState is RuntimeScenarioSessionState.Completed or
            RuntimeScenarioSessionState.Failed or
            RuntimeScenarioSessionState.TimedOut or
            RuntimeScenarioSessionState.Aborted)
        {
            Console.WriteLine(
                $"[SCN-RUNTIME] Finished state={tick.SessionState}, " +
                $"objective={tick.CurrentObjectiveId ?? "none"}, " +
                $"allCompleted={tick.AllObjectivesCompleted}"
            );
        }

        return tick;
    }

    public RuntimeScenarioSnapshot GetSnapshot(string? message = null)
    {
        lock (_gate)
        {
            return GetSnapshotUnsafe(message);
        }
    }

    private RuntimeScenarioSnapshot GetSnapshotUnsafe(string? message)
    {
        var currentTarget = ResolveCurrentTargetUnsafe();
        var runtimeVehicleId = ResolveRuntimeVehicleId();
        var capability = _activeVehicleContext?.CapabilityProfile;

        return new RuntimeScenarioSnapshot
        {
            Message = message,
            HasActiveScenario = _session is not null,
            IsRunning = _session?.State == RuntimeScenarioSessionState.Running,
            ScenarioId = _plan?.ScenarioId,
            ScenarioName = _plan?.ScenarioName,

            // KalÄ±cÄ± karar:
            // Ops/Gateway tarafÄ±na giden operational vehicle id tek kaynaktan gelir.
            // Scenario plan iÃ§indeki VehicleId metadata/default araÃ§ bilgisi olabilir,
            // fakat runtime telemetry/world/mission identity olarak kullanÄ±lmaz.
            VehicleId = runtimeVehicleId,
            ScenarioVehicleId = _plan?.VehicleId,

            // Vehicle Profile binding:
            // Runtime baÅŸlangÄ±cÄ±nda seÃ§ilen aktif araÃ§ profili snapshot iÃ§ine taÅŸÄ±nÄ±r.
            // Ops/Gateway/diagnostics tarafÄ± artÄ±k sadece vehicleId deÄŸil,
            // platform ve capability bilgisini de gÃ¶rebilir.
            VehicleProfileId = _activeVehicleContext?.ProfileId,
            VehiclePlatformKind = _activeVehicleContext?.PlatformKind.ToString(),
            VehicleDisplayName = _activeVehicleContext?.Profile?.DisplayName,
            VehicleProfileActive = _activeVehicleContext?.HasProfile == true,
            VehicleIsUnderwater = _activeVehicleContext?.IsUnderwater == true,
            VehicleIsMiniRov = _activeVehicleContext?.IsMiniRov == true,

            VehicleHasThrusters = capability?.HasAnyThruster == true,
            VehicleHasReverseAuthority = capability?.HasReverseAuthority == true,
            VehicleCanGenerateLateralForce = capability?.CanGenerateLateralForce == true,
            VehicleCanGenerateYawMoment = capability?.CanGenerateYawMoment == true,
            VehicleCapabilitySummary = capability?.Summary,

            State = _session?.State.ToString() ?? "None",
            RunId = _session?.RunId,
            CurrentObjectiveId = _session?.CurrentObjectiveId,
            CompletedObjectiveCount = _session?.CompletedObjectiveIds.Count ?? 0,
            TotalObjectiveCount = _plan?.Targets.Count ?? 0,
            LastCompletedObjectiveId = _lastTick?.CompletedObjectiveId,
            LastDistanceToTargetMeters = _lastTick?.DistanceToCurrentTargetMeters,
            LastDistance3DToTargetMeters = _lastTick?.Distance3DToCurrentTargetMeters,
            LastTickSummary = _lastTick?.Summary,
            SessionSummary = _session?.Summary,

            ActiveObjectiveTargetX = currentTarget?.Target.X,
            ActiveObjectiveTargetY = currentTarget?.Target.Y,
            ActiveObjectiveTargetZ = currentTarget?.Target.Z,
            ActiveObjectiveToleranceMeters = currentTarget?.ToleranceMeters,

            RoutePoints = BuildRoutePointsUnsafe(),
            WorldObjects = BuildWorldObjectsUnsafe()
        };
    }

    private ScenarioMissionTarget? ResolveCurrentTargetUnsafe()
    {
        if (_session is null || _plan is null)
            return null;

        var objectiveId = _session.CurrentObjectiveId;

        if (string.IsNullOrWhiteSpace(objectiveId))
            return _session.CurrentTarget ?? _session.NextTarget;

        return _plan.Targets.FirstOrDefault(x =>
            string.Equals(x.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<RuntimeScenarioRoutePoint> BuildRoutePointsUnsafe()
    {
        if (_plan is null || _plan.Targets.Count == 0)
            return Array.Empty<RuntimeScenarioRoutePoint>();

        return _plan.Targets
            .Select((target, index) => new RuntimeScenarioRoutePoint
            {
                Id = target.ObjectiveId,
                Label = BuildObjectiveLabel(target.ObjectiveId, index),
                ObjectiveId = target.ObjectiveId,
                Index = index,
                X = target.Target.X,
                Y = target.Target.Y,
                Z = target.Target.Z,
                ToleranceMeters = target.ToleranceMeters,
                IsActive = _session is not null &&
                           string.Equals(_session.CurrentObjectiveId, target.ObjectiveId, StringComparison.OrdinalIgnoreCase),
                IsCompleted = _session is not null &&
                              _session.CompletedObjectiveIds.Contains(target.ObjectiveId)
            })
            .ToArray();
    }

    private void EnsureActiveObjectiveTaskUnsafe(long tickIndex)
    {
        if (_session is null || _plan is null || _adapter is null)
            return;

        if (_session.State != RuntimeScenarioSessionState.Running)
            return;

        var objectiveId = _session.CurrentObjectiveId;

        if (string.IsNullOrWhiteSpace(objectiveId))
            return;

        var target = _plan.Targets
            .FirstOrDefault(x => string.Equals(x.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));

        if (target is null)
            return;

        var currentTask = _taskManager.CurrentTask;

        if (IsTaskAlreadyPointingToTarget(currentTask, target))
            return;

        var task = _adapter.ToTaskDefinition(target);
        _taskManager.SetTask(task);

        if (!string.Equals(_lastReassertedObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase) ||
            _lastReassertedTickIndex != tickIndex)
        {
            Console.WriteLine(
                $"[SCN-RUNTIME] Reasserted active objective task tick={tickIndex} " +
                $"objective={objectiveId} task={task.Name} " +
                $"target=({target.Target.X:F2},{target.Target.Y:F2},{target.Target.Z:F2})"
            );

            _lastReassertedObjectiveId = objectiveId;
            _lastReassertedTickIndex = tickIndex;
        }
    }

    private static bool IsTaskAlreadyPointingToTarget(TaskDefinition? task, ScenarioMissionTarget target)
    {
        if (task is null)
            return false;

        if (task.Target is not Vec3 taskTarget)
            return false;

        return
            Math.Abs(taskTarget.X - target.Target.X) <= 0.0001 &&
            Math.Abs(taskTarget.Y - target.Target.Y) <= 0.0001 &&
            Math.Abs(taskTarget.Z - target.Target.Z) <= 0.0001;
    }

    private RuntimeScenarioExecutionOptions ReadOptions()
    {
        return new RuntimeScenarioExecutionOptions
        {
            AutoApplyFirstTarget = ReadBool("ScenarioRuntime:AutoApplyFirstTarget", true),
            AutoAdvanceObjectives = ReadBool("ScenarioRuntime:AutoAdvanceObjectives", true),
            ClearTaskOnCompletion = ReadBool("ScenarioRuntime:ClearTaskOnCompletion", true),
            ClearTaskOnStop = ReadBool("ScenarioRuntime:ClearTaskOnStop", true),
            UseDistanceTrackerForAdvance = ReadBool("ScenarioRuntime:UseDistanceTrackerForAdvance", true),

            SettleSeconds = ReadDouble("ScenarioRuntime:SettleSeconds", 0.35),
            MaxArrivalSpeedMps = ReadDouble("ScenarioRuntime:MaxArrivalSpeedMps", 0.75),
            MaxArrivalYawRateDegPerSec = ReadDouble("ScenarioRuntime:MaxArrivalYawRateDegPerSec", 25.0),
            DefaultToleranceMeters = ReadDouble("ScenarioRuntime:DefaultToleranceMeters", 1.0),

            EvaluateJudgeEveryTick = ReadBool("ScenarioRuntime:EvaluateJudgeEveryTick", true)
        }.Sanitized();
    }

        private string ResolveRuntimeVehicleId()
    {
        if (_activeVehicleContext?.HasProfile == true &&
            !string.IsNullOrWhiteSpace(_activeVehicleContext.VehicleId))
        {
            return _activeVehicleContext.VehicleId.Trim();
        }

        var scenarioRuntimeVehicleId = _config["ScenarioRuntime:VehicleId"];

        if (!string.IsNullOrWhiteSpace(scenarioRuntimeVehicleId))
            return scenarioRuntimeVehicleId.Trim();

        var runtimeVehicleId = _config["Runtime:TelemetrySummary:VehicleId"];

        if (!string.IsNullOrWhiteSpace(runtimeVehicleId))
            return runtimeVehicleId.Trim();

        return DefaultRuntimeVehicleId;
    }

    private bool ReadBool(string key, bool fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return bool.TryParse(raw, out var value)
            ? value
            : fallback;
    }

    private double ReadDouble(string key, double fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}
