using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// GÃ¶rev hedef nesnesi.
    ///
    /// Target; varÄ±ÅŸ noktasÄ±, takip hedefi, tespit hedefi veya gÃ¶rev tamamlanma noktasÄ±
    /// olarak kullanÄ±labilir.
    /// </summary>
    public readonly record struct SimTargetObject(
        SimMissionObject MissionObject,
        double AcceptanceRadiusMeters,
        bool RequiresVisualConfirmation,
        bool RequiresPhysicalArrival
    )
    {
        public static SimTargetObject Create(
            string id,
            string name,
            SimVector3 position,
            double acceptanceRadiusMeters = 1.0
        )
        {
            var sphere = new SimSphere(position, acceptanceRadiusMeters);

            var worldObject = SimWorldObject.Create3D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.Target,
                shape: sphere,
                material: SimWorldMaterial.MissionTarget
            ).WithTransform(
                SimWorldTransform.Identity with
                {
                    Pose = new SimWorldPose(position, SimQuaternion.Identity, "world")
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.Target, worldObject, "target")
                .WithCapabilities("position_estimation", "heading_estimation");

            return new SimTargetObject(
                MissionObject: missionObject,
                AcceptanceRadiusMeters: SafePositive(acceptanceRadiusMeters, 1.0),
                RequiresVisualConfirmation: false,
                RequiresPhysicalArrival: true
            );
        }

        public SimTargetObject Sanitized()
        {
            return new SimTargetObject(
                MissionObject: MissionObject.Sanitized(),
                AcceptanceRadiusMeters: SafePositive(AcceptanceRadiusMeters, 1.0),
                RequiresVisualConfirmation: RequiresVisualConfirmation,
                RequiresPhysicalArrival: RequiresPhysicalArrival
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
