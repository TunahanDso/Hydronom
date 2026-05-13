using Hydronom.Runtime.Scheduling;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Runtime scheduler oluşturur.
    ///
    /// Bu scheduler ilk aşamada ana loop'u tamamen parçalamaz.
    /// Önce modül frekanslarını, jitter/overrun durumlarını ve runtime timing sağlığını görünür yapar.
    /// </summary>
    private static RuntimeModuleScheduler CreateRuntimeScheduler(IConfiguration config)
    {
        var scheduler = new RuntimeModuleScheduler();

        Console.WriteLine("[SCHED] Multi-rate runtime scheduler core enabled.");

        return scheduler;
    }

    /// <summary>
    /// Config üzerinden Hz okur.
    /// </summary>
    private static double ReadSchedulerHz(
        IConfiguration config,
        string key,
        double fallbackHz)
    {
        var hz = ReadDouble(config, key, fallbackHz);

        if (!double.IsFinite(hz) || hz <= 0.0)
            hz = fallbackHz;

        return Math.Clamp(hz, 0.1, 2000.0);
    }

    /// <summary>
    /// Scheduler snapshot'ını kısa ve okunabilir şekilde loglar.
    /// </summary>
    private static void EmitSchedulerSnapshot(
        RuntimeSchedulerSnapshot snapshot,
        bool verbose)
    {
        if (snapshot.ModuleCount <= 0)
            return;

        Console.WriteLine(
            $"[SCHED] tick={snapshot.SchedulerTickCount} " +
            $"modules={snapshot.ModuleCount} " +
            $"moduleTicks={snapshot.TotalModuleTicks} " +
            $"failures={snapshot.TotalFailures} " +
            $"overruns={snapshot.TotalOverruns} " +
            $"late={snapshot.TotalLateTicks}"
        );

        if (!verbose)
            return;

        foreach (var module in snapshot.Modules)
        {
            var m = module.Metrics;

            Console.WriteLine(
                $"[SCHED:{module.Name}] " +
                $"kind={module.Kind} " +
                $"targetHz={module.TargetHz:F1} " +
                $"avgHz={m.AverageObservedHz:F1} " +
                $"ticks={m.TickCount} " +
                $"last={m.LastDurationMs:F2}ms " +
                $"avg={m.AverageDurationMs:F2}ms " +
                $"max={m.MaxDurationMs:F2}ms " +
                $"late={m.LateCount}/{m.MaxLatenessMs:F2}ms " +
                $"overrun={m.OverrunCount} " +
                $"reason={m.LastReason}"
            );
        }
    }
}