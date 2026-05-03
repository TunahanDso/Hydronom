using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// KapÄ±/geÃ§iÅŸ gÃ¶rev nesnesi.
    ///
    /// Ä°ki ÅŸamandÄ±ra arasÄ±ndan geÃ§me, gate crossing, checkpoint veya yarÄ±ÅŸma kapÄ±sÄ±
    /// gÃ¶revlerini temsil eder.
    /// </summary>
    public readonly record struct SimGateObject(
        SimMissionObject MissionObject,
        SimVector3 LeftPost,
        SimVector3 RightPost,
        double WidthMeters,
        double CrossingToleranceMeters,
        bool RequireCorrectDirection
    )
    {
        public static SimGateObject Create(
            string id,
            string name,
            SimVector3 leftPost,
            SimVector3 rightPost
        )
        {
            var center = (leftPost + rightPost) * 0.5;
            var width = leftPost.DistanceTo(rightPost);

            var shape = new SimBox(
                Center: center,
                Size: new SimVector3(width, 0.25, 1.0),
                Rotation: SimQuaternion.Identity
            );

            var worldObject = SimWorldObject.Create3D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.Gate,
                shape: shape,
                material: SimWorldMaterial.MissionTarget
            ).WithTransform(
                SimWorldTransform.Identity with
                {
                    Pose = new SimWorldPose(center, SimQuaternion.Identity, "world")
                }
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("gate"),
                    SimWorldTag.Create("checkpoint")
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.Gate, worldObject, "gate_crossing")
                .WithCapabilities("position_estimation", "heading_estimation");

            return new SimGateObject(
                MissionObject: missionObject,
                LeftPost: leftPost.Sanitized(),
                RightPost: rightPost.Sanitized(),
                WidthMeters: SafePositive(width, 1.0),
                CrossingToleranceMeters: 0.75,
                RequireCorrectDirection: true
            );
        }

        public SimGateObject Sanitized()
        {
            return new SimGateObject(
                MissionObject: MissionObject.Sanitized(),
                LeftPost: LeftPost.Sanitized(),
                RightPost: RightPost.Sanitized(),
                WidthMeters: SafePositive(WidthMeters, 1.0),
                CrossingToleranceMeters: SafePositive(CrossingToleranceMeters, 0.75),
                RequireCorrectDirection: RequireCorrectDirection
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
