using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Sensors.Lidar.Models
{
    /// <summary>
    /// LiDAR/Sonar/Radar gibi tarama sensÃ¶rleri iÃ§in ortak range/point veri modeli.
    ///
    /// Ranges dÃ¼zenli aÃ§Ä±sal taramalar iÃ§in kullanÄ±lÄ±r.
    /// Points ise 2D/3D nokta bulutu veya projeksiyon iÃ§in kullanÄ±labilir.
    /// </summary>
    public readonly record struct LidarSampleData(
        IReadOnlyList<double> Ranges,
        double AngleMinRad,
        double AngleMaxRad,
        double AngleIncrementRad,
        double RangeMinMeters,
        double RangeMaxMeters,
        IReadOnlyList<SimVector3> Points,
        int HitCount
    )
    {
        public static LidarSampleData Empty => new(
            Ranges: Array.Empty<double>(),
            AngleMinRad: 0.0,
            AngleMaxRad: 0.0,
            AngleIncrementRad: 0.0,
            RangeMinMeters: 0.0,
            RangeMaxMeters: 0.0,
            Points: Array.Empty<SimVector3>(),
            HitCount: 0
        );

        public bool IsFinite =>
            double.IsFinite(AngleMinRad) &&
            double.IsFinite(AngleMaxRad) &&
            double.IsFinite(AngleIncrementRad) &&
            double.IsFinite(RangeMinMeters) &&
            double.IsFinite(RangeMaxMeters);

        public LidarSampleData Sanitized()
        {
            var ranges = Ranges is null
                ? Array.Empty<double>()
                : Ranges
                    .Select(r => double.IsFinite(r) ? r : double.PositiveInfinity)
                    .ToArray();

            var points = Points is null
                ? Array.Empty<SimVector3>()
                : Points
                    .Select(p => p.Sanitized())
                    .Where(p => p.IsFinite)
                    .ToArray();

            return new LidarSampleData(
                Ranges: ranges,
                AngleMinRad: Safe(AngleMinRad),
                AngleMaxRad: Safe(AngleMaxRad),
                AngleIncrementRad: SafeNonNegative(AngleIncrementRad),
                RangeMinMeters: SafeNonNegative(RangeMinMeters),
                RangeMaxMeters: SafeNonNegative(RangeMaxMeters),
                Points: points,
                HitCount: HitCount < 0 ? 0 : HitCount
            );
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}


