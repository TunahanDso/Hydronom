using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// İlk trajectory generator.
    ///
    /// PlannedPath noktalarını hız, heading ve lookahead bilgisi taşıyan
    /// trajectory noktalarına çevirir.
    /// </summary>
    public sealed class SimpleTrajectoryGenerator : ITrajectoryGenerator
    {
        public TrajectoryPlan GenerateTrajectory(
            PlanningContext context,
            PlannedPath path)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var planned = (path ?? PlannedPath.Empty).Sanitized();

            if (!planned.IsValid || planned.Points.Count == 0)
                return TrajectoryPlan.Empty with
                {
                    Summary = "TRAJECTORY_SKIPPED_INVALID_PATH"
                };

            var points = new List<TrajectoryPoint>();
            double distanceAlong = 0.0;

            for (var i = 0; i < planned.Points.Count; i++)
            {
                var current = planned.Points[i];

                if (i > 0)
                    distanceAlong += Distance(planned.Points[i - 1].Position, current.Position);

                var next = i + 1 < planned.Points.Count
                    ? planned.Points[i + 1]
                    : current;

                var heading = ResolveHeading(current, next);
                var distanceToGoal = Distance(current.Position, safe.Goal.TargetPosition);

                var speed = ResolveSpeed(
                    safe,
                    planned,
                    current,
                    distanceToGoal,
                    i);

                var point = new TrajectoryPoint
                {
                    Id = $"traj-{i}-{current.Id}",
                    Position = current.Position,
                    HeadingDeg = heading,
                    DesiredSpeedMps = speed,
                    DesiredYawRateDegPerSec = 0.0,
                    Curvature = EstimateCurvature(planned, i),
                    DistanceAlongPathMeters = distanceAlong,
                    AcceptanceRadiusMeters = current.AcceptanceRadiusMeters,
                    RiskScore = Math.Max(current.RiskScore, planned.Risk.RiskScore),
                    RequiresHeadingAlignment = ShouldRequireHeadingAlignment(safe, planned, current, i),
                    RequiresSlowMode = planned.Risk.RequiresSlowMode || current.RiskScore >= 0.45,
                    Reason = BuildReason(planned, current, i)
                }.Sanitized();

                points.Add(point);
            }

            var lookAhead = SelectLookAheadPoint(
                safe,
                points);

            return new TrajectoryPlan
            {
                Mode = planned.Mode,
                SourcePath = planned,
                Points = points,
                LookAheadPoint = lookAhead,
                Risk = planned.Risk,
                IsValid = points.Count > 0,
                RequiresReplan = planned.RequiresReplan,
                RequiresSlowMode = planned.Risk.RequiresSlowMode,
                Source = nameof(SimpleTrajectoryGenerator),
                Summary = $"TRAJECTORY points={points.Count} lookahead={lookAhead?.Id ?? "none"} risk={planned.Risk.RiskScore:F2}"
            }.Sanitized();
        }

        private static double ResolveHeading(
            PlannedPathPoint current,
            PlannedPathPoint next)
        {
            if (double.IsFinite(current.PreferredHeadingDeg))
                return current.PreferredHeadingDeg;

            var dx = next.Position.X - current.Position.X;
            var dy = next.Position.Y - current.Position.Y;

            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return 0.0;

            return NormalizeDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        private static double ResolveSpeed(
            PlanningContext context,
            PlannedPath path,
            PlannedPathPoint point,
            double distanceToGoal,
            int index)
        {
            var cruise = Math.Min(
                context.MaxPlanSpeedMps,
                Math.Min(point.PreferredSpeedMps, context.Goal.DesiredCruiseSpeedMps));

            if (index == 0)
                cruise = Math.Min(cruise, 0.35);

            if (path.Risk.RequiresSlowMode || point.RiskScore >= 0.45)
                cruise *= 0.55;

            if (distanceToGoal <= context.Goal.AcceptanceRadiusMeters * 3.0)
                cruise = Math.Min(cruise, context.Goal.DesiredArrivalSpeedMps);

            return Math.Clamp(cruise, 0.0, context.MaxPlanSpeedMps);
        }

        private static TrajectoryPoint? SelectLookAheadPoint(
            PlanningContext context,
            IReadOnlyList<TrajectoryPoint> points)
        {
            if (points.Count == 0)
                return null;

            var vehicle = context.VehicleState.Position;
            var lookAhead = Math.Max(1.0, context.LookAheadMeters);

            TrajectoryPoint? best = null;
            double bestDistance = double.PositiveInfinity;

            foreach (var point in points)
            {
                var distance = Distance(vehicle, point.Position);

                if (distance >= lookAhead * 0.35 && distance < bestDistance)
                {
                    best = point;
                    bestDistance = distance;
                }
            }

            return best ?? points[^1];
        }

        private static bool ShouldRequireHeadingAlignment(
            PlanningContext context,
            PlannedPath path,
            PlannedPathPoint point,
            int index)
        {
            if (context.Goal.RequiresHeadingAlignment)
                return true;

            if (index == 0)
                return true;

            if (path.Mode is PlanningMode.Corridor or PlanningMode.Avoidance or PlanningMode.Arrival)
                return true;

            return point.RiskScore >= 0.45;
        }

        private static double EstimateCurvature(
            PlannedPath path,
            int index)
        {
            if (path.Points.Count < 3)
                return 0.0;

            if (index <= 0 || index >= path.Points.Count - 1)
                return 0.0;

            var a = path.Points[index - 1].Position;
            var b = path.Points[index].Position;
            var c = path.Points[index + 1].Position;

            var h1 = HeadingDeg(a, b);
            var h2 = HeadingDeg(b, c);

            var delta = Math.Abs(NormalizeDeg(h2 - h1));
            var segmentLength = Math.Max(0.1, Distance(a, b));

            return Math.Clamp(delta / segmentLength / 90.0, 0.0, 10.0);
        }

        private static string BuildReason(
            PlannedPath path,
            PlannedPathPoint point,
            int index)
        {
            return $"{path.Mode}_TRAJECTORY_{index}_{point.Reason}";
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double HeadingDeg(Vec3 from, Vec3 to)
        {
            return NormalizeDeg(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI);
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