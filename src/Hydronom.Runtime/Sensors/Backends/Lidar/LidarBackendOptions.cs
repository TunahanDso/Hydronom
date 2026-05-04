namespace Hydronom.Runtime.Sensors.Backends.Lidar;

/// <summary>
/// Sim/real LiDAR backend ayarları.
/// Şimdilik SimLidarBackend tarafından kullanılır.
/// </summary>
public sealed class LidarBackendOptions
{
    public string SensorId { get; set; } = "lidar0";

    public string Source { get; set; } = "sim_lidar";

    public string FrameId { get; set; } = "lidar_link";

    public string CalibrationId { get; set; } = "sim_lidar_uncalibrated";

    public double RateHz { get; set; } = 10.0;

    public double RangeMinMeters { get; set; } = 0.05;

    public double RangeMaxMeters { get; set; } = 30.0;

    public double FovDeg { get; set; } = 180.0;

    public int BeamCount { get; set; } = 181;

    public double NoiseMeters { get; set; } = 0.0;

    public bool UseWorldModel { get; set; } = true;

    public static LidarBackendOptions Default()
    {
        return new LidarBackendOptions();
    }

    public LidarBackendOptions Sanitized()
    {
        var rangeMin = SafePositive(RangeMinMeters, 0.05);
        var rangeMax = SafePositive(RangeMaxMeters, 30.0);

        if (rangeMax < rangeMin)
        {
            (rangeMin, rangeMax) = (rangeMax, rangeMin);
        }

        return new LidarBackendOptions
        {
            SensorId = Normalize(SensorId, "lidar0"),
            Source = Normalize(Source, "sim_lidar"),
            FrameId = Normalize(FrameId, "lidar_link"),
            CalibrationId = Normalize(CalibrationId, "sim_lidar_uncalibrated"),
            RateHz = SafePositive(RateHz, 10.0),
            RangeMinMeters = rangeMin,
            RangeMaxMeters = rangeMax,
            FovDeg = Clamp(SafePositive(FovDeg, 180.0), 1.0, 360.0),
            BeamCount = BeamCount < 2 ? 2 : BeamCount,
            NoiseMeters = SafeNonNegative(NoiseMeters),
            UseWorldModel = UseWorldModel
        };
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double SafePositive(double value, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return value <= 0.0 ? fallback : value;
    }

    private static double SafeNonNegative(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        return value < 0.0 ? 0.0 : value;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}