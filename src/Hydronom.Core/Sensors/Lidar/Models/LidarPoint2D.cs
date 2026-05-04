namespace Hydronom.Core.Sensors.Lidar.Models;

/// <summary>
/// LiDAR ışınının dünya veya sensör lokal düzlemindeki 2D nokta karşılığı.
/// </summary>
public readonly record struct LidarPoint2D(
    double X,
    double Y,
    double RangeMeters,
    double AngleDeg,
    bool Hit,
    string ObjectId
)
{
    public bool IsFinite =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(RangeMeters) &&
        double.IsFinite(AngleDeg);

    public LidarPoint2D Sanitized()
    {
        return new LidarPoint2D(
            X: Safe(X),
            Y: Safe(Y),
            RangeMeters: SafeNonNegative(RangeMeters),
            AngleDeg: Safe(AngleDeg),
            Hit: Hit,
            ObjectId: ObjectId?.Trim() ?? ""
        );
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