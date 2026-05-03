using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// Waypoint gÃ¶rev nesnesi.
    ///
    /// GÃ¶rev planlama, rota Ã§izimi, Ops mission editor ve simulator hedef noktalarÄ± iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimWaypointObject(
        SimMissionObject MissionObject,
        int Order,
        double AcceptanceRadiusMeters,
        double DesiredSpeedMps,
        bool HoldAtWaypoint,
        double HoldSeconds
    )
    {
        public static SimWaypointObject Create(
            string id,
            string name,
            SimVector3 position,
            int order,
            double acceptanceRadiusMeters = 1.0
        )
        {
            var shape = new SimSphere(position, acceptanceRadiusMeters);

            var worldObject = SimWorldObject.Create3D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.Waypoint,
                shape: shape,
                material: SimWorldMaterial.MissionTarget
            ).WithTransform(
                SimWorldTransform.Identity with
                {
                    Pose = new SimWorldPose(position, SimQuaternion.Identity, "world")
                }
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("waypoint"),
                    SimWorldTag.Create("order", order.ToString())
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.Waypoint, worldObject, "waypoint_navigation")
                .WithCapabilities("position_estimation", "heading_estimation");

            return new SimWaypointObject(
                MissionObject: missionObject,
                Order: order < 0 ? 0 : order,
                AcceptanceRadiusMeters: SafePositive(acceptanceRadiusMeters, 1.0),
                DesiredSpeedMps: 1.0,
                HoldAtWaypoint: false,
                HoldSeconds: 0.0
            );
        }

        public SimWaypointObject Sanitized()
        {
            return new SimWaypointObject(
                MissionObject: MissionObject.Sanitized(),
                Order: Order < 0 ? 0 : Order,
                AcceptanceRadiusMeters: SafePositive(AcceptanceRadiusMeters, 1.0),
                DesiredSpeedMps: SafeNonNegative(DesiredSpeedMps),
                HoldAtWaypoint: HoldAtWaypoint,
                HoldSeconds: SafeNonNegative(HoldSeconds)
            );
        }

        public SimWorldObject ToWorldObject()
        {
            return MissionObject.ToWorldObject();
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
