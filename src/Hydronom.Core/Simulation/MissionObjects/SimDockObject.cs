using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// Dock / yanaÅŸma noktasÄ± nesnesi.
    ///
    /// Deniz aracÄ±, liman robotu, AGV veya taÅŸÄ±ma platformu iÃ§in Ã¶zel yaklaÅŸma/yanaÅŸma
    /// gÃ¶revlerini sadeleÅŸtirmek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimDockObject(
        SimMissionObject MissionObject,
        SimVector3 ApproachPoint,
        SimVector3 DockNormal,
        double ApproachRadiusMeters,
        double MaxApproachSpeedMps,
        bool RequiresPreciseAlignment
    )
    {
        public static SimDockObject Create(
            string id,
            string name,
            SimVector3 dockCenter,
            SimVector3 approachPoint,
            SimVector3 dockNormal,
            double approachRadiusMeters = 2.0
        )
        {
            var shape = new SimBox(
                Center: dockCenter,
                Size: new SimVector3(2.0, 0.5, 0.5),
                Rotation: SimQuaternion.Identity
            );

            var worldObject = SimWorldObject.Create3D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.Dock,
                shape: shape,
                material: SimWorldMaterial.Default
            ).WithTransform(
                SimWorldTransform.Identity with
                {
                    Pose = new SimWorldPose(dockCenter, SimQuaternion.Identity, "world")
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.Dock, worldObject, "dock_approach")
                .WithCapabilities("position_estimation", "heading_estimation", "low_speed_control");

            return new SimDockObject(
                MissionObject: missionObject,
                ApproachPoint: approachPoint.Sanitized(),
                DockNormal: dockNormal.Sanitized().Normalized(),
                ApproachRadiusMeters: SafePositive(approachRadiusMeters, 2.0),
                MaxApproachSpeedMps: 0.4,
                RequiresPreciseAlignment: true
            );
        }

        public SimDockObject Sanitized()
        {
            return new SimDockObject(
                MissionObject: MissionObject.Sanitized(),
                ApproachPoint: ApproachPoint.Sanitized(),
                DockNormal: DockNormal.Sanitized().Normalized(),
                ApproachRadiusMeters: SafePositive(ApproachRadiusMeters, 2.0),
                MaxApproachSpeedMps: SafePositive(MaxApproachSpeedMps, 0.4),
                RequiresPreciseAlignment: RequiresPreciseAlignment
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
    }
}
