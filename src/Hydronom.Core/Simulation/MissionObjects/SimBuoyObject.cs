using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// ÅamandÄ±ra gÃ¶rev nesnesi.
    ///
    /// Bu nesne yarÄ±ÅŸma gÃ¶revlerinde hedef tespiti, geÃ§iÅŸ, etrafÄ±ndan dolaÅŸma,
    /// renk/etiket tanÄ±ma ve gÃ¶rsel navigasyon gÃ¶revleri iÃ§in kullanÄ±labilir.
    /// </summary>
    public readonly record struct SimBuoyObject(
        SimMissionObject MissionObject,
        string BuoyColor,
        string BuoyLabel,
        double DetectionRadiusMeters,
        bool RequiresVisionDetection
    )
    {
        public static SimBuoyObject Create(
            string id,
            string name,
            SimVector3 position,
            string color = "red",
            string label = ""
        )
        {
            var shape = new SimCylinder(
                Center: position,
                Radius: 0.25,
                Height: 1.0,
                Rotation: SimQuaternion.Identity
            );

            var material = SimWorldMaterial.MissionTarget with
            {
                MaterialId = "buoy_" + Normalize(color, "red"),
                DisplayName = "Buoy " + Normalize(color, "red"),
                ColorHex = ColorToHex(color)
            };

            var worldObject = SimWorldObject.Create3D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.Buoy,
                shape: shape,
                material: material
            ).WithTransform(
                SimWorldTransform.Identity with
                {
                    Pose = new SimWorldPose(position, SimQuaternion.Identity, "world")
                }
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("buoy"),
                    SimWorldTag.Create("color", Normalize(color, "red"))
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.Buoy, worldObject, "buoy_detection")
                .WithCapabilities("visual_target_detection", "position_estimation");

            return new SimBuoyObject(
                MissionObject: missionObject,
                BuoyColor: Normalize(color, "red"),
                BuoyLabel: Normalize(label, ""),
                DetectionRadiusMeters: 8.0,
                RequiresVisionDetection: true
            );
        }

        public SimBuoyObject Sanitized()
        {
            return new SimBuoyObject(
                MissionObject: MissionObject.Sanitized(),
                BuoyColor: Normalize(BuoyColor, "red"),
                BuoyLabel: Normalize(BuoyLabel, ""),
                DetectionRadiusMeters: SafePositive(DetectionRadiusMeters, 8.0),
                RequiresVisionDetection: RequiresVisionDetection
            );
        }

        public SimWorldObject ToWorldObject()
        {
            return MissionObject.ToWorldObject();
        }

        private static string ColorToHex(string color)
        {
            var c = Normalize(color, "red").ToLowerInvariant();

            return c switch
            {
                "red" => "#EF4444",
                "green" => "#22C55E",
                "blue" => "#3B82F6",
                "yellow" => "#EAB308",
                "orange" => "#F97316",
                "white" => "#F9FAFB",
                "black" => "#111827",
                _ => "#EF4444"
            };
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
