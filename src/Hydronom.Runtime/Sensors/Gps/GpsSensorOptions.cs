namespace Hydronom.Runtime.Sensors.Gps;

/// <summary>
/// C# GPS sensÃ¶rÃ¼ iÃ§in ayar modeli.
/// 
/// Bu model sim GPS ve ileride gelecek NMEA/UBX/GPSD backend'leri iÃ§in temel ayarlarÄ± taÅŸÄ±r.
/// </summary>
public sealed class GpsSensorOptions
{
    public string Source { get; set; } = "gps0";

    public string FrameId { get; set; } = "map";

    public string CalibrationId { get; set; } = "sim_gps_default";

    public double RateHz { get; set; } = 5.0;

    /// <summary>
    /// Sim GPS baÅŸlangÄ±Ã§ enlemi.
    /// Ä°stanbul'a yakÄ±n Ã¶rnek baÅŸlangÄ±Ã§ deÄŸeri.
    /// </summary>
    public double OriginLat { get; set; } = 41.0;

    /// <summary>
    /// Sim GPS baÅŸlangÄ±Ã§ boylamÄ±.
    /// </summary>
    public double OriginLon { get; set; } = 29.0;

    /// <summary>
    /// Sim araÃ§ x yÃ¶nÃ¼ hÄ±zÄ±, m/s.
    /// </summary>
    public double SimVxMetersPerSec { get; set; } = 0.4;

    /// <summary>
    /// Sim araÃ§ y yÃ¶nÃ¼ hÄ±zÄ±, m/s.
    /// </summary>
    public double SimVyMetersPerSec { get; set; } = 0.1;

    /// <summary>
    /// GPS HDOP deÄŸeri.
    /// KÃ¼Ã§Ã¼k deÄŸer daha iyi GPS kalitesini temsil eder.
    /// </summary>
    public double SimHdop { get; set; } = 0.9;

    /// <summary>
    /// Konum gÃ¼rÃ¼ltÃ¼sÃ¼, metre cinsinden.
    /// </summary>
    public double PositionNoiseMeters { get; set; } = 0.03;

    public static GpsSensorOptions Default()
    {
        return new GpsSensorOptions();
    }
}
