using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

partial class Program
{
    /// <summary>
    /// Açıyı -180 / +180 derece aralığına normalize eder.
    /// </summary>
    private static double NormalizeAngleDeg(double deg)
    {
        if (!double.IsFinite(deg))
            return 0.0;

        deg %= 360.0;

        if (deg > 180.0)
            deg -= 360.0;

        if (deg < -180.0)
            deg += 360.0;

        return deg;
    }

    /// <summary>
    /// Lineer interpolasyon.
    /// t değeri 0..1 aralığına sıkıştırılır.
    /// </summary>
    private static double Lerp(double a, double b, double t)
    {
        if (!double.IsFinite(a))
            a = 0.0;

        if (!double.IsFinite(b))
            b = 0.0;

        if (!double.IsFinite(t))
            return a;

        if (t <= 0.0)
            return a;

        if (t >= 1.0)
            return b;

        return a + (b - a) * t;
    }

    /// <summary>
    /// Runtime ana döngüsü için hibrit bekleme.
    ///
    /// Mantık:
    /// - Kalan süre büyükse Task.Delay kullanır.
    /// - Kalan süre küçükse SpinWait ile daha hassas deadline yakalamaya çalışır.
    ///
    /// Bu yöntem Windows üzerinde normal Task.Delay jitter'ını azaltmak için kullanılır.
    /// </summary>
    private static async Task HybridWaitUntilAsync(long targetTicks, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            long now = Stopwatch.GetTimestamp();
            long remainingTicks = targetTicks - now;

            if (remainingTicks <= 0)
                return;

            double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;

            if (remainingMs > 2.0)
            {
                int delayMs = Math.Max(1, (int)Math.Floor(remainingMs - 1.0));
                await Task.Delay(delayMs, ct);
            }
            else if (remainingMs > 0.25)
            {
                Thread.SpinWait(200);
            }
            else
            {
                Thread.SpinWait(50);
            }
        }
    }

    /// <summary>
    /// Stopwatch tick farkını milisaniyeye çevirir.
    /// </summary>
    private static double StopwatchTicksToMs(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Stopwatch tick farkını saniyeye çevirir.
    /// </summary>
    private static double StopwatchTicksToSeconds(long ticks)
    {
        return ticks / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// Tick süresinden period tick sayısı üretir.
    /// Minimum 1 tick döner.
    /// </summary>
    private static long ComputePeriodTicks(int tickMs)
    {
        if (tickMs < 1)
            tickMs = 1;

        return Math.Max(1L, (long)Math.Round(Stopwatch.Frequency * (tickMs / 1000.0)));
    }

    /// <summary>
    /// Ölçülen dt değerini fizik/kontrol için güvenli aralığa çeker.
    /// </summary>
    private static double NormalizeLoopDt(double measuredDtSeconds, int fallbackTickMs)
    {
        double fallback = Math.Max(1, fallbackTickMs) / 1000.0;

        if (!double.IsFinite(measuredDtSeconds))
            return fallback;

        if (measuredDtSeconds <= 1e-4 || measuredDtSeconds > 1.0)
            return fallback;

        return measuredDtSeconds;
    }
}