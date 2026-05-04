using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.Scenarios.Mission;
using Hydronom.Runtime.Scenarios.Runtime;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Runtime scenario execution host'unu config'e göre oluşturur.
    ///
    /// Bu katman normal runtime akışını bozmaz:
    /// - ScenarioRuntime:Enabled=false ise hiçbir şey yapmaz.
    /// - Enabled=true ise scenario JSON'u mission plan'a çevirir.
    /// - RuntimeScenarioExecutionHost ilk hedefi ITaskManager'a basar.
    /// - Gerçek loop içinde TickRuntimeScenarioIfEnabled(...) ile objective geçişleri takip edilir.
    /// </summary>
    private static async Task<RuntimeScenarioExecutionHost?> CreateRuntimeScenarioExecutionHostAsync(
        IConfiguration config,
        ITaskManager tasks,
        VehicleState initialState,
        CancellationToken cancellationToken)
    {
        if (!ReadBool(config, "ScenarioRuntime:Enabled", false))
        {
            Console.WriteLine("[SCN-RUNTIME] Disabled.");
            return null;
        }

        var scenarioPath = ResolveRuntimeScenarioPath(config);

        if (!File.Exists(scenarioPath))
        {
            Console.WriteLine($"[SCN-RUNTIME] Scenario file not found: {scenarioPath}");
            return null;
        }

        var loader = new ScenarioLoader();
        var scenario = await loader.LoadAsync(scenarioPath);

        cancellationToken.ThrowIfCancellationRequested();

        var adapter = new ScenarioMissionAdapter();
        var plan = adapter.BuildPlan(scenario);

        if (!plan.HasTargets)
        {
            Console.WriteLine($"[SCN-RUNTIME] Scenario has no mission targets: {scenario.Id}");
            return null;
        }

        var options = ReadRuntimeScenarioExecutionOptions(config);

        var session = new RuntimeScenarioSession(plan);

        var host = new RuntimeScenarioExecutionHost(
            session,
            tasks,
            options,
            adapter);

        var start = host.Start(initialState);

        Console.WriteLine(
            $"[SCN-RUNTIME] Started scenario={plan.ScenarioId}, " +
            $"targets={plan.Targets.Count}, state={start.SessionState}, " +
            $"objective={start.CurrentObjectiveId ?? "none"}, " +
            $"appliedTask={start.AppliedNewTask}"
        );

        return host;
    }

    /// <summary>
    /// Runtime scenario host'u bir loop sonunda güncel VehicleState ile tick eder.
    ///
    /// Dönüş değeri:
    /// - true  → scenario terminal state'e ulaştı, host artık kapatılabilir.
    /// - false → scenario devam ediyor veya host yok.
    /// </summary>
    private static bool TickRuntimeScenarioIfEnabled(
        RuntimeScenarioExecutionHost? host,
        VehicleState state,
        long tickIndex)
    {
        if (host is null)
            return false;

        var tick = host.Tick(state);

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

            return true;
        }

        return false;
    }

    /// <summary>
    /// ScenarioRuntime:ScenarioPath verilmişse onu kullanır.
    /// Verilmemişse TEKNOFEST Parkur-1 sample scenario dosyasına düşer.
    /// </summary>
    private static string ResolveRuntimeScenarioPath(IConfiguration config)
    {
        var configuredPath = config["ScenarioRuntime:ScenarioPath"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Scenarios",
                "Samples",
                "teknofest_2026_parkur_1_point_tracking.json"));
    }

    /// <summary>
    /// Runtime scenario objective geçiş ayarlarını config'ten okur.
    /// Değerler gerçek runtime testleri için güvenli varsayılanlarla başlar.
    /// </summary>
    private static RuntimeScenarioExecutionOptions ReadRuntimeScenarioExecutionOptions(IConfiguration config)
    {
        return new RuntimeScenarioExecutionOptions
        {
            AutoApplyFirstTarget = ReadBool(config, "ScenarioRuntime:AutoApplyFirstTarget", true),
            AutoAdvanceObjectives = ReadBool(config, "ScenarioRuntime:AutoAdvanceObjectives", true),
            ClearTaskOnCompletion = ReadBool(config, "ScenarioRuntime:ClearTaskOnCompletion", true),
            ClearTaskOnStop = ReadBool(config, "ScenarioRuntime:ClearTaskOnStop", true),
            UseDistanceTrackerForAdvance = ReadBool(config, "ScenarioRuntime:UseDistanceTrackerForAdvance", true),

            SettleSeconds = ReadDouble(config, "ScenarioRuntime:SettleSeconds", 0.35),
            MaxArrivalSpeedMps = ReadDouble(config, "ScenarioRuntime:MaxArrivalSpeedMps", 0.75),
            MaxArrivalYawRateDegPerSec = ReadDouble(config, "ScenarioRuntime:MaxArrivalYawRateDegPerSec", 25.0),
            DefaultToleranceMeters = ReadDouble(config, "ScenarioRuntime:DefaultToleranceMeters", 1.0),

            EvaluateJudgeEveryTick = ReadBool(config, "ScenarioRuntime:EvaluateJudgeEveryTick", true)
        }.Sanitized();
    }
}