using System.Diagnostics;

namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Scheduler tarafından yönetilen tek runtime modülü.
///
/// İlk sürüm:
/// - single-thread
/// - deterministic
/// - multi-rate
///
/// Gelecekte:
/// - dedicated thread
/// - pinned core
/// - realtime priority
/// - distributed runtime
/// gibi yapılara taşınabilecek şekilde tasarlanmıştır.
/// </summary>
public sealed class RuntimeModuleSlot
{
    private readonly Func<
        RuntimeModuleTickContext,
        CancellationToken,
        ValueTask<RuntimeModuleTickResult>> _tick;

    private long _nextDueTicks;
    private long _lastStartTicks;
    private long _lastEndTicks;
    private long _tickIndex;

    public RuntimeModuleSlot(
        RuntimeModuleKind kind,
        string name,
        RuntimeFrequencyProfile frequency,
        Func<
            RuntimeModuleTickContext,
            CancellationToken,
            ValueTask<RuntimeModuleTickResult>> tick)
    {
        Kind = kind;

        Name = string.IsNullOrWhiteSpace(name)
            ? kind.ToString()
            : name.Trim();

        Frequency = frequency
            ?? throw new ArgumentNullException(nameof(frequency));

        _tick = tick
            ?? throw new ArgumentNullException(nameof(tick));

        var now = Stopwatch.GetTimestamp();

        _nextDueTicks = now;
        _lastStartTicks = 0;
        _lastEndTicks = 0;
    }

    public RuntimeModuleKind Kind { get; }

    public string Name { get; }

    public RuntimeFrequencyProfile Frequency { get; private set; }

    public RuntimeTimingMetrics Metrics { get; } = new();

    public long TickIndex => Interlocked.Read(ref _tickIndex);

    public double PeriodMs => Frequency.PeriodMs;

    /// <summary>
    /// Modül şu anda tick almalı mı?
    /// </summary>
    public bool IsDue(long nowTicks)
    {
        return nowTicks >= Interlocked.Read(ref _nextDueTicks);
    }

    /// <summary>
    /// Runtime sırasında frekans güncellemesi yapılabilir.
    /// </summary>
    public void SetFrequency(
        RuntimeFrequencyProfile frequency,
        bool resetDeadline = false)
    {
        Frequency = frequency
            ?? throw new ArgumentNullException(nameof(frequency));

        if (resetDeadline)
        {
            var now = Stopwatch.GetTimestamp();

            Interlocked.Exchange(
                ref _nextDueTicks,
                now + PeriodToStopwatchTicks(Frequency.PeriodSeconds));
        }
    }

    /// <summary>
    /// Modül zamanı geldiyse tick çalıştırır.
    /// </summary>
    public async ValueTask<bool> TickIfDueAsync(
        long nowTicks,
        CancellationToken ct)
    {
        if (!IsDue(nowTicks))
            return false;

        var startTicks = Stopwatch.GetTimestamp();

        var lastStart = Interlocked.Read(ref _lastStartTicks);

        double dtSeconds;

        if (lastStart <= 0)
        {
            dtSeconds = Frequency.PeriodSeconds;
        }
        else
        {
            dtSeconds = Math.Max(
                1e-6,
                (startTicks - lastStart) / (double)Stopwatch.Frequency);
        }

        var nextDue = Interlocked.Read(ref _nextDueTicks);

        var latenessMs = Math.Max(
            0.0,
            (startTicks - nextDue) * 1000.0 / Stopwatch.Frequency);

        var currentTick = Interlocked.Increment(ref _tickIndex);

        var context = new RuntimeModuleTickContext(
            Kind,
            Name,
            currentTick,
            DateTime.UtcNow,
            dtSeconds,
            Frequency.TargetHz,
            Frequency.PeriodMs,
            Frequency.DeadlineMs,
            latenessMs
        ).Sanitized();

        RuntimeModuleTickResult result;

        try
        {
            result = (await _tick(context, ct)).Sanitized();
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            result = RuntimeModuleTickResult.Warn("CANCELLED");
        }
        catch (Exception ex)
        {
            result = RuntimeModuleTickResult.Fail(
                $"{ex.GetType().Name}: {ex.Message}");
        }

        var endTicks = Stopwatch.GetTimestamp();

        Interlocked.Exchange(ref _lastStartTicks, startTicks);
        Interlocked.Exchange(ref _lastEndTicks, endTicks);

        Metrics.Record(
            context,
            result,
            endTicks - startTicks);

        AdvanceNextDue(endTicks);

        return true;
    }

    public RuntimeModuleSlotSnapshot Snapshot()
    {
        var nextDue = Interlocked.Read(ref _nextDueTicks);

        var now = Stopwatch.GetTimestamp();

        var dueInMs =
            (nextDue - now) * 1000.0 / Stopwatch.Frequency;

        return new RuntimeModuleSlotSnapshot(
            Kind,
            Name,
            Frequency.TargetHz,
            Frequency.PeriodMs,
            Frequency.DeadlineMs,
            Math.Max(0.0, dueInMs),
            TickIndex,
            Metrics.Snapshot()
        );
    }

    /// <summary>
    /// Sonraki tick zamanını ilerletir.
    /// Catch-up politikası burada uygulanır.
    /// </summary>
    private void AdvanceNextDue(long nowTicks)
    {
        var periodTicks =
            PeriodToStopwatchTicks(Frequency.PeriodSeconds);

        if (periodTicks <= 0)
            periodTicks = 1;

        var next = Interlocked.Read(ref _nextDueTicks);

        next += periodTicks;

        if (next <= nowTicks)
        {
            var lagTicks = nowTicks - next;

            var maxCatchUpPeriods = Math.Max(
                0,
                (long)Math.Floor(Frequency.MaxCatchUpTicks));

            if (maxCatchUpPeriods <= 0)
            {
                next = nowTicks + periodTicks;
            }
            else
            {
                var missed =
                    Math.Max(1, lagTicks / periodTicks + 1);

                var allowed =
                    Math.Min(missed, maxCatchUpPeriods);

                next += allowed * periodTicks;

                if (next <= nowTicks)
                    next = nowTicks + periodTicks;
            }
        }

        Interlocked.Exchange(ref _nextDueTicks, next);
    }

    private static long PeriodToStopwatchTicks(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0.0)
            seconds = 1.0;

        return Math.Max(
            1L,
            (long)Math.Round(seconds * Stopwatch.Frequency));
    }
}

/// <summary>
/// Slot snapshot modeli.
/// </summary>
public readonly record struct RuntimeModuleSlotSnapshot(
    RuntimeModuleKind Kind,
    string Name,
    double TargetHz,
    double PeriodMs,
    double DeadlineMs,
    double DueInMs,
    long TickIndex,
    RuntimeTimingMetricsSnapshot Metrics
);