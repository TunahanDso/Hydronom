using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// Girilmemesi gereken bÃ¶lge.
    ///
    /// Bu model safety, planner, Ops ve simulator tarafÄ±ndan ortak kullanÄ±labilir.
    /// </summary>
    public readonly record struct SimNoGoZone(
        SimMissionObject MissionObject,
        SimShape2D Area2D,
        double SafetyMarginMeters,
        bool HardForbidden,
        string ViolationAction
    )
    {
        public static SimNoGoZone Create(
            string id,
            string name,
            SimShape2D area,
            double safetyMarginMeters = 1.0,
            bool hardForbidden = true
        )
        {
            var worldObject = SimWorldObject.Create2D(
                objectId: id,
                displayName: name,
                kind: SimWorldObjectKind.NoGoZone,
                shape: area,
                material: SimWorldMaterial.Obstacle with
                {
                    Opacity = 0.35,
                    IsCollidable = false
                }
            ).WithTags(
                new[]
                {
                    SimWorldTag.Create("no_go"),
                    SimWorldTag.Create("safety_zone")
                }
            );

            var missionObject = SimMissionObject
                .FromWorldObject(id, name, SimMissionObjectKind.NoGoZone, worldObject, "avoid_zone")
                .WithCapabilities("local_navigation", "path_planning");

            return new SimNoGoZone(
                MissionObject: missionObject,
                Area2D: area.Sanitized(),
                SafetyMarginMeters: SafeNonNegative(safetyMarginMeters),
                HardForbidden: hardForbidden,
                ViolationAction: hardForbidden ? "STOP_OR_REPLAN" : "WARN_AND_REPLAN"
            );
        }

        public SimNoGoZone Sanitized()
        {
            return new SimNoGoZone(
                MissionObject: MissionObject.Sanitized(),
                Area2D: Area2D.Sanitized(),
                SafetyMarginMeters: SafeNonNegative(SafetyMarginMeters),
                HardForbidden: HardForbidden,
                ViolationAction: Normalize(ViolationAction, HardForbidden ? "STOP_OR_REPLAN" : "WARN_AND_REPLAN")
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

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
