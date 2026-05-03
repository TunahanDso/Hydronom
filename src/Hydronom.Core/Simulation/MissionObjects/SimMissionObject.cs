using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.MissionObjects
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nda gÃ¶rev anlamÄ± taÅŸÄ±yan temel nesne.
    ///
    /// Bu model Hydronom'un Ã¶zel gÃ¶revlerini basitleÅŸtirmek iÃ§in kullanÄ±lÄ±r:
    /// - hedefe git
    /// - ÅŸamandÄ±radan geÃ§
    /// - kapÄ±dan geÃ§
    /// - dock'a yaklaÅŸ
    /// - bÃ¶lgeyi tara
    /// - no-go zone'dan kaÃ§
    /// - inspection zone iÃ§inde gÃ¶rev yap
    /// </summary>
    public readonly record struct SimMissionObject(
        string MissionObjectId,
        string DisplayName,
        SimMissionObjectKind Kind,
        SimWorldObject WorldObject,
        string TaskHint,
        double Priority,
        double RequiredAccuracyMeters,
        bool Required,
        bool Completed,
        IReadOnlyList<string> RequiredCapabilities,
        IReadOnlyList<string> Tags,
        DateTime CreatedUtc,
        DateTime UpdatedUtc
    )
    {
        public static SimMissionObject FromWorldObject(
            string missionObjectId,
            string displayName,
            SimMissionObjectKind kind,
            SimWorldObject worldObject,
            string taskHint = "none"
        )
        {
            var now = DateTime.UtcNow;

            return new SimMissionObject(
                MissionObjectId: Normalize(missionObjectId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(displayName, "Mission Object"),
                Kind: kind,
                WorldObject: worldObject.Sanitized(),
                TaskHint: Normalize(taskHint, "none"),
                Priority: 0.5,
                RequiredAccuracyMeters: 1.0,
                Required: true,
                Completed: false,
                RequiredCapabilities: Array.Empty<string>(),
                Tags: Array.Empty<string>(),
                CreatedUtc: now,
                UpdatedUtc: now
            ).Sanitized();
        }

        public SimMissionObject Sanitized()
        {
            var now = DateTime.UtcNow;

            return new SimMissionObject(
                MissionObjectId: Normalize(MissionObjectId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(DisplayName, "Mission Object"),
                Kind: Kind,
                WorldObject: WorldObject.Sanitized(),
                TaskHint: Normalize(TaskHint, "none"),
                Priority: Clamp01(Priority),
                RequiredAccuracyMeters: SafePositive(RequiredAccuracyMeters, 1.0),
                Required: Required,
                Completed: Completed,
                RequiredCapabilities: NormalizeList(RequiredCapabilities),
                Tags: NormalizeList(Tags),
                CreatedUtc: CreatedUtc == default ? now : CreatedUtc,
                UpdatedUtc: UpdatedUtc == default ? now : UpdatedUtc
            );
        }

        public SimMissionObject MarkCompleted()
        {
            return this with
            {
                Completed = true,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimMissionObject MarkIncomplete()
        {
            return this with
            {
                Completed = false,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimMissionObject WithCapabilities(params string[] capabilities)
        {
            return this with
            {
                RequiredCapabilities = NormalizeList(capabilities),
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimMissionObject WithTags(params string[] tags)
        {
            return this with
            {
                Tags = NormalizeList(tags),
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimWorldObject ToWorldObject()
        {
            return WorldObject.Sanitized();
        }

        public SimVector3 Position => WorldObject.Transform.Pose.Position;

        public SimBox ApproxBounds => WorldObject.GetApproxBounds3D();

        private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values)
        {
            if (values is null)
                return Array.Empty<string>();

            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}
