namespace Hydronom.Core.Sensors.Lidar.Models;

/// <summary>
/// Sim/debug amaçlı LiDAR obstacle hint.
/// Fusion/Mapping tarafı isterse hangi objenin hangi beam'de görüldüğünü inceleyebilir.
/// </summary>
public readonly record struct LidarObstacleHint(
    string ObjectId,
    string Kind,
    double RangeMeters,
    double BearingDeg,
    double HitX,
    double HitY
)
{
    public LidarObstacleHint Sanitized()
    {
        return new LidarObstacleHint(
            ObjectId: Normalize(ObjectId, ""),
            Kind: Normalize(Kind, "unknown"),
            RangeMeters: SafeNonNegative(RangeMeters),
            BearingDeg: Safe(BearingDeg),
            HitX: Safe(HitX),
            HitY: Safe(HitY)
        );
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double Safe(double value, double fallback = 0.0)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double SafeNonNegative(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        return value < 0.0 ? 0.0 : value;
    }
}