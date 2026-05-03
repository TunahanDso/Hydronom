using System;
using System.Collections.Generic;
using Hydronom.Core.Simulation.Environment;
using Hydronom.Core.Simulation.MissionObjects;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Runtime.Simulation.World
{
    /// <summary>
    /// Runtime tarafÄ±nda aktif simÃ¼lasyon dÃ¼nyasÄ±nÄ± temsil eden ana model.
    ///
    /// Bu yapÄ± Core'daki SimWorldState'i, environment state'i ve mission object listesini
    /// tek runtime paketi olarak bir arada tutar.
    /// </summary>
    public readonly record struct RuntimeSimWorld(
        string RuntimeWorldId,
        DateTime TimestampUtc,
        SimWorldState World,
        SimEnvironmentState Environment,
        IReadOnlyList<SimMissionObject> MissionObjects,
        bool Enabled,
        string Source,
        string Summary
    )
    {
        public static RuntimeSimWorld CreateDefaultMarine(string runtimeWorldId = "runtime_world")
        {
            var world = SimWorldState.Empty(runtimeWorldId);

            return new RuntimeSimWorld(
                RuntimeWorldId: Normalize(runtimeWorldId, "runtime_world"),
                TimestampUtc: DateTime.UtcNow,
                World: world,
                Environment: SimEnvironmentState.DefaultMarine,
                MissionObjects: Array.Empty<SimMissionObject>(),
                Enabled: true,
                Source: "RuntimeSimWorld",
                Summary: "Default marine runtime simulation world."
            ).Sanitized();
        }

        public static RuntimeSimWorld CreateEmpty(string runtimeWorldId = "runtime_world")
        {
            var world = SimWorldState.Empty(runtimeWorldId);

            return new RuntimeSimWorld(
                RuntimeWorldId: Normalize(runtimeWorldId, "runtime_world"),
                TimestampUtc: DateTime.UtcNow,
                World: world,
                Environment: SimEnvironmentState.DefaultMarine,
                MissionObjects: Array.Empty<SimMissionObject>(),
                Enabled: true,
                Source: "RuntimeSimWorld",
                Summary: "Empty runtime simulation world."
            ).Sanitized();
        }

        public RuntimeSimWorld Sanitized()
        {
            var world = World.Sanitized();
            var missionObjects = NormalizeMissionObjects(MissionObjects);

            return new RuntimeSimWorld(
                RuntimeWorldId: Normalize(RuntimeWorldId, world.WorldId),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                World: world,
                Environment: Environment.Sanitized(),
                MissionObjects: missionObjects,
                Enabled: Enabled,
                Source: Normalize(Source, "RuntimeSimWorld"),
                Summary: Normalize(Summary, BuildSummary(world, missionObjects.Count))
            );
        }

        public RuntimeSimWorld WithWorld(SimWorldState world)
        {
            var safeWorld = world.Sanitized();

            return this with
            {
                TimestampUtc = DateTime.UtcNow,
                World = safeWorld,
                Summary = BuildSummary(safeWorld, MissionObjects?.Count ?? 0)
            };
        }

        public RuntimeSimWorld WithEnvironment(SimEnvironmentState environment)
        {
            return this with
            {
                TimestampUtc = DateTime.UtcNow,
                Environment = environment.Sanitized()
            };
        }

        public RuntimeSimWorld WithMissionObjects(IReadOnlyList<SimMissionObject> missionObjects)
        {
            var safeMissionObjects = NormalizeMissionObjects(missionObjects);

            return this with
            {
                TimestampUtc = DateTime.UtcNow,
                MissionObjects = safeMissionObjects,
                Summary = BuildSummary(World.Sanitized(), safeMissionObjects.Count)
            };
        }

        public RuntimeSimWorld WithObject(SimWorldObject obj, string? layerId = null)
        {
            var safe = Sanitized();

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                World = safe.World.WithObject(obj, layerId)
            };
        }

        public RuntimeSimWorld WithoutObject(string objectId)
        {
            var safe = Sanitized();

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                World = safe.World.WithoutObject(objectId)
            };
        }

        public RuntimeSimWorld WithMissionObject(SimMissionObject missionObject, string layerId = "mission_objects")
        {
            var safe = Sanitized();
            var safeMission = missionObject.Sanitized();

            var missions = new List<SimMissionObject>();

            foreach (var existing in safe.MissionObjects)
            {
                if (!string.Equals(existing.MissionObjectId, safeMission.MissionObjectId, StringComparison.OrdinalIgnoreCase))
                    missions.Add(existing);
            }

            missions.Add(safeMission);

            var world = safe.World.WithObject(safeMission.ToWorldObject(), layerId);

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                World = world,
                MissionObjects = missions.ToArray(),
                Summary = BuildSummary(world, missions.Count)
            };
        }

        public RuntimeSimWorld WithoutMissionObject(string missionObjectId)
        {
            if (string.IsNullOrWhiteSpace(missionObjectId))
                return this;

            var safe = Sanitized();
            var id = missionObjectId.Trim();

            var missions = new List<SimMissionObject>();

            foreach (var mission in safe.MissionObjects)
            {
                if (!string.Equals(mission.MissionObjectId, id, StringComparison.OrdinalIgnoreCase))
                    missions.Add(mission);
            }

            var world = safe.World.WithoutObject(id);

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                World = world,
                MissionObjects = missions.ToArray(),
                Summary = BuildSummary(world, missions.Count)
            };
        }

        private static IReadOnlyList<SimMissionObject> NormalizeMissionObjects(
            IReadOnlyList<SimMissionObject>? missionObjects
        )
        {
            if (missionObjects is null || missionObjects.Count == 0)
                return Array.Empty<SimMissionObject>();

            var dict = new Dictionary<string, SimMissionObject>(StringComparer.OrdinalIgnoreCase);

            foreach (var missionObject in missionObjects)
            {
                var safe = missionObject.Sanitized();
                dict[safe.MissionObjectId] = safe;
            }

            return new List<SimMissionObject>(dict.Values).ToArray();
        }

        private static string BuildSummary(SimWorldState world, int missionObjectCount)
        {
            var safe = world.Sanitized();

            return $"Runtime world={safe.WorldId}, objects={safe.Objects.Count}, layers={safe.Layers.Count}, missionObjects={missionObjectCount}";
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
