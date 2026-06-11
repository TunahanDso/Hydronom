using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// Runtime/scenario world object modelini planner semantiğine çevirir.
    ///
    /// Bu sınıfın amacı:
    /// - Gate/corridor/boundary marker'larını gerçek rota kesen engellerden ayırmak.
    /// - Duba/blocker/obstacle/no-go gibi objeleri planner için fiziksel engel yapmak.
    /// - Araç radius + safety margin ile inflated radius hesaplamak.
    /// - Planner'ın raw HydronomWorldObject string/kind yorumlarına gömülmesini engellemek.
    /// </summary>
    public static class WorldObjectPlanningClassifier
    {
        public static IReadOnlyList<PlanningObstacle> Classify(
            IEnumerable<HydronomWorldObject> worldObjects,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            string source = "world-model")
        {
            if (worldObjects is null)
                return Array.Empty<PlanningObstacle>();

            var vehicleRadius = Math.Max(0.0, vehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, safetyMarginMeters);

            return worldObjects
                .Where(x => x is not null)
                .Select(x => Classify(
                    x,
                    vehicleRadius,
                    safetyMargin,
                    source))
                .Where(x => x.IsActive)
                .ToArray();
        }

        public static PlanningObstacle Classify(
            HydronomWorldObject obj,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            string source = "world-model")
        {
            if (obj is null)
                return PlanningObstacle.Empty;

            var id = obj.Id ?? string.Empty;
            var kind = obj.Kind ?? string.Empty;
            var name = obj.Name ?? string.Empty;
            var layer = obj.Layer ?? string.Empty;

            var radius = ResolvePhysicalRadius(obj);
            var vehicleRadius = Math.Max(0.0, vehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, safetyMarginMeters);

            var isGateMarker = IsGateMarker(obj);
            var isCorridorMarker = IsCorridorMarker(obj);
            var isBoundaryLike = IsBoundaryLike(obj);
            var isNoGoZone = IsNoGoZone(obj);
            var isHintMarker = IsHintMarker(obj);
            var isExplicitObstacle = IsExplicitObstacle(obj);
            var isBuoyLike = IsBuoyLike(obj);
            var isBlockerLike = IsBlockerLike(obj);
            var isPassableMarker = isGateMarker || isCorridorMarker || isBoundaryLike || isHintMarker;

            /*
             * Güvenli varsayım:
             * Eğer obje açıkça obstacle/blocker/buoy/no-go ise fiziksel engeldir.
             * Gate/corridor marker tek başına rota kesen engel değildir.
             */
            var isPhysicalObstacle =
                obj.IsBlocking ||
                isExplicitObstacle ||
                isBuoyLike ||
                isBlockerLike ||
                isNoGoZone;

            var isUnknownPhysical =
                !isPhysicalObstacle &&
                !isPassableMarker &&
                radius > 0.05 &&
                ContainsAny(id, "object", "unknown", "marker") == false &&
                ContainsAny(kind, "target", "waypoint", "path") == false;

            if (isUnknownPhysical)
                isPhysicalObstacle = true;

            var isBlockingForRoute =
                obj.IsActive &&
                isPhysicalObstacle &&
                !isPassableMarker;

            /*
             * no_go_zone passable marker olamaz.
             */
            if (isNoGoZone)
            {
                isBlockingForRoute = true;
                isPhysicalObstacle = true;
                isPassableMarker = false;
            }

            var planningWeight = ResolvePlanningWeight(
                obj,
                isPhysicalObstacle,
                isBlockingForRoute,
                isNoGoZone,
                isGateMarker,
                isCorridorMarker,
                isBoundaryLike);

            return new PlanningObstacle
            {
                Id = id,
                Kind = kind,
                Name = name,
                Layer = layer,
                Position = new Vec3(obj.X, obj.Y, obj.Z),
                RadiusMeters = radius,
                InflatedRadiusMeters = radius + vehicleRadius + safetyMargin,
                VehicleRadiusMeters = vehicleRadius,
                SafetyMarginMeters = safetyMargin,
                IsActive = obj.IsActive,
                IsPhysicalObstacle = isPhysicalObstacle,
                IsBlockingForRoute = isBlockingForRoute,
                IsPassableMarker = isPassableMarker,
                IsBoundaryLike = isBoundaryLike,
                IsCorridorMarker = isCorridorMarker,
                IsGateMarker = isGateMarker,
                IsNoGoZone = isNoGoZone,
                IsHintMarker = isHintMarker,
                IsUnknownPhysicalObject = isUnknownPhysical,
                PlanningWeight = planningWeight,
                Source = source,
                Raw = obj
            };
        }

        public static IReadOnlyList<PlanningObstacle> BlockingOnly(
            IEnumerable<HydronomWorldObject> worldObjects,
            double vehicleRadiusMeters,
            double safetyMarginMeters,
            string source = "world-model")
        {
            return Classify(
                    worldObjects,
                    vehicleRadiusMeters,
                    safetyMarginMeters,
                    source)
                .Where(x => x.CanBlockRoute)
                .ToArray();
        }

        private static double ResolvePhysicalRadius(
            HydronomWorldObject obj)
        {
            var radius = Math.Max(0.0, obj.Radius);

            /*
             * Bazı senaryo objelerinde radius yerine width/height dolu olabilir.
             * Planner fiziksel yarıçap ister; width/height varsa güvenli çevrel çapa çek.
             */
            var halfWidth = Math.Max(0.0, obj.Width) * 0.5;
            var halfHeight = Math.Max(0.0, obj.Height) * 0.5;
            var boxRadius = Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

            return Math.Max(radius, boxRadius);
        }

        private static bool IsExplicitObstacle(
            HydronomWorldObject obj)
        {
            return obj.IsObstacleLike ||
                   ContainsAny(obj.Kind, "obstacle", "blocker", "buoy", "hazard", "no_go", "nogo") ||
                   ContainsAny(obj.Id, "obstacle", "blocker", "buoy", "hazard", "no_go", "nogo") ||
                   ContainsAny(obj.Name, "obstacle", "blocker", "buoy", "hazard", "no_go", "nogo");
        }

        private static bool IsBuoyLike(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "buoy", "duba") ||
                   ContainsAny(obj.Id, "buoy", "duba") ||
                   ContainsAny(obj.Name, "buoy", "duba");
        }

        private static bool IsBlockerLike(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "blocker", "barrier", "wall") ||
                   ContainsAny(obj.Id, "blocker", "barrier", "wall") ||
                   ContainsAny(obj.Name, "blocker", "barrier", "wall");
        }

        private static bool IsNoGoZone(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "no_go", "nogo", "forbidden", "restricted") ||
                   ContainsAny(obj.Id, "no_go", "nogo", "forbidden", "restricted") ||
                   ContainsAny(obj.Name, "no_go", "nogo", "forbidden", "restricted");
        }

        private static bool IsGateMarker(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "gate") ||
                   ContainsAny(obj.Id, "gate") ||
                   ContainsAny(obj.Name, "gate");
        }

        private static bool IsCorridorMarker(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "corridor", "lane", "path_hint", "guidance") ||
                   ContainsAny(obj.Id, "corridor", "lane", "path_hint", "guidance") ||
                   ContainsAny(obj.Name, "corridor", "lane", "path_hint", "guidance");
        }

        private static bool IsBoundaryLike(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "boundary", "border", "limit", "fence") ||
                   ContainsAny(obj.Id, "boundary", "border", "limit", "fence") ||
                   ContainsAny(obj.Name, "boundary", "border", "limit", "fence");
        }

        private static bool IsHintMarker(
            HydronomWorldObject obj)
        {
            return ContainsAny(obj.Kind, "hint", "debug", "ghost", "virtual") ||
                   ContainsAny(obj.Id, "hint", "debug", "ghost", "virtual") ||
                   ContainsAny(obj.Name, "hint", "debug", "ghost", "virtual");
        }

        private static double ResolvePlanningWeight(
            HydronomWorldObject obj,
            bool isPhysicalObstacle,
            bool isBlockingForRoute,
            bool isNoGoZone,
            bool isGateMarker,
            bool isCorridorMarker,
            bool isBoundaryLike)
        {
            if (!obj.IsActive)
                return 0.0;

            if (isNoGoZone)
                return 5.0;

            if (isBlockingForRoute)
                return 2.0;

            if (isPhysicalObstacle)
                return 1.5;

            if (isBoundaryLike)
                return 0.4;

            if (isGateMarker || isCorridorMarker)
                return 0.2;

            return 1.0;
        }

        private static bool ContainsAny(
            string? value,
            params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (var needle in needles)
            {
                if (value.Contains(
                        needle,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}