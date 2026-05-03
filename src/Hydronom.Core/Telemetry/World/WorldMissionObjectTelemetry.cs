using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.MissionObjects;

namespace Hydronom.Core.Telemetry.World
{
    /// <summary>
    /// Ops/Gateway/Ground Station iÃ§in gÃ¶rev nesnesi telemetry modeli.
    ///
    /// GÃ¶rev hedefleri, waypoint'ler, no-go zone'lar, inspection zone'lar,
    /// gate ve buoy gibi Ã¶zel gÃ¶rev nesneleri bu modelle UI'a taÅŸÄ±nÄ±r.
    /// </summary>
    public readonly record struct WorldMissionObjectTelemetry(
        string MissionObjectId,
        string DisplayName,
        string Kind,
        string WorldObjectId,
        string TaskHint,
        double Priority,
        double RequiredAccuracyMeters,
        bool Required,
        bool Completed,
        double X,
        double Y,
        double Z,
        IReadOnlyList<string> RequiredCapabilities,
        IReadOnlyList<string> Tags,
        DateTime UpdatedUtc
    )
    {
        public static WorldMissionObjectTelemetry FromMissionObject(SimMissionObject missionObject)
        {
            var safe = missionObject.Sanitized();
            var position = safe.Position;

            return new WorldMissionObjectTelemetry(
                MissionObjectId: safe.MissionObjectId,
                DisplayName: safe.DisplayName,
                Kind: safe.Kind.ToString(),
                WorldObjectId: safe.WorldObject.ObjectId,
                TaskHint: safe.TaskHint,
                Priority: safe.Priority,
                RequiredAccuracyMeters: safe.RequiredAccuracyMeters,
                Required: safe.Required,
                Completed: safe.Completed,
                X: position.X,
                Y: position.Y,
                Z: position.Z,
                RequiredCapabilities: NormalizeStringList(safe.RequiredCapabilities),
                Tags: NormalizeStringList(safe.Tags),
                UpdatedUtc: safe.UpdatedUtc
            );
        }

        public WorldMissionObjectTelemetry Sanitized()
        {
            return new WorldMissionObjectTelemetry(
                MissionObjectId: Normalize(MissionObjectId, "mission_object"),
                DisplayName: Normalize(DisplayName, "Mission Object"),
                Kind: Normalize(Kind, "Unknown"),
                WorldObjectId: Normalize(WorldObjectId, "world_object"),
                TaskHint: Normalize(TaskHint, "none"),
                Priority: Clamp01(Priority),
                RequiredAccuracyMeters: SafePositive(RequiredAccuracyMeters, 1.0),
                Required: Required,
                Completed: Completed,
                X: Safe(X),
                Y: Safe(Y),
                Z: Safe(Z),
                RequiredCapabilities: NormalizeStringList(RequiredCapabilities),
                Tags: NormalizeStringList(Tags),
                UpdatedUtc: UpdatedUtc == default ? DateTime.UtcNow : UpdatedUtc
            );
        }

        private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
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

        private static double Safe(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
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
