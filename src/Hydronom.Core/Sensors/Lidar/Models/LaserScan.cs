namespace Hydronom.Core.Sensors.Lidar.Models;

/// <summary>
/// 2D LiDAR range scan modeli.
/// Açılar derece cinsindedir, mesafeler metredir.
/// </summary>
public readonly record struct LaserScan(
    double AngleMinDeg,
    double AngleMaxDeg,
    double AngleIncrementDeg,
    double RangeMinMeters,
    double RangeMaxMeters,
    IReadOnlyList<double> RangesMeters,
    IReadOnlyList<LidarPoint2D> Points,
    IReadOnlyList<LidarObstacleHint> ObstacleHints,
    DateTime TimestampUtc
)
{
    public int BeamCount => RangesMeters?.Count ?? 0;

    public bool HasRanges => BeamCount > 0;

    public double? NearestRangeMeters
    {
        get
        {
            if (RangesMeters is null || RangesMeters.Count == 0)
            {
                return null;
            }

            /*
             * record struct içinde lambda this/instance member'lara doğrudan erişince
             * CS1673 hatası verebilir. Bu yüzden range sınırlarını local değişkene alıyoruz.
             */
            var minRange = RangeMinMeters;
            var maxRange = RangeMaxMeters;

            var nearest = RangesMeters
                .Where(x => double.IsFinite(x) && x >= minRange && x <= maxRange)
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min();

            return double.IsPositiveInfinity(nearest) ? null : nearest;
        }
    }

    public LaserScan Sanitized()
    {
        var minRange = SafePositive(RangeMinMeters, 0.05);
        var maxRange = SafePositive(RangeMaxMeters, 30.0);

        if (maxRange < minRange)
        {
            (minRange, maxRange) = (maxRange, minRange);
        }

        var ranges = NormalizeRanges(RangesMeters, minRange, maxRange);
        var points = NormalizePoints(Points);
        var hints = NormalizeHints(ObstacleHints);

        return new LaserScan(
            AngleMinDeg: Safe(AngleMinDeg),
            AngleMaxDeg: Safe(AngleMaxDeg),
            AngleIncrementDeg: SafePositive(AngleIncrementDeg, 1.0),
            RangeMinMeters: minRange,
            RangeMaxMeters: maxRange,
            RangesMeters: ranges,
            Points: points,
            ObstacleHints: hints,
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc
        );
    }

    private static IReadOnlyList<double> NormalizeRanges(
        IReadOnlyList<double>? ranges,
        double minRange,
        double maxRange)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<double>();
        }

        return ranges
            .Select(x =>
            {
                if (!double.IsFinite(x))
                {
                    return maxRange;
                }

                if (x < minRange)
                {
                    return minRange;
                }

                if (x > maxRange)
                {
                    return maxRange;
                }

                return x;
            })
            .ToArray();
    }

    private static IReadOnlyList<LidarPoint2D> NormalizePoints(IReadOnlyList<LidarPoint2D>? points)
    {
        if (points is null || points.Count == 0)
        {
            return Array.Empty<LidarPoint2D>();
        }

        return points
            .Select(x => x.Sanitized())
            .Where(x => x.IsFinite)
            .ToArray();
    }

    private static IReadOnlyList<LidarObstacleHint> NormalizeHints(IReadOnlyList<LidarObstacleHint>? hints)
    {
        if (hints is null || hints.Count == 0)
        {
            return Array.Empty<LidarObstacleHint>();
        }

        return hints
            .Select(x => x.Sanitized())
            .Where(x => !string.IsNullOrWhiteSpace(x.ObjectId))
            .ToArray();
    }

    private static double Safe(double value, double fallback = 0.0)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double SafePositive(double value, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return value <= 0.0 ? fallback : value;
    }
}