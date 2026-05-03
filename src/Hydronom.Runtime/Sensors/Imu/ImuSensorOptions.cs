namespace Hydronom.Runtime.Sensors.Imu;

/// <summary>
/// C# IMU sensÃ¶rÃ¼ iÃ§in ayar modeli.
/// 
/// Bu model hem sim IMU hem de ileride gelecek gerÃ§ek serial/I2C/SPI IMU
/// backend'leri iÃ§in ortak temel ayarlarÄ± taÅŸÄ±r.
/// </summary>
public sealed class ImuSensorOptions
{
    public string Source { get; set; } = "imu0";

    public string FrameId { get; set; } = "base_link";

    public string CalibrationId { get; set; } = "sim_imu_default";

    public double RateHz { get; set; } = 100.0;

    /// <summary>
    /// SimÃ¼lasyon yaw hÄ±zÄ±.
    /// Derece/saniye cinsindendir.
    /// </summary>
    public double SimYawRateDegPerSec { get; set; } = 5.0;

    /// <summary>
    /// SimÃ¼lasyon roll salÄ±nÄ±m genliÄŸi.
    /// Derece cinsindendir.
    /// </summary>
    public double SimRollAmplitudeDeg { get; set; } = 2.0;

    /// <summary>
    /// SimÃ¼lasyon pitch salÄ±nÄ±m genliÄŸi.
    /// Derece cinsindendir.
    /// </summary>
    public double SimPitchAmplitudeDeg { get; set; } = 1.2;

    /// <summary>
    /// IMU sÄ±caklÄ±k simÃ¼lasyonu.
    /// </summary>
    public double SimTemperatureC { get; set; } = 32.0;

    /// <summary>
    /// Ã‡ok kÃ¼Ã§Ã¼k sahte noise Ã¼retmek iÃ§in kullanÄ±lÄ±r.
    /// Åimdilik basit tutuldu; gerÃ§ek noise model ileride ayrÄ± profile taÅŸÄ±nacak.
    /// </summary>
    public double NoiseScale { get; set; } = 0.002;

    public static ImuSensorOptions Default()
    {
        return new ImuSensorOptions();
    }
}
