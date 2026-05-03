using System;
using Hydronom.Core.Simulation.MissionObjects;
using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// Runtime tarafÄ±nda sÄ±k kullanÄ±lan dÃ¼nya/gÃ¶rev nesnelerini Ã¼retmek iÃ§in factory.
    ///
    /// Bu sÄ±nÄ±f Ops mission editor, scenario loader ve test senaryolarÄ± iÃ§in ortak Ã¼retim
    /// yardÄ±mcÄ±larÄ± saÄŸlar.
    /// </summary>
    public static class RuntimeWorldObjectFactory
    {
        public static SimWorldObject CreateBoxObstacle(
            string id,
            string name,
            SimVector3 center,
            SimVector3 size
        )
        {
            var shape = new SimBox(
                Center: center.Sanitized(),
                Size: size.Sanitized(),
                Rotation: SimQuaternion.Identity
            );

            return SimWorldObject.Create3D(
                objectId: Normalize(id, Guid.NewGuid().ToString("N")),
                displayName: Normalize(name, "Box Obstacle"),
                kind: SimWorldObjectKind.StaticObstacle,
                shape: shape,
                material: SimWorldMaterial.Obstacle
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("obstacle"),
                    SimWorldTag.Create("static"),
                    SimWorldTag.Create("box")
                }
            );
        }

        public static SimWorldObject CreateSphereObstacle(
            string id,
            string name,
            SimVector3 center,
            double radius
        )
        {
            var shape = new SimSphere(
                Center: center.Sanitized(),
                Radius: SafePositive(radius, 1.0)
            );

            return SimWorldObject.Create3D(
                objectId: Normalize(id, Guid.NewGuid().ToString("N")),
                displayName: Normalize(name, "Sphere Obstacle"),
                kind: SimWorldObjectKind.StaticObstacle,
                shape: shape,
                material: SimWorldMaterial.Obstacle
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("obstacle"),
                    SimWorldTag.Create("static"),
                    SimWorldTag.Create("sphere")
                }
            );
        }

        public static SimWorldObject CreateCylinderObstacle(
            string id,
            string name,
            SimVector3 center,
            double radius,
            double height
        )
        {
            var shape = new SimCylinder(
                Center: center.Sanitized(),
                Radius: SafePositive(radius, 0.5),
                Height: SafePositive(height, 1.0),
                Rotation: SimQuaternion.Identity
            );

            return SimWorldObject.Create3D(
                objectId: Normalize(id, Guid.NewGuid().ToString("N")),
                displayName: Normalize(name, "Cylinder Obstacle"),
                kind: SimWorldObjectKind.StaticObstacle,
                shape: shape,
                material: SimWorldMaterial.Obstacle
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("obstacle"),
                    SimWorldTag.Create("static"),
                    SimWorldTag.Create("cylinder")
                }
            );
        }

        public static SimTargetObject CreateTarget(
            string id,
            string name,
            SimVector3 position,
            double acceptanceRadiusMeters = 1.0
        )
        {
            return SimTargetObject.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "Target"),
                position: position.Sanitized(),
                acceptanceRadiusMeters: acceptanceRadiusMeters
            );
        }

        public static SimWaypointObject CreateWaypoint(
            string id,
            string name,
            SimVector3 position,
            int order,
            double acceptanceRadiusMeters = 1.0
        )
        {
            return SimWaypointObject.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "Waypoint"),
                position: position.Sanitized(),
                order: order,
                acceptanceRadiusMeters: acceptanceRadiusMeters
            );
        }

        public static SimBuoyObject CreateBuoy(
            string id,
            string name,
            SimVector3 position,
            string color = "red",
            string label = ""
        )
        {
            return SimBuoyObject.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "Buoy"),
                position: position.Sanitized(),
                color: color,
                label: label
            );
        }

        public static SimGateObject CreateGate(
            string id,
            string name,
            SimVector3 leftPost,
            SimVector3 rightPost
        )
        {
            return SimGateObject.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "Gate"),
                leftPost: leftPost.Sanitized(),
                rightPost: rightPost.Sanitized()
            );
        }

        public static SimNoGoZone CreateNoGoCircle(
            string id,
            string name,
            SimVector2 center,
            double radius,
            double safetyMarginMeters = 1.0
        )
        {
            var circle = new SimCircle(
                Center: center.Sanitized(),
                Radius: SafePositive(radius, 1.0)
            );

            return SimNoGoZone.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "No-Go Zone"),
                area: circle,
                safetyMarginMeters: safetyMarginMeters,
                hardForbidden: true
            );
        }

        public static SimInspectionZone CreateInspectionCircle(
            string id,
            string name,
            SimVector2 center,
            double radius,
            double requiredCoveragePercent = 80.0
        )
        {
            var circle = new SimCircle(
                Center: center.Sanitized(),
                Radius: SafePositive(radius, 1.0)
            );

            return SimInspectionZone.Create(
                id: Normalize(id, Guid.NewGuid().ToString("N")),
                name: Normalize(name, "Inspection Zone"),
                area: circle,
                requiredCoveragePercent: requiredCoveragePercent
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }
    }
}
