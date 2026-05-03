namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensÃ¶r runtime Ã§alÄ±ÅŸma modu.
    ///
    /// Normal Hydronom Ã§alÄ±ÅŸma modu CSharpPrimary olmalÄ±dÄ±r.
    /// PythonBackup yalnÄ±zca aÃ§Ä±kÃ§a fallback/backup olarak seÃ§ildiÄŸinde authority alabilir.
    /// </summary>
    public enum SensorRuntimeMode
    {
        Disabled = 0,

        CSharpPrimary = 10,

        PythonBackup = 20,

        CompareOnly = 30,

        Replay = 40,

        Simulation = 50,

        HybridDebug = 60
    }
}

