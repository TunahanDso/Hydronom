namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Scheduler tarafından modüle verilen zaman/tick bağlamı.
///
/// Amaç:
/// - modülün gerçek dt değerini bilmesi
/// - lateness/jitter analizine izin vermesi
/// - diagnostics/telemetry üretimini standartlaştırması
/// </summary>
public readonly record struct RuntimeModuleTickContext(
    RuntimeModuleKind Kind,
    string Name,
    long TickIndex,
    DateTime TimestampUtc,
    double DtSeconds,
    double TargetHz,
    double PeriodMs,
    double DeadlineMs,
    double LatenessMs
)
{
    /// <summary>
    /// Tick hedef deadline'ı kaçırdı mı?
    /// </summary>
    public bool IsLate => LatenessMs > 0.0;

    /// <summary>
    /// Güvenli normalize edilmiş context üretir.
    /// Runtime tarafında NaN/Infinity yayılmasını önlemek için kullanılır.
    /// </summary>
    public RuntimeModuleTickContext Sanitized()
    {
        return new RuntimeModuleTickContext(
            Kind,
            string.IsNullOrWhiteSpace(Name)
                ? Kind.ToString()
                : Name.Trim(),

            TickIndex < 0
                ? 0
                : TickIndex,

            TimestampUtc == default
                ? DateTime.UtcNow
                : TimestampUtc,

            double.IsFinite(DtSeconds) && DtSeconds > 0.0
                ? Math.Min(DtSeconds, 10.0)
                : 0.001,

            double.IsFinite(TargetHz) && TargetHz > 0.0
                ? TargetHz
                : 1.0,

            double.IsFinite(PeriodMs) && PeriodMs > 0.0
                ? PeriodMs
                : 1000.0,

            double.IsFinite(DeadlineMs) && DeadlineMs > 0.0
                ? DeadlineMs
                : 800.0,

            double.IsFinite(LatenessMs)
                ? Math.Max(0.0, LatenessMs)
                : 0.0
        );
    }
}