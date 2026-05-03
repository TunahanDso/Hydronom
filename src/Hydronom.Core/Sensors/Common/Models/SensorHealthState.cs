namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensÃ¶rÃ¼n genel saÄŸlÄ±k durumu.
    ///
    /// Bu deÄŸer tek bir sample'Ä±n kalitesi deÄŸildir.
    /// SensÃ¶rÃ¼n genel Ã§alÄ±ÅŸma saÄŸlÄ±ÄŸÄ±nÄ± temsil eder.
    /// </summary>
    public enum SensorHealthState
    {
        Unknown = 0,

        Healthy = 10,
        Degraded = 20,
        Stale = 30,
        Failing = 40,
        Offline = 50,
        Disabled = 60,

        Simulated = 100,
        Replay = 110
    }
}

