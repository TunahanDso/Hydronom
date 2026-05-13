using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Planning.Models
{
    /// <summary>
    /// Planner'ın çözmeye çalıştığı görev hedefidir.
    ///
    /// TaskDefinition doğrudan planner içine taşınmaz.
    /// Onun yerine görev, world-aware planlamaya uygun sade bir hedef modeline çevrilir.
    /// </summary>
    public sealed record PlanningGoal
    {
        public string GoalId { get; init; } = "goal";

        public string DisplayName { get; init; } = "Planning Goal";

        public PlanningMode PreferredMode { get; init; } = PlanningMode.Navigate;

        public Vec3 TargetPosition { get; init; } = Vec3.Zero;

        public double AcceptanceRadiusMeters { get; init; } = 1.0;

        public double DesiredCruiseSpeedMps { get; init; } = 1.2;

        public double DesiredArrivalSpeedMps { get; init; } = 0.35;

        public double PreferredHeadingDeg { get; init; } = double.NaN;

        public bool RequiresHeadingAlignment { get; init; }

        public bool AllowReverse { get; init; }

        public bool Required { get; init; } = true;

        public int Priority { get; init; } = 0;

        public string Source { get; init; } = "runtime";

        public string Reason { get; init; } = "GOAL";

        public static PlanningGoal Idle { get; } = new()
        {
            GoalId = "idle",
            DisplayName = "Idle",
            PreferredMode = PlanningMode.Idle,
            TargetPosition = Vec3.Zero,
            AcceptanceRadiusMeters = 1.0,
            DesiredCruiseSpeedMps = 0.0,
            DesiredArrivalSpeedMps = 0.0,
            Source = "system",
            Reason = "IDLE"
        };

        public PlanningGoal Sanitized()
        {
            return this with
            {
                GoalId = Normalize(GoalId, "goal"),
                DisplayName = Normalize(DisplayName, "Planning Goal"),
                TargetPosition = TargetPosition.Sanitized(),
                AcceptanceRadiusMeters = SafePositive(AcceptanceRadiusMeters, 1.0),
                DesiredCruiseSpeedMps = SafeNonNegative(DesiredCruiseSpeedMps, 1.2),
                DesiredArrivalSpeedMps = SafeNonNegative(DesiredArrivalSpeedMps, 0.35),
                PreferredHeadingDeg = double.IsFinite(PreferredHeadingDeg) ? NormalizeDeg(PreferredHeadingDeg) : double.NaN,
                Source = Normalize(Source, "runtime"),
                Reason = Normalize(Reason, "GOAL")
            };
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double SafePositive(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0.0 ? value : fallback;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            return double.IsFinite(value) && value >= 0.0 ? value : fallback;
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