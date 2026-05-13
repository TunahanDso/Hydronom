using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planner tarafından üretilen soyut yol noktasını temsil eder.
    ///
    /// Bu hâlâ motor/control komutu değildir.
    /// Global/Local planner'ın "şuradan geç" dediği world-space ara noktadır.
    /// </summary>
    public sealed record PlannedPathPoint
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        public Vec3 Position { get; init; } = Vec3.Zero;

        public double PreferredHeadingDeg { get; init; } = double.NaN;

        public double PreferredSpeedMps { get; init; } = 1.0;

        public double AcceptanceRadiusMeters { get; init; } = 1.0;

        public double ClearanceMeters { get; init; } = double.PositiveInfinity;

        public double RiskScore { get; init; }

        public bool IsMandatory { get; init; }

        public bool IsCorridorPoint { get; init; }

        public string Reason { get; init; } = "PATH_POINT";

        public PlannedPathPoint Sanitized()
        {
            return this with
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
                Position = Position.Sanitized(),
                PreferredHeadingDeg = double.IsFinite(PreferredHeadingDeg)
                    ? NormalizeDeg(PreferredHeadingDeg)
                    : double.NaN,
                PreferredSpeedMps = SafeNonNegative(PreferredSpeedMps, 1.0),
                AcceptanceRadiusMeters = SafePositive(AcceptanceRadiusMeters, 1.0),
                ClearanceMeters = double.IsFinite(ClearanceMeters)
                    ? Math.Max(0.0, ClearanceMeters)
                    : double.PositiveInfinity,
                RiskScore = Clamp01(RiskScore),
                Reason = string.IsNullOrWhiteSpace(Reason) ? "PATH_POINT" : Reason.Trim()
            };
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
            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}