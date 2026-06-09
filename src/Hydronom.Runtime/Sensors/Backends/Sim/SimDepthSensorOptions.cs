namespace Hydronom.Runtime.Sensors.Backends.Sim;

/// <summary>
/// C# simülasyon depth sensörü için ayar modeli.
///
/// Hydronom world convention:
/// - Z yukarı yöndür.
/// - Depth pozitif aşağı yöndür.
/// - Bu yüzden depth = max(0, -Z) olarak hesaplanır.
/// </summary>
public sealed class SimDepthSensorOptions
{
    public string Source { get; set; } = "depth0";

    public string FrameId { get; set; } = "depth_link";

    public string CalibrationId { get; set; } = "sim_depth_default";

    public double RateHz { get; set; } = 20.0;

    /// <summary>
    /// Basit simülasyon depth gürültüsü.
    /// Metre cinsindendir.
    /// </summary>
    public double DepthNoiseMeters { get; set; } = 0.01;

    /// <summary>
    /// Atmosfer / yüzey basıncı.
    /// kPa cinsindendir.
    /// </summary>
    public double SurfacePressureKPa { get; set; } = 101.3;

    /// <summary>
    /// Yaklaşık su basınç artışı.
    /// 1 metre su derinliği yaklaşık 9.81 kPa kabul edilir.
    /// </summary>
    public double PressureKPaPerMeter { get; set; } = 9.81;

    public double SimTemperatureC { get; set; } = 22.0;

    /// <summary>
    /// Truth provider yoksa üretilecek procedural derinlik.
    /// </summary>
    public double ProceduralDepthMeters { get; set; } = 1.0;

    public static SimDepthSensorOptions Default()
    {
        return new SimDepthSensorOptions();
    }

    public SimDepthSensorOptions Sanitized()
    {
        return new SimDepthSensorOptions
        {
            Source = string.IsNullOrWhiteSpace(Source) ? "depth0" : Source.Trim(),
            FrameId = string.IsNullOrWhiteSpace(FrameId) ? "depth_link" : FrameId.Trim(),
            CalibrationId = string.IsNullOrWhiteSpace(CalibrationId) ? "sim_depth_default" : CalibrationId.Trim(),
            RateHz = SafePositive(RateHz, 20.0),
            DepthNoiseMeters = SafeNonNegative(DepthNoiseMeters),
            SurfacePressureKPa = SafePositive(SurfacePressureKPa, 101.3),
            PressureKPaPerMeter = SafePositive(PressureKPaPerMeter, 9.81),
            SimTemperatureC = double.IsFinite(SimTemperatureC) ? SimTemperatureC : 22.0,
            ProceduralDepthMeters = SafeNonNegative(ProceduralDepthMeters)
        };
    }

    private static double SafePositive(double value, double fallback)
    {
        return double.IsFinite(value) && value > 0.0 ? value : fallback;
    }

    private static double SafeNonNegative(double value)
    {
        return double.IsFinite(value) && value >= 0.0 ? value : 0.0;
    }
}