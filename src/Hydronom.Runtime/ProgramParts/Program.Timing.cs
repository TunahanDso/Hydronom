using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

partial class Program
{
    /// <summary>
    /// AÃ§Ä±yÄ± -180 / +180 derece aralÄ±ÄŸÄ±na normalize eder.
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
    /// t deÄŸeri 0..1 aralÄ±ÄŸÄ±na sÄ±kÄ±ÅŸtÄ±rÄ±lÄ±r.
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
    /// Runtime ana dÃ¶ngÃ¼sÃ¼ iÃ§in hibrit bekleme.
    ///
    /// MantÄ±k:
    /// - Kalan sÃ¼re bÃ¼yÃ¼kse Task.Delay kullanÄ±r.
    /// - Kalan sÃ¼re kÃ¼Ã§Ã¼kse SpinWait ile daha hassas deadline yakalamaya Ã§alÄ±ÅŸÄ±r.
    ///
    /// Bu yÃ¶ntem Windows Ã¼zerinde normal Task.Delay jitter'Ä±nÄ± azaltmak iÃ§in kullanÄ±lÄ±r.
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
    /// Stopwatch tick farkÄ±nÄ± milisaniyeye Ã§evirir.
    /// </summary>
    private static double StopwatchTicksToMs(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Stopwatch tick farkÄ±nÄ± saniyeye Ã§evirir.
    /// </summary>
    private static double StopwatchTicksToSeconds(long ticks)
    {
        return ticks / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// Tick sÃ¼resinden period tick sayÄ±sÄ± Ã¼retir.
    /// Minimum 1 tick dÃ¶ner.
    /// </summary>
    private static long ComputePeriodTicks(int tickMs)
    {
        if (tickMs < 1)
            tickMs = 1;

        return Math.Max(1L, (long)Math.Round(Stopwatch.Frequency * (tickMs / 1000.0)));
    }

    /// <summary>
    /// Ã–lÃ§Ã¼len dt deÄŸerini fizik/kontrol iÃ§in gÃ¼venli aralÄ±ÄŸa Ã§eker.
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
