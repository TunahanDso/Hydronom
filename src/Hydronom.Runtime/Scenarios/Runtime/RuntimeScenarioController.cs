using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Scenarios.Mission;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Runtime senaryo yaşam döngüsünü yöneten controller.
///
/// Amaç:
/// - CommandServer / ControlApp / Ops gibi dış komut kaynaklarından senaryo başlatmak.
/// - RuntimeScenarioExecutionHost'u Program.cs içinde dağınık local değişken olmaktan çıkarmak.
/// - Aktif scenario durumunu sorgulanabilir hale getirmek.
/// - Runtime loop içinde VehicleState ile objective geçişlerini takip etmek.
/// - Scenario hâlâ aktifken TaskManager görevi erken temizlerse aktif hedefi tekrar basmak.
/// </summary>
public sealed class RuntimeScenarioController
{
    private readonly IConfiguration _config;
    private readonly ITaskManager _taskManager;
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
        ITaskManager taskManager)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
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
        {
            return GetSnapshot("Scenario runtime auto-start disabled.");
        }

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
        {
            return GetSnapshot($"Scenario file not found: {resolvedPath}");
        }

        var loader = new ScenarioLoader();
        var scenario = await loader.LoadAsync(resolvedPath).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var adapter = new ScenarioMissionAdapter();
        var plan = adapter.BuildPlan(scenario);

        if (!plan.HasTargets)
        {
            return GetSnapshot($"Scenario has no mission targets: {scenario.Id}");
        }

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
            {
                return GetSnapshotUnsafe("Scenario already running.");
            }

            _plan = plan;
            _adapter = adapter;
            _session = session;
            _host = host;
            _lastReassertedObjectiveId = null;
            _lastReassertedTickIndex = -1;

            startResult = _host.Start(_lastState);
            _lastTick = startResult;
        }

        Console.WriteLine(
            $"[SCN-RUNTIME] Started by={requestedBy} scenario={plan.ScenarioId}, " +
            $"targets={plan.Targets.Count}, state={startResult.SessionState}, " +
            $"objective={startResult.CurrentObjectiveId ?? "none"}, " +
            $"appliedTask={startResult.AppliedNewTask}"
        );

        return GetSnapshot("Scenario started.");
    }

    public RuntimeScenarioSnapshot StopScenario(
        string reason = "Stopped by command.")
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
        }

        Console.WriteLine($"[SCN-RUNTIME] Stopped. reason={reason}");
        return GetSnapshot(reason);
    }

    public RuntimeScenarioTickResult? Tick(
        VehicleState state,
        long tickIndex)
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
        return new RuntimeScenarioSnapshot
        {
            Message = message,
            HasActiveScenario = _session is not null,
            IsRunning = _session?.State == RuntimeScenarioSessionState.Running,
            ScenarioId = _plan?.ScenarioId,
            ScenarioName = _plan?.ScenarioName,
            VehicleId = _plan?.VehicleId,
            State = _session?.State.ToString() ?? "None",
            RunId = _session?.RunId,
            CurrentObjectiveId = _session?.CurrentObjectiveId,
            CompletedObjectiveCount = _session?.CompletedObjectiveIds.Count ?? 0,
            TotalObjectiveCount = _plan?.Targets.Count ?? 0,
            LastCompletedObjectiveId = _lastTick?.CompletedObjectiveId,
            LastDistanceToTargetMeters = _lastTick?.DistanceToCurrentTargetMeters,
            LastDistance3DToTargetMeters = _lastTick?.Distance3DToCurrentTargetMeters,
            LastTickSummary = _lastTick?.Summary,
            SessionSummary = _session?.Summary
        };
    }

    /// <summary>
    /// Scenario hâlâ Running durumundayken TaskManager görevi kaybetmişse
    /// aktif objective hedefini tekrar TaskManager'a basar.
    ///
    /// Bu özellikle AdvancedTaskManager'ın kendi varış kabul mantığı ile
    /// RuntimeScenarioObjectiveTracker'ın hız/settle/tolerance mantığı ayrıştığında önemlidir.
    /// Scenario tamamlanmadan task=null kalırsa karar modülü NO_TASK/Idle'a düşer.
    /// </summary>
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

    private static bool IsTaskAlreadyPointingToTarget(
        TaskDefinition? task,
        ScenarioMissionTarget target)
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

        var outputPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "Samples",
                "teknofest_2026_parkur_1_point_tracking.json"));

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
                "teknofest_2026_parkur_1_point_tracking.json"));
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
}

/// <summary>
/// CommandServer / Ops / ControlApp tarafına dönebilecek sade scenario durum modeli.
/// </summary>
public sealed class RuntimeScenarioSnapshot
{
    public string? Message { get; set; }
    public bool HasActiveScenario { get; set; }
    public bool IsRunning { get; set; }
    public string? ScenarioId { get; set; }
    public string? ScenarioName { get; set; }
    public string? VehicleId { get; set; }
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
}