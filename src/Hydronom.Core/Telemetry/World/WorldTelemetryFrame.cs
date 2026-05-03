using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.Environment;
using Hydronom.Core.Simulation.MissionObjects;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Core.Telemetry.World
{
    /// <summary>
    /// Hydronom Ops / Gateway / Ground Station iÃ§in ana dÃ¼nya telemetry frame'i.
    ///
    /// Bu frame:
    /// - world metadata
    /// - world objects
    /// - layers
    /// - environment
    /// - mission objects
    /// - summary/diagnostics
    ///
    /// bilgisini tek pakette taÅŸÄ±r.
    /// </summary>
    public readonly record struct WorldTelemetryFrame(
        string FrameId,
        string WorldId,
        string DisplayName,
        DateTime TimestampUtc,
        string Source,
        IReadOnlyList<WorldObjectTelemetry> Objects,
        IReadOnlyList<WorldLayerTelemetry> Layers,
        WorldEnvironmentTelemetry? Environment,
        IReadOnlyList<WorldMissionObjectTelemetry> MissionObjects,
        int ObjectCount,
        int VisibleObjectCount,
        int DetectableObjectCount,
        int LayerCount,
        int MissionObjectCount,
        string Summary
    )
    {
        public static WorldTelemetryFrame FromWorld(
            SimWorldState world,
            SimEnvironmentState? environment = null,
            IReadOnlyList<SimMissionObject>? missionObjects = null,
            string source = "RuntimeSimWorld"
        )
        {
            var safeWorld = world.Sanitized();
            var safeMissionObjects = NormalizeMissionObjects(missionObjects);

            var objects = safeWorld.Objects
                .Select(WorldObjectTelemetry.FromWorldObject)
                .Select(x => x.Sanitized())
                .ToArray();

            var layers = safeWorld.Layers
                .Select(WorldLayerTelemetry.FromLayer)
                .Select(x => x.Sanitized())
                .ToArray();

            var missionTelemetry = safeMissionObjects
                .Select(WorldMissionObjectTelemetry.FromMissionObject)
                .Select(x => x.Sanitized())
                .ToArray();

            var visibleCount = objects.Count(x => x.Visible);
            var detectableCount = objects.Count(x => x.Detectable);

            return new WorldTelemetryFrame(
                FrameId: Guid.NewGuid().ToString("N"),
                WorldId: safeWorld.WorldId,
                DisplayName: safeWorld.DisplayName,
                TimestampUtc: DateTime.UtcNow,
                Source: Normalize(source, "RuntimeSimWorld"),
                Objects: objects,
                Layers: layers,
                Environment: environment.HasValue
                    ? WorldEnvironmentTelemetry.FromEnvironment(environment.Value)
                    : null,
                MissionObjects: missionTelemetry,
                ObjectCount: objects.Length,
                VisibleObjectCount: visibleCount,
                DetectableObjectCount: detectableCount,
                LayerCount: layers.Length,
                MissionObjectCount: missionTelemetry.Length,
                Summary: BuildSummary(safeWorld.WorldId, objects.Length, layers.Length, missionTelemetry.Length)
            ).Sanitized();
        }

        public WorldTelemetryFrame Sanitized()
        {
            var objects = NormalizeObjects(Objects);
            var layers = NormalizeLayers(Layers);
            var missions = NormalizeMissionTelemetry(MissionObjects);

            return new WorldTelemetryFrame(
                FrameId: Normalize(FrameId, Guid.NewGuid().ToString("N")),
                WorldId: Normalize(WorldId, "world"),
                DisplayName: Normalize(DisplayName, "Simulation World"),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Source: Normalize(Source, "RuntimeSimWorld"),
                Objects: objects,
                Layers: layers,
                Environment: Environment?.Sanitized(),
                MissionObjects: missions,
                ObjectCount: objects.Count,
                VisibleObjectCount: objects.Count(x => x.Visible),
                DetectableObjectCount: objects.Count(x => x.Detectable),
                LayerCount: layers.Count,
                MissionObjectCount: missions.Count,
                Summary: Normalize(Summary, BuildSummary(WorldId, objects.Count, layers.Count, missions.Count))
            );
        }

        private static IReadOnlyList<WorldObjectTelemetry> NormalizeObjects(
            IReadOnlyList<WorldObjectTelemetry>? objects
        )
        {
            if (objects is null || objects.Count == 0)
                return Array.Empty<WorldObjectTelemetry>();

            return objects
                .Select(x => x.Sanitized())
                .GroupBy(x => x.ObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();
        }

        private static IReadOnlyList<WorldLayerTelemetry> NormalizeLayers(
            IReadOnlyList<WorldLayerTelemetry>? layers
        )
        {
            if (layers is null || layers.Count == 0)
                return Array.Empty<WorldLayerTelemetry>();

            return layers
                .Select(x => x.Sanitized())
                .GroupBy(x => x.LayerId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(x => x.DrawOrder)
                .ToArray();
        }

        private static IReadOnlyList<SimMissionObject> NormalizeMissionObjects(
            IReadOnlyList<SimMissionObject>? missionObjects
        )
        {
            if (missionObjects is null || missionObjects.Count == 0)
                return Array.Empty<SimMissionObject>();

            return missionObjects
                .Select(x => x.Sanitized())
                .GroupBy(x => x.MissionObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();
        }

        private static IReadOnlyList<WorldMissionObjectTelemetry> NormalizeMissionTelemetry(
            IReadOnlyList<WorldMissionObjectTelemetry>? missionObjects
        )
        {
            if (missionObjects is null || missionObjects.Count == 0)
                return Array.Empty<WorldMissionObjectTelemetry>();

            return missionObjects
                .Select(x => x.Sanitized())
                .GroupBy(x => x.MissionObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();
        }

        private static string BuildSummary(
            string worldId,
            int objectCount,
            int layerCount,
            int missionObjectCount
        )
        {
            return $"World={Normalize(worldId, "world")}, objects={objectCount}, layers={layerCount}, missionObjects={missionObjectCount}";
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
