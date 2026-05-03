using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// Ä°nceleme/tarama bÃ¶lgesi.
    ///
    /// Kamera, sonar, lidar veya Ã§evresel sensÃ¶rlerle belirli bir alanÄ± tarama gÃ¶revleri iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimInspectionZone(
        SimMissionObject MissionObject,
        SimShape2D Area2D,
        double RequiredCoveragePercent,
        double MaxSpeedMps,
        bool RequiresSensorSweep
    )
    {
        public static SimInspectionZone Create(
            string id,
            string name,
            SimShape2D area,
            double requiredCoveragePercent = 80.0
        )
        {
            var worldObject = SimWorldObject.Create2D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.InspectionZone,
                shape: area,
                material: SimWorldMaterial.MissionTarget with
                {
                    Opacity = 0.25,
                    IsCollidable = false
                }
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("inspection_zone"),
                    SimWorldTag.Create("mission_area")
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.InspectionZone, worldObject, "inspection_sweep")
                .WithCapabilities("local_navigation", "area_coverage");

            return new SimInspectionZone(
                MissionObject: missionObject,
                Area2D: area.Sanitized(),
                RequiredCoveragePercent: Clamp(requiredCoveragePercent, 0.0, 100.0),
                MaxSpeedMps: 1.0,
                RequiresSensorSweep: true
            );
        }

        public SimInspectionZone Sanitized()
        {
            return new SimInspectionZone(
                MissionObject: MissionObject.Sanitized(),
                Area2D: Area2D.Sanitized(),
                RequiredCoveragePercent: Clamp(RequiredCoveragePercent, 0.0, 100.0),
                MaxSpeedMps: SafePositive(MaxSpeedMps, 1.0),
                RequiresSensorSweep: RequiresSensorSweep
            );
        }

        public bool Contains(SimVector2 point)
        {
            return Area2D.Contains(point);
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

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
