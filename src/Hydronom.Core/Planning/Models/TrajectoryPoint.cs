using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Controller'ın takip edebileceği zamansız hareket referans noktasıdır.
    ///
    /// PathPoint daha çok "nereden geçilecek?" sorusunu cevaplar.
    /// TrajectoryPoint ise "hangi hız/heading/eğrilik profiliyle geçilecek?"
    /// sorusunu cevaplar.
    /// </summary>
    public sealed record TrajectoryPoint
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        public Vec3 Position { get; init; } = Vec3.Zero;

        public double HeadingDeg { get; init; }

        public double DesiredSpeedMps { get; init; }

        public double DesiredYawRateDegPerSec { get; init; }

        public double Curvature { get; init; }

        public double DistanceAlongPathMeters { get; init; }

        public double AcceptanceRadiusMeters { get; init; } = 1.0;

        public double RiskScore { get; init; }

        public bool RequiresHeadingAlignment { get; init; }

        public bool RequiresSlowMode { get; init; }

        public string Reason { get; init; } = "TRAJECTORY_POINT";

        public TrajectoryPoint Sanitized()
        {
            return this with
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
                Position = Position.Sanitized(),
                HeadingDeg = NormalizeDeg(HeadingDeg),
                DesiredSpeedMps = SafeSpeed(DesiredSpeedMps),
                DesiredYawRateDegPerSec = SafeSigned(DesiredYawRateDegPerSec, 0.0, 360.0),
                Curvature = SafeSigned(Curvature, 0.0, 10.0),
                DistanceAlongPathMeters = SafeNonNegative(DistanceAlongPathMeters, 0.0),
                AcceptanceRadiusMeters = SafePositive(AcceptanceRadiusMeters, 1.0),
                RiskScore = Clamp01(RiskScore),
                Reason = string.IsNullOrWhiteSpace(Reason) ? "TRAJECTORY_POINT" : Reason.Trim()
            };
        }

        private static double SafeSpeed(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, -3.0, 6.0);
        }

        private static double SafeSigned(double value, double fallback, double absMax)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, -absMax, absMax);
        }

        private static double SafePositive(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0.0 ? value : fallback;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            return double.IsFinite(value) && value >= 0.0 ? value : fallback;
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}