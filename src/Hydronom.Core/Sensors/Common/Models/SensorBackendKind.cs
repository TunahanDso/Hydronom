namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensÃ¶r verisinin hangi backend tÃ¼rÃ¼nden geldiÄŸini belirtir.
    ///
    /// FusionEngine backend detayÄ±na baÄŸÄ±mlÄ± olmamalÄ±dÄ±r.
    /// Ancak diagnostics, telemetry, replay ve debug iÃ§in backend tÃ¼rÃ¼ taÅŸÄ±nmalÄ±dÄ±r.
    /// </summary>
    public enum SensorBackendKind
    {
        Unknown = 0,

        Sim = 10,
        RealHardware = 20,
        Replay = 30,

        Serial = 40,
        I2c = 41,
        Spi = 42,
        Can = 43,
        Network = 44,
        Usb = 45,

        CSharpPrimary = 60,
        PythonBackup = 70,
        PythonCompareOnly = 71,

        External = 80,

        Mock = 90,

        Custom = 1000
    }
}

