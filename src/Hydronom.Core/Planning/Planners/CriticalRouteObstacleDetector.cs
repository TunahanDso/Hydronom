using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// Start-target segmentini kesen veya güvenlik bandını ihlal eden world obstacles'ı bulur.
    ///
    /// Bu sınıf planner'ın "biraz sağdan/ soldan al" davranışının temelidir.
    /// Çünkü bypass kararı ancak hangi engelin route'u bozduğunu biliyorsak üretilebilir.
    /// </summary>
    public static class CriticalRouteObstacleDetector
    {
        public static IReadOnlyList<CriticalRouteObstacle> Detect(
            Vec3 start,
            Vec3 target,
            IEnumerable<PlanningObstacle> obstacles,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            double maxPlanSpeedMps,
            double routeWindowBeforeStart = 0.10,
            double routeWindowAfterTarget = 0.15,
            int maxResults = 8)
        {
            if (obstacles is null)
                return Array.Empty<CriticalRouteObstacle>();

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            var dz = target.Z - start.Z;

            var len2d = Math.Sqrt(dx * dx + dy * dy);
            if (len2d <= 1e-6)
                return Array.Empty<CriticalRouteObstacle>();

            var ux = dx / len2d;
            var uy = dy / len2d;

            var vehicleRadius = Math.Max(0.0, vehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, safetyMarginMeters);
            var speedBuffer = Math.Clamp(maxPlanSpeedMps * 0.22, 0.0, 0.75);

            /*
             * Required physical clearance:
             * - Araç radius zaten physical clearance hesabında ayrıca düşülüyor.
             * - Bu değer, obstacle yüzeyi ile araç gövdesi arasında istenen minimum açıklık.
             */
            var requiredPhysicalClearance = Math.Clamp(
                safetyMargin + 0.85 + speedBuffer,
                1.05,
                2.40);

            var result = new List<CriticalRouteObstacle>();

            foreach (var obstacle in obstacles)
            {
                if (obstacle is null ||
                    !obstacle.CanBlockRoute ||
                    !IsFinite(obstacle.Position.X) ||
                    !IsFinite(obstacle.Position.Y))
                {
                    continue;
                }

                var px = obstacle.Position.X - start.X;
                var py = obstacle.Position.Y - start.Y;

                var distanceAlong = px * ux + py * uy;
                var projectionT = distanceAlong / len2d;
                var clampedT = Math.Clamp(projectionT, 0.0, 1.0);

                var closest = new Vec3(
                    start.X + dx * clampedT,
                    start.Y + dy * clampedT,
                    start.Z + dz * clampedT);

                var distanceToRoute = Distance2D(
                    obstacle.Position,
                    closest);

                var physicalClearance =
                    distanceToRoute -
                    obstacle.RadiusMeters -
                    vehicleRadius;

                var safetyClearance =
                    physicalClearance -
                    safetyMargin;

                var isAhead =
                    projectionT >= -routeWindowBeforeStart;

                var isWithinRouteWindow =
                    projectionT >= -routeWindowBeforeStart &&
                    projectionT <= 1.0 + routeWindowAfterTarget;

                var isCollision =
                    physicalClearance < 0.0;

                var isSafetyViolation =
                    safetyClearance < 0.0;

                var isBlocking =
                    isWithinRouteWindow &&
                    physicalClearance < requiredPhysicalClearance;

                if (!isWithinRouteWindow &&
                    !isCollision &&
                    !isSafetyViolation)
                {
                    continue;
                }

                if (!isBlocking &&
                    !isCollision &&
                    !isSafetyViolation)
                {
                    continue;
                }

                var severity = ComputeSeverity(
                    physicalClearance,
                    safetyClearance,
                    requiredPhysicalClearance,
                    projectionT,
                    isAhead,
                    isWithinRouteWindow,
                    obstacle.PlanningWeight);

                var recommendedOffset = Math.Clamp(
                    obstacle.RadiusMeters +
                    vehicleRadius +
                    safetyMargin +
                    0.75 +
                    speedBuffer,
                    1.50,
                    6.00);

                var reason = ResolveReason(
                    isCollision,
                    isSafetyViolation,
                    isBlocking,
                    isAhead,
                    isWithinRouteWindow);

                result.Add(new CriticalRouteObstacle
                {
                    Obstacle = obstacle,
                    ProjectionT = projectionT,
                    ClampedProjectionT = clampedT,
                    ClosestPointOnRoute = closest,
                    DistanceToRouteMeters = distanceToRoute,
                    PhysicalClearanceMeters = physicalClearance,
                    SafetyClearanceMeters = safetyClearance,
                    RequiredPhysicalClearanceMeters = requiredPhysicalClearance,
                    RecommendedMinOffsetMeters = recommendedOffset,
                    Severity = severity,
                    IsAhead = isAhead,
                    IsWithinRouteWindow = isWithinRouteWindow,
                    IsBlocking = isBlocking,
                    IsCollision = isCollision,
                    IsSafetyViolation = isSafetyViolation,
                    Reason = reason
                });
            }

            return result
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => Math.Abs(x.ProjectionT))
                .Take(Math.Max(1, maxResults))
                .ToArray();
        }

        public static CriticalRouteObstacle? MostCritical(
            Vec3 start,
            Vec3 target,
            IEnumerable<PlanningObstacle> obstacles,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            double maxPlanSpeedMps)
        {
            return Detect(
                    start,
                    target,
                    obstacles,
                    vehicleRadiusMeters,
                    safetyMarginMeters,
                    maxPlanSpeedMps,
                    maxResults: 1)
                .FirstOrDefault();
        }

        private static double ComputeSeverity(
            double physicalClearance,
            double safetyClearance,
            double requiredPhysicalClearance,
            double projectionT,
            bool isAhead,
            bool isWithinRouteWindow,
            double planningWeight)
        {
            var clearanceDeficit = Math.Max(
                0.0,
                requiredPhysicalClearance - physicalClearance);

            var safetyDeficit = Math.Max(
                0.0,
                -safetyClearance);

            var collisionBonus = physicalClearance < 0.0
                ? 2.0 + Math.Abs(physicalClearance)
                : 0.0;

            var windowWeight = isWithinRouteWindow ? 1.0 : 0.35;
            var aheadWeight = isAhead ? 1.0 : 0.45;

            /*
             * projectionT 0..1 arasıysa route üstünde demektir.
             * Target sonrası/gerisi küçük ağırlık alır.
             */
            var projectionWeight =
                projectionT >= 0.0 && projectionT <= 1.0
                    ? 1.0
                    : 0.55;

            var raw =
                clearanceDeficit * 0.75 +
                safetyDeficit * 0.55 +
                collisionBonus;

            raw *= windowWeight;
            raw *= aheadWeight;
            raw *= projectionWeight;
            raw *= Math.Clamp(planningWeight, 0.2, 5.0);

            return Math.Clamp(raw, 0.0, 10.0);
        }

        private static string ResolveReason(
            bool isCollision,
            bool isSafetyViolation,
            bool isBlocking,
            bool isAhead,
            bool isWithinRouteWindow)
        {
            if (isCollision)
                return "ROUTE_COLLISION";

            if (isSafetyViolation)
                return "SAFETY_BAND_VIOLATION";

            if (isBlocking)
                return "REQUIRED_CLEARANCE_VIOLATION";

            if (!isAhead)
                return "BEHIND_ROUTE_WINDOW";

            if (!isWithinRouteWindow)
                return "OUTSIDE_ROUTE_WINDOW";

            return "ROUTE_RISK";
        }

        private static double Distance2D(
            Vec3 a,
            Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsFinite(
            double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}