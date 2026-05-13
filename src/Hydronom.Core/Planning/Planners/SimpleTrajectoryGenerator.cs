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
    ///
    /// Bu sürümde lookahead seçimi sadece "araca en yakın uygun nokta" değildir.
    /// Araç path üzerinde nereye kadar ilerledi, hangi noktalar geride kaldı,
    /// projection hangi segment üzerinde gibi bilgiler dikkate alınır.
    ///
    /// Böylece araç gate/corridor noktasını geçtikten sonra eski gate noktasını
    /// tekrar lookahead olarak seçmez.
    /// </summary>
    public sealed class SimpleTrajectoryGenerator : ITrajectoryGenerator
    {
        private const double PassedPointSlackMeters = 0.75;
        private const double FinalCaptureLookAheadFactor = 0.55;

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

            var progress = EstimatePathProgress(
                safe.VehicleState.Position,
                points);

            var lookAhead = SelectLookAheadPoint(
                safe,
                points,
                progress);

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
                Summary =
                    $"TRAJECTORY points={points.Count} " +
                    $"progress={progress.DistanceAlongPathMeters:F2}m " +
                    $"seg={progress.SegmentIndex} " +
                    $"lookahead={lookAhead?.Id ?? "none"} " +
                    $"risk={planned.Risk.RiskScore:F2}"
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
            IReadOnlyList<TrajectoryPoint> points,
            PathProgress progress)
        {
            if (points.Count == 0)
                return null;

            if (points.Count == 1)
                return points[0];

            var vehicle = context.VehicleState.Position;
            var lookAheadDistance = Math.Max(1.0, context.LookAheadMeters);

            var final = points[^1];
            var distanceToFinal = Distance(vehicle, final.Position);

            /*
             * Final hedefe yaklaşıldığında lookahead zorla daha eski corridor/gate
             * noktalarına dönmemeli. Hedefe yeterince yakınsak final referansı korunur.
             */
            if (distanceToFinal <= Math.Max(
                    final.AcceptanceRadiusMeters * 3.0,
                    lookAheadDistance * FinalCaptureLookAheadFactor))
            {
                return final;
            }

            var desiredDistanceAlong = progress.DistanceAlongPathMeters + lookAheadDistance;

            TrajectoryPoint? bestForward = null;

            foreach (var point in points)
            {
                if (IsPointClearlyBehindProgress(point, progress))
                    continue;

                if (point.DistanceAlongPathMeters + PassedPointSlackMeters < progress.DistanceAlongPathMeters)
                    continue;

                if (point.DistanceAlongPathMeters >= desiredDistanceAlong)
                {
                    bestForward = point;
                    break;
                }
            }

            if (bestForward is not null)
                return bestForward;

            /*
             * Eğer lookahead mesafesi path sonunu aşıyorsa, ileride kalan son noktayı seç.
             * Burada geride kalan gate noktalarını tekrar seçmiyoruz.
             */
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];

                if (!IsPointClearlyBehindProgress(point, progress) &&
                    point.DistanceAlongPathMeters + PassedPointSlackMeters >= progress.DistanceAlongPathMeters)
                {
                    return point;
                }
            }

            return final;
        }

        private static bool IsPointClearlyBehindProgress(
            TrajectoryPoint point,
            PathProgress progress)
        {
            if (progress.SegmentIndex < 0)
                return false;

            /*
             * Noktanın path distance'ı aracın projection ilerlemesinden belirgin şekilde
             * gerideyse bu nokta lookahead olamaz.
             */
            return point.DistanceAlongPathMeters < progress.DistanceAlongPathMeters - PassedPointSlackMeters;
        }

        private static PathProgress EstimatePathProgress(
            Vec3 vehicle,
            IReadOnlyList<TrajectoryPoint> points)
        {
            if (points.Count == 0)
                return new PathProgress(
                    SegmentIndex: -1,
                    SegmentT: 0.0,
                    DistanceAlongPathMeters: 0.0,
                    CrossTrackErrorMeters: double.PositiveInfinity);

            if (points.Count == 1)
            {
                return new PathProgress(
                    SegmentIndex: 0,
                    SegmentT: 0.0,
                    DistanceAlongPathMeters: points[0].DistanceAlongPathMeters,
                    CrossTrackErrorMeters: Distance(vehicle, points[0].Position));
            }

            var bestSegment = 0;
            var bestT = 0.0;
            var bestDistance = double.PositiveInfinity;
            var bestAlong = 0.0;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];

                var projection = ProjectPointToSegment2D(
                    vehicle,
                    a.Position,
                    b.Position);

                var segmentLength = Math.Max(
                    1e-6,
                    Distance2D(a.Position, b.Position));

                var along =
                    a.DistanceAlongPathMeters +
                    projection.T * segmentLength;

                if (projection.DistanceMeters < bestDistance)
                {
                    bestDistance = projection.DistanceMeters;
                    bestSegment = i;
                    bestT = projection.T;
                    bestAlong = along;
                }
            }

            return new PathProgress(
                SegmentIndex: bestSegment,
                SegmentT: bestT,
                DistanceAlongPathMeters: Math.Max(0.0, bestAlong),
                CrossTrackErrorMeters: bestDistance);
        }

        private static SegmentProjection ProjectPointToSegment2D(
            Vec3 point,
            Vec3 a,
            Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var segmentLengthSq = vx * vx + vy * vy;

            if (segmentLengthSq <= 1e-12)
            {
                return new SegmentProjection(
                    T: 0.0,
                    DistanceMeters: Distance2D(point, a));
            }

            var t = (vx * wx + vy * wy) / segmentLengthSq;
            t = Math.Clamp(t, 0.0, 1.0);

            var px = a.X + t * vx;
            var py = a.Y + t * vy;

            var dx = point.X - px;
            var dy = point.Y - py;

            return new SegmentProjection(
                T: t,
                DistanceMeters: Math.Sqrt(dx * dx + dy * dy));
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

        private static double Distance2D(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
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

        private readonly record struct PathProgress(
            int SegmentIndex,
            double SegmentT,
            double DistanceAlongPathMeters,
            double CrossTrackErrorMeters);

        private readonly record struct SegmentProjection(
            double T,
            double DistanceMeters);
    }
}