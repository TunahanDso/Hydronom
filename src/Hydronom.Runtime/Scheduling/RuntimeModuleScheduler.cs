using System.Diagnostics;

namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Hydronom runtime için deterministic multi-rate scheduler.
///
/// Bu sınıfın ilk ürün seviyesi hedefi:
/// - Tek thread üzerinde farklı modülleri farklı frekanslarda çalıştırmak.
/// - Her modülün jitter/deadline/overrun metriğini tutmak.
/// - Ana runtime loop'un 100 ms tek blok davranışından çıkmasını sağlamak.
/// - İleride dedicated-thread realtime host'a taşınabilecek sözleşmeyi korumak.
///
/// Bilinçli tercih:
/// İlk aşamada multi-thread yapılmaz. Çünkü Hydronom'da state, actuator,
/// telemetry ve scenario tarafında race condition üretmeden önce ölçülebilir
/// single-thread scheduler temelini kurmak gerekir.
/// </summary>
public sealed class RuntimeModuleScheduler
{
    private readonly List<RuntimeModuleSlot> _slots = new();
    private readonly object _lock = new();

    private long _schedulerTickCount;

    public IReadOnlyList<RuntimeModuleSlot> Slots
    {
        get
        {
            lock (_lock)
                return _slots.ToArray();
        }
    }

    public RuntimeModuleScheduler Register(
        RuntimeModuleKind kind,
        string name,
        RuntimeFrequencyProfile frequency,
        Func<
            RuntimeModuleTickContext,
            CancellationToken,
            ValueTask<RuntimeModuleTickResult>> tick)
    {
        var slot = new RuntimeModuleSlot(
            kind,
            name,
            frequency,
            tick);

        lock (_lock)
        {
            if (_slots.Any(s =>
                    s.Kind == kind &&
                    string.Equals(s.Name, slot.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Runtime scheduler module already registered: {kind}/{slot.Name}");
            }

            _slots.Add(slot);
        }

        return this;
    }

    public RuntimeModuleScheduler RegisterSync(
        RuntimeModuleKind kind,
        string name,
        RuntimeFrequencyProfile frequency,
        Func<RuntimeModuleTickContext, RuntimeModuleTickResult> tick)
    {
        if (tick is null)
            throw new ArgumentNullException(nameof(tick));

        return Register(
            kind,
            name,
            frequency,
            (context, _) => ValueTask.FromResult(tick(context)));
    }

    /// <summary>
    /// Şu anda zamanı gelmiş tüm modülleri çalıştırır.
    /// </summary>
    public async ValueTask<int> TickDueAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _schedulerTickCount);

        RuntimeModuleSlot[] snapshot;

        lock (_lock)
            snapshot = _slots.ToArray();

        var nowTicks = Stopwatch.GetTimestamp();
        var executed = 0;

        foreach (var slot in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            if (await slot.TickIfDueAsync(nowTicks, ct))
                executed++;
        }

        return executed;
    }

    /// <summary>
    /// Basit scheduler run loop.
    /// Program.cs ana döngüsüne entegre edilmeden önce smoke testlerde de kullanılabilir.
    /// </summary>
    public async Task RunAsync(
        TimeSpan idleDelay,
        CancellationToken ct)
    {
        if (idleDelay <= TimeSpan.Zero)
            idleDelay = TimeSpan.FromMilliseconds(1);

        while (!ct.IsCancellationRequested)
        {
            await TickDueAsync(ct);
            await Task.Delay(idleDelay, ct);
        }
    }

    public RuntimeSchedulerSnapshot Snapshot()
    {
        RuntimeModuleSlot[] snapshot;

        lock (_lock)
            snapshot = _slots.ToArray();

        return new RuntimeSchedulerSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            SchedulerTickCount = Interlocked.Read(ref _schedulerTickCount),
            ModuleCount = snapshot.Length,
            Modules = snapshot
                .Select(s => s.Snapshot())
                .ToArray()
        };
    }
}