using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// CriticalRouteObstacle listesinden gerçek sağ/sol bypass adayları üretir.
    ///
    /// Bu sınıf Hydronom'un "biraz sağdan/ soldan al" davranışının geometri çekirdeğidir.
    /// Random fraction detour yerine obstacle merkezinden lateral offset üretir.
    /// </summary>
    public static class ObstacleBypassCandidateFactory
    {
        private static readonly double[] DefaultOffsetMultipliers =
        {
            0.95,
            1.15,
            1.45,
            1.80
        };

        public static IReadOnlyList<ObstacleBypassCandidate> BuildCandidates(
            Vec3 start,
            Vec3 target,
            IEnumerable<CriticalRouteObstacle> criticalObstacles,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            double maxPlanSpeedMps,
            int maxObstacles = 4)
        {
            if (criticalObstacles is null)
                return Array.Empty<ObstacleBypassCandidate>();

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            var dz = target.Z - start.Z;

            var routeLength = Math.Sqrt(dx * dx + dy * dy);
            if (routeLength <= 1e-6)
                return Array.Empty<ObstacleBypassCandidate>();

            var routeDir = new Vec3(
                dx / routeLength,
                dy / routeLength,
                dz / Math.Max(routeLength, 1e-6));

            var leftNormal = new Vec3(
                -routeDir.Y,
                routeDir.X,
                0.0);

            var rightNormal = new Vec3(
                -leftNormal.X,
                -leftNormal.Y,
                0.0);

            var vehicleRadius = Math.Max(0.0, vehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, safetyMarginMeters);
            var speedBuffer = Math.Clamp(maxPlanSpeedMps * 0.22, 0.0, 0.75);

            var candidates = new List<ObstacleBypassCandidate>();

            foreach (var critical in criticalObstacles
                         .Where(x => x is not null)
                         .Where(x => x.RequiresBypass)
                         .OrderByDescending(x => x.Severity)
                         .Take(Math.Max(1, maxObstacles)))
            {
                var baseOffset = ResolveBaseOffset(
                    critical,
                    vehicleRadius,
                    safetyMargin,
                    speedBuffer);

                var forwardBias = ResolveForwardBias(
                    critical,
                    vehicleRadius);

                foreach (var multiplier in DefaultOffsetMultipliers)
                {
                    var lateralOffset = baseOffset * multiplier;

                    AddCandidate(
                        candidates,
                        critical,
                        ObstacleBypassSide.Left,
                        start,
                        target,
                        routeDir,
                        leftNormal,
                        lateralOffset,
                        forwardBias);

                    AddCandidate(
                        candidates,
                        critical,
                        ObstacleBypassSide.Right,
                        start,
                        target,
                        routeDir,
                        rightNormal,
                        lateralOffset,
                        forwardBias);
                }
            }

            return candidates
                .Where(x => x.IsValid)
                .DistinctBy(x =>
                    $"{x.ObstacleId}:{x.Side}:{Math.Round(x.LateralOffsetMeters, 2)}:{Math.Round(x.ForwardBiasMeters, 2)}")
                .ToArray();
        }

        public static IReadOnlyList<ObstacleBypassCandidate> BuildCandidatesForMostCritical(
            Vec3 start,
            Vec3 target,
            IEnumerable<CriticalRouteObstacle> criticalObstacles,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            double maxPlanSpeedMps)
        {
            var mostCritical = criticalObstacles?
                .Where(x => x is not null)
                .Where(x => x.RequiresBypass)
                .OrderByDescending(x => x.Severity)
                .Take(1)
                .ToArray() ?? Array.Empty<CriticalRouteObstacle>();

            return BuildCandidates(
                start,
                target,
                mostCritical,
                vehicleRadiusMeters,
                safetyMarginMeters,
                maxPlanSpeedMps,
                maxObstacles: 1);
        }

        private static void AddCandidate(
            List<ObstacleBypassCandidate> candidates,
            CriticalRouteObstacle critical,
            ObstacleBypassSide side,
            Vec3 start,
            Vec3 target,
            Vec3 routeDir,
            Vec3 lateralDir,
            double lateralOffset,
            double forwardBias)
        {
            var obstacle = critical.Obstacle;
            var center = obstacle.Position;

            /*
             * Bypass point obstacle merkezinden doğar:
             * center + lateral normal * offset + route direction * forward bias
             *
             * Forward bias çok önemli:
             * Araç obstacle'a yaklaştığında bypass noktasının geride kalmasını önler.
             */
            var raw = new Vec3(
                center.X + lateralDir.X * lateralOffset + routeDir.X * forwardBias,
                center.Y + lateralDir.Y * lateralOffset + routeDir.Y * forwardBias,
                center.Z);

            if (!IsPointUsableForward(start, target, raw))
                return;

            candidates.Add(new ObstacleBypassCandidate
            {
                ObstacleId = critical.ObstacleId,
                Side = side,
                Point = raw,
                ObstacleCenter = center,
                RouteDirection = routeDir,
                LateralDirection = lateralDir,
                ProjectionT = critical.ProjectionT,
                LateralOffsetMeters = lateralOffset,
                ForwardBiasMeters = forwardBias,
                RequiredClearanceMeters = critical.RequiredPhysicalClearanceMeters,
                SourcePhysicalClearanceMeters = critical.PhysicalClearanceMeters,
                SourceSafetyClearanceMeters = critical.SafetyClearanceMeters,
                Severity = critical.Severity,
                Reason = critical.Reason
            });
        }

        private static double ResolveBaseOffset(
            CriticalRouteObstacle critical,
            double vehicleRadius,
            double safetyMargin,
            double speedBuffer)
        {
            var obstacleRadius = Math.Max(
                0.0,
                critical.Obstacle.RadiusMeters);

            /*
             * Offset, obstacle yüzeyinden değil obstacle merkezinden verilir.
             * Bu yüzden obstacle radius + vehicle radius + margin + comfort gerekir.
             */
            return Math.Clamp(
                obstacleRadius + vehicleRadius + safetyMargin + 0.75 + speedBuffer,
                1.50,
                6.00);
        }

        private static double ResolveForwardBias(
            CriticalRouteObstacle critical,
            double vehicleRadius)
        {
            var obstacleRadius = Math.Max(
                0.0,
                critical.Obstacle.RadiusMeters);

            /*
             * Çok büyük olursa bypass noktası gereksiz ileri kaçar,
             * çok küçük olursa araç obstacle yanında geriye dönmeye çalışabilir.
             */
            return Math.Clamp(
                obstacleRadius + vehicleRadius * 0.50 + 0.85,
                0.80,
                2.60);
        }

        private static bool IsPointUsableForward(
            Vec3 start,
            Vec3 target,
            Vec3 point)
        {
            var dx = target.X - start.X;
            var dy = target.Y - start.Y;

            var routeLength = Math.Sqrt(dx * dx + dy * dy);
            if (routeLength <= 1e-6)
                return false;

            var ux = dx / routeLength;
            var uy = dy / routeLength;

            var px = point.X - start.X;
            var py = point.Y - start.Y;

            var forwardMeters = px * ux + py * uy;
            var t = forwardMeters / routeLength;

            /*
             * Biraz start gerisi toleransı var ama gerçek geriye dönüş yasak.
             */
            if (t < -0.05 || t > 1.25)
                return false;

            if (forwardMeters < 0.20)
                return false;

            return IsFinite(point.X) &&
                   IsFinite(point.Y);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}