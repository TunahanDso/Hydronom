using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.Scenarios.Mission;
using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed class RuntimeScenarioController
{
    private const string DefaultRuntimeVehicleId = "hydronom-main";

    private readonly IConfiguration _config;
    private readonly ITaskManager _taskManager;
    private readonly RuntimeWorldModel? _runtimeWorld;
    private readonly object _gate = new();

    private RuntimeScenarioExecutionHost? _host;
    private RuntimeScenarioSession? _session;
    private ScenarioMissionPlan? _plan;
    private ScenarioMissionAdapter? _adapter;
    private RuntimeScenarioTickResult? _lastTick;
    private VehicleState _lastState = VehicleState.Zero;

    private string? _lastReassertedObjectiveId;
    private long _lastReassertedTickIndex = -1;

    public RuntimeScenarioController(
        IConfiguration config,
        ITaskManager taskManager,
        RuntimeWorldModel? runtimeWorld = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _runtimeWorld = runtimeWorld;
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

        if (!File.Exists(resolvedPath))
            return GetSnapshot($"Scenario file not found: {resolvedPath}");

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
                // Abort sırasında beklenmeyen hata olsa bile güvenli biçimde görevi temizliyoruz.
            }

            _host = null;
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

        return new RuntimeScenarioSnapshot
        {
            Message = message,
            HasActiveScenario = _session is not null,
            IsRunning = _session?.State == RuntimeScenarioSessionState.Running,
            ScenarioId = _plan?.ScenarioId,
            ScenarioName = _plan?.ScenarioName,

            // Kalıcı karar:
            // Ops/Gateway tarafına giden operational vehicle id tek kaynaktan gelir.
            // Scenario plan içindeki VehicleId metadata/default araç bilgisi olabilir,
            // fakat runtime telemetry/world/mission identity olarak kullanılmaz.
            VehicleId = runtimeVehicleId,
            ScenarioVehicleId = _plan?.VehicleId,

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

    private IReadOnlyList<RuntimeScenarioWorldObject> BuildWorldObjectsUnsafe()
    {
        var objects = new List<RuntimeScenarioWorldObject>();

        if (_plan is not null)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = "start",
                Type = "start",
                Label = "START",
                X = 0.0,
                Y = 0.0,
                Z = ResolveDefaultStartZUnsafe(),
                Radius = 0.8,
                Color = "#38bdf8",
                IsActive = true
            });

            foreach (var target in _plan.Targets.Select((value, index) => new { value, index }))
            {
                var isLast = target.index == _plan.Targets.Count - 1;
                var objectiveId = target.value.ObjectiveId;

                objects.Add(new RuntimeScenarioWorldObject
                {
                    Id = objectiveId,
                    Type = isLast ? "finish" : "checkpoint",
                    Label = isLast ? "FINISH" : BuildObjectiveLabel(objectiveId, target.index),
                    ObjectiveId = objectiveId,
                    X = target.value.Target.X,
                    Y = target.value.Target.Y,
                    Z = target.value.Target.Z,
                    Radius = target.value.ToleranceMeters,
                    Color = isLast ? "#f97316" : "#facc15",
                    IsActive = true,
                    IsCompleted = _session is not null &&
                                  _session.CompletedObjectiveIds.Contains(objectiveId),
                    IsBlocking = false,
                    IsDetectable = false
                });
            }
        }

        if (IsSurfaceTeknofestParkur1Unsafe())
            AddTeknofestParkur1Buoys(objects);

        if (IsSurfaceTeknofestParkur2Unsafe())
            AddTeknofestParkur2Objects(objects);

        return objects;
    }

    private double ResolveDefaultStartZUnsafe()
    {
        if (_plan is null || _plan.Targets.Count == 0)
            return 0.0;

        var firstTargetZ = _plan.Targets[0].Target.Z;

        return double.IsFinite(firstTargetZ)
            ? firstTargetZ
            : 0.0;
    }

    private bool IsUnderwaterScenarioUnsafe()
    {
        var scenarioId = _plan?.ScenarioId ?? string.Empty;
        var scenarioName = _plan?.ScenarioName ?? string.Empty;
        var vehicleId = _plan?.VehicleId ?? string.Empty;

        return
            scenarioId.Contains("uuv", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("underwater", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("sualti", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("su_alti", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("submarine", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("sualtı", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("su altı", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("underwater", StringComparison.OrdinalIgnoreCase) ||
            vehicleId.Contains("uuv", StringComparison.OrdinalIgnoreCase) ||
            vehicleId.Contains("underwater", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSurfaceTeknofestParkur1Unsafe()
    {
        if (IsUnderwaterScenarioUnsafe())
            return false;

        var scenarioId = _plan?.ScenarioId ?? string.Empty;

        return scenarioId.Contains("teknofest_2026_parkur_1", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("parkur_1_point_tracking", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSurfaceTeknofestParkur2Unsafe()
    {
        if (IsUnderwaterScenarioUnsafe())
            return false;

        var scenarioId = _plan?.ScenarioId ?? string.Empty;

        return scenarioId.Contains("teknofest_2026_parkur_2", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("parkur_2", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("obstacle_point_tracking", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddTeknofestParkur1Buoys(List<RuntimeScenarioWorldObject> objects)
    {
        var leftXs = new[] { 8.0, 20.0, 32.0, 44.0 };
        var rightXs = new[] { 8.0, 20.0, 32.0, 44.0 };

        for (var i = 0; i < leftXs.Length; i++)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = $"parkur1-left-buoy-{i + 1}",
                Type = "buoy",
                Label = $"L-{i + 1}",
                X = leftXs[i],
                Y = 8.0,
                Z = 0.0,
                Radius = 0.45,
                Color = "#22c55e",
                Side = "left",
                IsActive = true,
                IsBlocking = true,
                IsDetectable = true
            });
        }

        for (var i = 0; i < rightXs.Length; i++)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = $"parkur1-right-buoy-{i + 1}",
                Type = "buoy",
                Label = $"R-{i + 1}",
                X = rightXs[i],
                Y = -8.0,
                Z = 0.0,
                Radius = 0.45,
                Color = "#ef4444",
                Side = "right",
                IsActive = true,
                IsBlocking = true,
                IsDetectable = true
            });
        }
    }

    private static void AddTeknofestParkur2Objects(List<RuntimeScenarioWorldObject> objects)
    {
        AddGate(objects, 1, 10.0, 0.0);
        AddGate(objects, 2, 22.0, 0.0);
        AddGate(objects, 3, 34.0, 0.0);

        AddObstacle(objects, "parkur2-obstacle-1", "OBS-1", 15.0, 1.2, 0.8);
        AddObstacle(objects, "parkur2-obstacle-2", "OBS-2", 28.0, -1.4, 0.9);
        AddObstacle(objects, "parkur2-obstacle-3", "OBS-3", 40.0, 1.8, 0.9);
    }

    private static void AddGate(
        List<RuntimeScenarioWorldObject> objects,
        int index,
        double x,
        double centerY)
    {
        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = $"parkur2-gate-{index}-left",
            Type = "buoy",
            Label = $"G{index}-L",
            X = x,
            Y = centerY + 3.0,
            Z = 0.0,
            Radius = 0.45,
            Color = "#22c55e",
            Side = "left",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });

        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = $"parkur2-gate-{index}-right",
            Type = "buoy",
            Label = $"G{index}-R",
            X = x,
            Y = centerY - 3.0,
            Z = 0.0,
            Radius = 0.45,
            Color = "#ef4444",
            Side = "right",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });
    }

    private static void AddObstacle(
        List<RuntimeScenarioWorldObject> objects,
        string id,
        string label,
        double x,
        double y,
        double radius)
    {
        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = id,
            Type = "obstacle",
            Label = label,
            X = x,
            Y = y,
            Z = 0.0,
            Radius = radius,
            Color = "#f43f5e",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });
    }

    private void UpdateRuntimeWorldModelUnsafe()
    {
        if (_runtimeWorld is null)
            return;

        var scenarioObjects = BuildWorldObjectsUnsafe();

        var worldObjects = scenarioObjects
            .Where(x => x.IsActive)
            .Select(obj => ToHydronomWorldObject(obj, _plan, _session, ResolveRuntimeVehicleId()))
            .ToArray();

        _runtimeWorld.UpsertMany(worldObjects);
    }

    private void ClearRuntimeWorldUnsafe()
    {
        _runtimeWorld?.Clear();
    }

    private static HydronomWorldObject ToHydronomWorldObject(
        RuntimeScenarioWorldObject obj,
        ScenarioMissionPlan? plan,
        RuntimeScenarioSession? session,
        string runtimeVehicleId)
    {
        var kind = NormalizeWorldObjectKind(obj);
        var layer = ResolveWorldLayer(obj, kind);

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "scenario",
            ["scenarioObject"] = "true",
            ["type"] = NormalizeTagValue(obj.Type, "object"),
            ["kind"] = kind,
            ["layer"] = layer,
            ["runtimeVehicleId"] = NormalizeTagValue(runtimeVehicleId, DefaultRuntimeVehicleId),
            ["isBlocking"] = obj.IsBlocking ? "true" : "false",
            ["isDetectable"] = obj.IsDetectable ? "true" : "false",
            ["isCompleted"] = obj.IsCompleted ? "true" : "false",
            ["isActive"] = obj.IsActive ? "true" : "false"
        };

        AddTagIfPresent(tags, "label", obj.Label);
        AddTagIfPresent(tags, "objectiveId", obj.ObjectiveId);
        AddTagIfPresent(tags, "side", obj.Side);
        AddTagIfPresent(tags, "color", obj.Color);

        if (plan is not null)
        {
            AddTagIfPresent(tags, "scenarioId", plan.ScenarioId);
            AddTagIfPresent(tags, "scenarioName", plan.ScenarioName);
            AddTagIfPresent(tags, "scenarioVehicleId", plan.VehicleId);
        }

        if (session is not null)
        {
            AddTagIfPresent(tags, "currentObjectiveId", session.CurrentObjectiveId);
            tags["scenarioState"] = session.State.ToString();
        }

        var gateIndex = TryExtractGateIndex(obj);
        if (gateIndex is not null)
        {
            tags["gateIndex"] = gateIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tags["gateSide"] = NormalizeTagValue(obj.Side, "unknown");
            tags["corridorMarker"] = "true";
        }

        var parkur1Index = TryExtractParkur1BuoyIndex(obj);
        if (parkur1Index is not null)
        {
            tags["gateIndex"] = parkur1Index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tags["gateSide"] = NormalizeTagValue(obj.Side, "unknown");
            tags["corridorMarker"] = "true";
            tags["parkur"] = "1";
        }

        if (obj.Type.Equals("checkpoint", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("finish", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            tags["missionMarker"] = "true";
        }

        return new HydronomWorldObject
        {
            Id = obj.Id,
            Kind = kind,
            Name = NormalizeTagValue(obj.Label, obj.Id),
            Layer = layer,
            X = obj.X,
            Y = obj.Y,
            Z = obj.Z,
            Radius = obj.Radius,
            Width = obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0,
            Height = obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0,
            YawDeg = 0.0,
            IsActive = obj.IsActive,
            IsBlocking = obj.IsBlocking,
            Tags = tags
        };
    }

    private static string ResolveWorldLayer(RuntimeScenarioWorldObject obj, string kind)
    {
        if (obj.IsBlocking || kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
            return "scenario_obstacles";

        if (kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
            return "scenario_corridor";

        return "scenario_mission";
    }

    private static string NormalizeWorldObjectKind(RuntimeScenarioWorldObject obj)
    {
        if (obj.IsBlocking || obj.IsDetectable)
        {
            if (obj.Type.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return "obstacle";

            if (obj.Type.Equals("buoy", StringComparison.OrdinalIgnoreCase))
                return "buoy";

            return "obstacle";
        }

        if (string.IsNullOrWhiteSpace(obj.Type))
            return "object";

        return obj.Type.Trim();
    }

    private static void AddTagIfPresent(
        Dictionary<string, string> tags,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            tags[key] = value.Trim();
    }

    private static string NormalizeTagValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int? TryExtractGateIndex(RuntimeScenarioWorldObject obj)
    {
        if (!obj.Id.StartsWith("parkur2-gate-", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = obj.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        return int.TryParse(parts[2], out var index)
            ? index
            : null;
    }

    private static int? TryExtractParkur1BuoyIndex(RuntimeScenarioWorldObject obj)
    {
        if (!obj.Id.StartsWith("parkur1-", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = obj.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return null;

        return int.TryParse(parts[^1], out var index)
            ? index
            : null;
    }

    private static string BuildObjectiveLabel(string objectiveId, int index)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return $"WP-{index + 1}";

        if (objectiveId.Contains("finish", StringComparison.OrdinalIgnoreCase))
            return "FINISH";

        if (objectiveId.Contains("wp_", StringComparison.OrdinalIgnoreCase) ||
            objectiveId.Contains("wp-", StringComparison.OrdinalIgnoreCase) ||
            objectiveId.Contains("reach_wp", StringComparison.OrdinalIgnoreCase))
            return $"WP-{index + 1}";

        return objectiveId;
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

    private string ResolveScenarioPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            return Path.GetFullPath(requestedPath.Trim());

        var configuredPath = _config["ScenarioRuntime:ScenarioPath"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath.Trim());

        var configuredScenarioId = _config["ScenarioRuntime:ScenarioId"];

        if (!string.IsNullOrWhiteSpace(configuredScenarioId))
        {
            var configuredScenarioPath = ResolveSampleScenarioPath(configuredScenarioId.Trim());
            if (File.Exists(configuredScenarioPath))
                return configuredScenarioPath;
        }

        var parkur2Path = ResolveSampleScenarioPath("teknofest_2026_parkur_2_obstacle_point_tracking.json");
        if (ReadBool("ScenarioRuntime:UseParkur2", false) && File.Exists(parkur2Path))
            return parkur2Path;

        var parkur1Path = ResolveSampleScenarioPath("teknofest_2026_parkur_1_point_tracking.json");
        if (File.Exists(parkur1Path))
            return parkur1Path;

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Hydronom.Runtime",
                "Scenarios",
                "Samples",
                "teknofest_2026_parkur_1_point_tracking.json"));
    }

    private static string ResolveSampleScenarioPath(string fileNameOrId)
    {
        var fileName = fileNameOrId.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileNameOrId
            : $"{fileNameOrId}.json";

        var outputPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "Samples",
                fileName));

        if (File.Exists(outputPath))
            return outputPath;

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Hydronom.Runtime",
                "Scenarios",
                "Samples",
                fileName));
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
        var runtimeVehicleId = _config["Runtime:TelemetrySummary:VehicleId"];

        if (!string.IsNullOrWhiteSpace(runtimeVehicleId))
            return runtimeVehicleId.Trim();

        var scenarioRuntimeVehicleId = _config["ScenarioRuntime:VehicleId"];

        if (!string.IsNullOrWhiteSpace(scenarioRuntimeVehicleId))
            return scenarioRuntimeVehicleId.Trim();

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

public sealed class RuntimeScenarioSnapshot
{
    public string? Message { get; set; }
    public bool HasActiveScenario { get; set; }
    public bool IsRunning { get; set; }
    public string? ScenarioId { get; set; }
    public string? ScenarioName { get; set; }

    /// <summary>
    /// Runtime tarafından kullanılan operasyonel vehicle id.
    /// Ops/Gateway tarafında telemetry, mission, world ve actuator frame'leri bu kimlikle birleşir.
    /// </summary>
    public string? VehicleId { get; set; }

    /// <summary>
    /// Scenario dosyasından gelen metadata/default araç kimliği.
    /// Bu değer runtime identity yerine kullanılmaz; debug/izleme için tutulur.
    /// </summary>
    public string? ScenarioVehicleId { get; set; }

    public string State { get; set; } = "None";
    public string? RunId { get; set; }
    public string? CurrentObjectiveId { get; set; }
    public int CompletedObjectiveCount { get; set; }
    public int TotalObjectiveCount { get; set; }
    public string? LastCompletedObjectiveId { get; set; }
    public double? LastDistanceToTargetMeters { get; set; }
    public double? LastDistance3DToTargetMeters { get; set; }
    public string? LastTickSummary { get; set; }
    public string? SessionSummary { get; set; }

    public double? ActiveObjectiveTargetX { get; set; }
    public double? ActiveObjectiveTargetY { get; set; }
    public double? ActiveObjectiveTargetZ { get; set; }
    public double? ActiveObjectiveToleranceMeters { get; set; }

    public IReadOnlyList<RuntimeScenarioRoutePoint> RoutePoints { get; set; } =
        Array.Empty<RuntimeScenarioRoutePoint>();

    public IReadOnlyList<RuntimeScenarioWorldObject> WorldObjects { get; set; } =
        Array.Empty<RuntimeScenarioWorldObject>();
}

public sealed class RuntimeScenarioRoutePoint
{
    public string Id { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public int Index { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double ToleranceMeters { get; set; }
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
}

public sealed class RuntimeScenarioWorldObject
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "object";
    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public string? Side { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Radius { get; set; } = 0.5;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsDetectable { get; set; }
}