using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// Hydronom simÃ¼lasyon dÃ¼nyasÄ±nÄ±n ana state modeli.
    ///
    /// Bu model araÃ§tan baÄŸÄ±msÄ±z olarak dÃ¼nyanÄ±n kendisini temsil eder:
    /// - 3D engeller
    /// - gÃ¶rev hedefleri
    /// - no-go zone
    /// - inspection zone
    /// - Ã§evresel alanlar
    /// - Ops'ta Ã§izilecek layer yapÄ±sÄ±
    /// </summary>
    public readonly record struct SimWorldState(
        string WorldId,
        string DisplayName,
        DateTime TimestampUtc,
        SimWorldBounds Bounds,
        IReadOnlyList<SimWorldObject> Objects,
        IReadOnlyList<SimWorldLayer> Layers,
        string FrameId,
        string Version,
        string Summary
    )
    {
        public static SimWorldState Empty(string worldId = "default_world")
        {
            return new SimWorldState(
                WorldId: Normalize(worldId, "default_world"),
                DisplayName: "Default Sim World",
                TimestampUtc: DateTime.UtcNow,
                Bounds: SimWorldBounds.Unbounded,
                Objects: Array.Empty<SimWorldObject>(),
                Layers: Array.Empty<SimWorldLayer>(),
                FrameId: "world",
                Version: "1.0.0",
                Summary: "Empty simulation world."
            );
        }

        public SimWorldState Sanitized()
        {
            return new SimWorldState(
                WorldId: Normalize(WorldId, "default_world"),
                DisplayName: Normalize(DisplayName, "Sim World"),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Bounds: Bounds.Sanitized(),
                Objects: NormalizeObjects(Objects),
                Layers: NormalizeLayers(Layers),
                FrameId: Normalize(FrameId, "world"),
                Version: Normalize(Version, "1.0.0"),
                Summary: Normalize(Summary, "Simulation world.")
            );
        }

        public SimWorldState WithObject(SimWorldObject obj, string? layerId = null)
        {
            var safe = Sanitized();
            var safeObj = obj.Sanitized();

            var objects = safe.Objects
                .Where(x => !string.Equals(x.ObjectId, safeObj.ObjectId, StringComparison.OrdinalIgnoreCase))
                .Append(safeObj)
                .ToArray();

            var layers = safe.Layers;

            if (!string.IsNullOrWhiteSpace(layerId))
            {
                var layerKey = layerId.Trim();
                var found = false;
                var nextLayers = new List<SimWorldLayer>();

                foreach (var layer in layers)
                {
                    if (string.Equals(layer.LayerId, layerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        nextLayers.Add(layer.WithObject(safeObj.ObjectId).Sanitized());
                        found = true;
                    }
                    else
                    {
                        nextLayers.Add(layer.Sanitized());
                    }
                }

                if (!found)
                {
                    nextLayers.Add(
                        SimWorldLayer
                            .Create(layerKey, layerKey, SimWorldLayerKind.Custom)
                            .WithObject(safeObj.ObjectId)
                            .Sanitized()
                    );
                }

                layers = nextLayers.ToArray();
            }

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                Objects = objects,
                Layers = layers,
                Summary = $"World contains {objects.Length} objects and {layers.Count} layers."
            };
        }

        public SimWorldState WithoutObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return this;

            var safe = Sanitized();
            var id = objectId.Trim();

            var objects = safe.Objects
                .Where(x => !string.Equals(x.ObjectId, id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var layers = safe.Layers
                .Select(layer => layer.WithoutObject(id).Sanitized())
                .ToArray();

            return safe with
            {
                TimestampUtc = DateTime.UtcNow,
                Objects = objects,
                Layers = layers,
                Summary = $"World contains {objects.Length} objects and {layers.Length} layers."
            };
        }

        public SimWorldObject? FindObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId) || Objects is null)
                return null;

            return Objects.FirstOrDefault(
                x => string.Equals(x.ObjectId, objectId.Trim(), StringComparison.OrdinalIgnoreCase)
            );
        }

        public IReadOnlyList<SimWorldObject> GetVisibleObjects()
        {
            var safe = Sanitized();

            return safe.Objects
                .Where(x => x.VisibleInOps && x.State != SimWorldObjectState.Hidden)
                .ToArray();
        }

        public IReadOnlyList<SimWorldObject> GetDetectableObjects()
        {
            var safe = Sanitized();

            return safe.Objects
                .Where(x => x.Detectable && x.State == SimWorldObjectState.Active)
                .ToArray();
        }

        private static IReadOnlyList<SimWorldObject> NormalizeObjects(IReadOnlyList<SimWorldObject>? objects)
        {
            if (objects is null || objects.Count == 0)
                return Array.Empty<SimWorldObject>();

            return objects
                .Select(x => x.Sanitized())
                .GroupBy(x => x.ObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToArray();
        }

        private static IReadOnlyList<SimWorldLayer> NormalizeLayers(IReadOnlyList<SimWorldLayer>? layers)
        {
            if (layers is null || layers.Count == 0)
                return Array.Empty<SimWorldLayer>();

            return layers
                .Select(x => x.Sanitized())
                .GroupBy(x => x.LayerId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(x => x.DrawOrder)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
