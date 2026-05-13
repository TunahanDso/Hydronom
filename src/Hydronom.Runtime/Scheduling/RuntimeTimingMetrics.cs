using System.Diagnostics;

namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Bir runtime modülünün zamanlama, jitter, deadline ve hata metriklerini tutar.
/// Ürün seviyesinde scheduler diagnostics, Ops ekranı ve blackbox kayıtları buradan beslenebilir.
/// </summary>
public sealed class RuntimeTimingMetrics
{
    private readonly object _lock = new();

    public long TickCount { get; private set; }
    public long SuccessCount { get; private set; }
    public long FailureCount { get; private set; }
    public long OverrunCount { get; private set; }
    public long LateCount { get; private set; }

    public double LastDurationMs { get; private set; }
    public double MaxDurationMs { get; private set; }
    public double AverageDurationMs { get; private set; }

    public double LastLatenessMs { get; private set; }
    public double MaxLatenessMs { get; private set; }

    public double LastDtMs { get; private set; }
    public double AverageObservedHz { get; private set; }

    public DateTime LastTickUtc { get; private set; }
    public string LastReason { get; private set; } = "NEVER_TICKED";

    public void Record(
        RuntimeModuleTickContext context,
        RuntimeModuleTickResult result,
        long elapsedStopwatchTicks)
    {
        var safeContext = context.Sanitized();
        var safeResult = result.Sanitized();

        var elapsedMs = elapsedStopwatchTicks * 1000.0 / Stopwatch.Frequency;

        if (!double.IsFinite(elapsedMs) || elapsedMs < 0.0)
            elapsedMs = 0.0;

        lock (_lock)
        {
            TickCount++;

            if (safeResult.Success)
                SuccessCount++;
            else
                FailureCount++;

            if (elapsedMs > safeContext.DeadlineMs)
                OverrunCount++;

            if (safeContext.LatenessMs > 0.0)
                LateCount++;

            LastDurationMs = elapsedMs;
            MaxDurationMs = Math.Max(MaxDurationMs, elapsedMs);
            AverageDurationMs += (elapsedMs - AverageDurationMs) / TickCount;

            LastLatenessMs = safeContext.LatenessMs;
            MaxLatenessMs = Math.Max(MaxLatenessMs, safeContext.LatenessMs);

            LastDtMs = safeContext.DtSeconds * 1000.0;

            if (safeContext.DtSeconds > 1e-9)
            {
                var observedHz = 1.0 / safeContext.DtSeconds;
                AverageObservedHz += (observedHz - AverageObservedHz) / TickCount;
            }

            LastTickUtc = safeContext.TimestampUtc;
            LastReason = safeResult.Reason;
        }
    }

    public RuntimeTimingMetricsSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new RuntimeTimingMetricsSnapshot(
                TickCount,
                SuccessCount,
                FailureCount,
                OverrunCount,
                LateCount,
                LastDurationMs,
                MaxDurationMs,
                AverageDurationMs,
                LastLatenessMs,
                MaxLatenessMs,
                LastDtMs,
                AverageObservedHz,
                LastTickUtc,
                LastReason
            );
        }
    }
}

/// <summary>
/// RuntimeTimingMetrics'in immutable snapshot modeli.
/// </summary>
public readonly record struct RuntimeTimingMetricsSnapshot(
    long TickCount,
    long SuccessCount,
    long FailureCount,
    long OverrunCount,
    long LateCount,
    double LastDurationMs,
    double MaxDurationMs,
    double AverageDurationMs,
    double LastLatenessMs,
    double MaxLatenessMs,
    double LastDtMs,
    double AverageObservedHz,
    DateTime LastTickUtc,
    string LastReason
);