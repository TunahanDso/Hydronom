using System.Diagnostics;

namespace Hydronom.Runtime.Sensors.Sim;

/// <summary>
/// SimÃ¼lasyon sensÃ¶rleri iÃ§in ortak zaman kaynaÄŸÄ±.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - Sim IMU, Sim GPS, Sim LiDAR gibi sensÃ¶rlerin aynÄ± zaman ekseninde Ã§alÄ±ÅŸmasÄ±nÄ± saÄŸlamak.
/// - Her sensÃ¶rÃ¼n kendi kafasÄ±na gÃ¶re zaman Ã¼retmesini engellemek.
/// - Ä°leride replay/sim/physics entegrasyonunda ortak simÃ¼lasyon zamanÄ± kullanabilmek.
/// 
/// Åimdilik gerÃ§ek zamanlÄ± stopwatch tabanlÄ± Ã§alÄ±ÅŸÄ±r.
/// Ä°leride physics engine zamanÄ±, replay zamanÄ± veya test zamanÄ± ile deÄŸiÅŸtirilebilir.
/// </summary>
public sealed class SimSensorClock
{
    private readonly Stopwatch _watch = new();

    private DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;

    public bool IsRunning => _watch.IsRunning;

    public DateTimeOffset StartedUtc => _startedUtc;

    /// <summary>
    /// SimÃ¼lasyon baÅŸlangÄ±cÄ±ndan itibaren geÃ§en sÃ¼re.
    /// </summary>
    public TimeSpan Elapsed => _watch.Elapsed;

    /// <summary>
    /// SimÃ¼lasyonun ÅŸu anki UTC zamanÄ±.
    /// </summary>
    public DateTimeOffset NowUtc => _startedUtc + _watch.Elapsed;

    public void Start()
    {
        _startedUtc = DateTimeOffset.UtcNow;
        _watch.Restart();
    }

    public void Stop()
    {
        _watch.Stop();
    }

    public void Reset()
    {
        _startedUtc = DateTimeOffset.UtcNow;
        _watch.Restart();
    }
}
