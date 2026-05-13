namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Bir runtime modülünün hedef frekans ve deadline politikasını taşır.
/// Bu yapı ileride hem single-thread deterministic scheduler hem de
/// multi-thread realtime-benzeri host tarafından kullanılabilir.
/// </summary>
public sealed record RuntimeFrequencyProfile
{
    public double TargetHz { get; init; }

    /// <summary>
    /// Deadline = PeriodMs * DeadlineRatio.
    /// Örnek: 100 Hz modül için period 10 ms, ratio 0.8 ise deadline 8 ms.
    /// </summary>
    public double DeadlineRatio { get; init; } = 0.80;

    /// <summary>
    /// Scheduler gecikirse kaç tick catch-up yapmasına izin verileceğini belirler.
    /// Ürün seviyesinde çoğu control/actuator modülü için catch-up yerine son duruma atlamak daha güvenlidir.
    /// </summary>
    public double MaxCatchUpTicks { get; init; } = 1.0;

    public double PeriodSeconds => TargetHz <= 0.0 ? 1.0 : 1.0 / TargetHz;
    public double PeriodMs => PeriodSeconds * 1000.0;
    public double DeadlineMs => PeriodMs * DeadlineRatio;

    public RuntimeFrequencyProfile(double targetHz)
    {
        TargetHz = SanitizeHz(targetHz);
    }

    public RuntimeFrequencyProfile(
        double targetHz,
        double deadlineRatio,
        double maxCatchUpTicks = 1.0)
    {
        TargetHz = SanitizeHz(targetHz);

        DeadlineRatio = double.IsFinite(deadlineRatio) && deadlineRatio > 0.05
            ? Math.Clamp(deadlineRatio, 0.05, 2.0)
            : 0.80;

        MaxCatchUpTicks = double.IsFinite(maxCatchUpTicks) && maxCatchUpTicks >= 0.0
            ? Math.Min(maxCatchUpTicks, 10.0)
            : 1.0;
    }

    public static RuntimeFrequencyProfile FromHz(
        double hz,
        double deadlineRatio = 0.80,
        double maxCatchUpTicks = 1.0)
    {
        return new RuntimeFrequencyProfile(hz, deadlineRatio, maxCatchUpTicks);
    }

    private static double SanitizeHz(double hz)
    {
        if (!double.IsFinite(hz) || hz <= 0.0)
            return 1.0;

        return Math.Clamp(hz, 0.1, 2000.0);
    }
}