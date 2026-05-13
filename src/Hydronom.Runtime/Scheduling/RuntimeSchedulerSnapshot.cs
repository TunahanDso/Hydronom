namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Scheduler'ın anlık özet raporu.
/// Ops, diagnostics, blackbox, heartbeat ve runtime telemetry tarafına taşınabilir.
/// </summary>
public sealed record RuntimeSchedulerSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public long SchedulerTickCount { get; init; }

    public int ModuleCount { get; init; }

    public IReadOnlyList<RuntimeModuleSlotSnapshot> Modules { get; init; } =
        Array.Empty<RuntimeModuleSlotSnapshot>();

    public long TotalModuleTicks =>
        Modules.Sum(m => m.Metrics.TickCount);

    public long TotalFailures =>
        Modules.Sum(m => m.Metrics.FailureCount);

    public long TotalOverruns =>
        Modules.Sum(m => m.Metrics.OverrunCount);

    public long TotalLateTicks =>
        Modules.Sum(m => m.Metrics.LateCount);

    public string Summary =>
        $"modules={ModuleCount} " +
        $"moduleTicks={TotalModuleTicks} " +
        $"failures={TotalFailures} " +
        $"overruns={TotalOverruns} " +
        $"late={TotalLateTicks}";
}