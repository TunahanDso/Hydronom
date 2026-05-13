using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// İlk global planner implementasyonu.
    ///
    /// Bu planner doğrudan hedefe giden kaba bir path üretir.
    /// Engel/koridor düzeltmesi LocalPlanner katmanına bırakılır.
    /// </summary>
    public sealed class DirectGlobalPlanner : IGlobalPlanner
    {
        public PlannedPath PlanGlobal(PlanningContext context)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var goal = safe.Goal.Sanitized();

            if (goal.PreferredMode == PlanningMode.Idle)
                return PlannedPath.Empty with
                {
                    Summary = "GLOBAL_IDLE"
                };

            var start = safe.VehicleState.Position.Sanitized();
            var target = goal.TargetPosition.Sanitized();

            var headingDeg = ResolveHeadingDeg(start, target, goal.PreferredHeadingDeg);
            var distance = Distance(start, target);

            var startPoint = new PlannedPathPoint
            {
                Id = "global-start",
                Position = start,
                PreferredHeadingDeg = headingDeg,
                PreferredSpeedMps = 0.0,
                AcceptanceRadiusMeters = Math.Max(0.5, safe.VehicleRadiusMeters),
                ClearanceMeters = double.PositiveInfinity,
                RiskScore = 0.0,
                IsMandatory = true,
                IsCorridorPoint = false,
                Reason = "GLOBAL_START"
            }.Sanitized();

            var targetPoint = new PlannedPathPoint
            {
                Id = goal.GoalId,
                Position = target,
                PreferredHeadingDeg = headingDeg,
                PreferredSpeedMps = goal.DesiredCruiseSpeedMps,
                AcceptanceRadiusMeters = goal.AcceptanceRadiusMeters,
                ClearanceMeters = double.PositiveInfinity,
                RiskScore = 0.0,
                IsMandatory = true,
                IsCorridorPoint = false,
                Reason = "GLOBAL_TARGET"
            }.Sanitized();

            return new PlannedPath
            {
                Mode = goal.PreferredMode == PlanningMode.Idle ? PlanningMode.Navigate : goal.PreferredMode,
                Goal = goal,
                Points = new[] { startPoint, targetPoint },
                Risk = PlanningRiskReport.Clear,
                IsValid = distance > 0.01,
                RequiresReplan = false,
                Source = nameof(DirectGlobalPlanner),
                Summary = $"GLOBAL_DIRECT distance={distance:F2}m"
            }.Sanitized();
        }

        private static double ResolveHeadingDeg(Vec3 start, Vec3 target, double preferredHeadingDeg)
        {
            if (double.IsFinite(preferredHeadingDeg))
                return NormalizeDeg(preferredHeadingDeg);

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;

            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return 0.0;

            return NormalizeDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
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