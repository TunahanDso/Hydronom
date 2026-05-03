using System;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimWorldProvider iÃ§in diagnostics/telemetry snapshot modeli.
    /// </summary>
    public readonly record struct SimWorldSnapshot(
        DateTime TimestampUtc,
        string ProviderName,
        string WorldId,
        int ObjectCount,
        int VisibleObjectCount,
        int DetectableObjectCount,
        int LayerCount,
        bool IsAvailable,
        string Summary,
        SimWorldState World
    )
    {
        public static SimWorldSnapshot FromWorld(
            string providerName,
            bool isAvailable,
            SimWorldState world
        )
        {
            var safe = world.Sanitized();

            return new SimWorldSnapshot(
                TimestampUtc: DateTime.UtcNow,
                ProviderName: string.IsNullOrWhiteSpace(providerName) ? "SimWorldProvider" : providerName.Trim(),
                WorldId: safe.WorldId,
                ObjectCount: safe.Objects.Count,
                VisibleObjectCount: safe.GetVisibleObjects().Count,
                DetectableObjectCount: safe.GetDetectableObjects().Count,
                LayerCount: safe.Layers.Count,
                IsAvailable: isAvailable,
                Summary: BuildSummary(isAvailable, safe),
                World: safe
            );
        }

        private static string BuildSummary(bool isAvailable, SimWorldState world)
        {
            if (!isAvailable)
                return $"World provider unavailable. WorldId={world.WorldId}";

            return $"World={world.WorldId}, objects={world.Objects.Count}, layers={world.Layers.Count}, visible={world.GetVisibleObjects().Count}";
        }
    }
}
