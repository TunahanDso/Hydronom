using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±nda bir katman.
    ///
    /// Ops tarafÄ±nda bu katmanlar ayrÄ± ayrÄ± aÃ§Ä±lÄ±p kapatÄ±labilir:
    /// - obstacles
    /// - mission objects
    /// - zones
    /// - environment
    /// - sensor debug
    /// - physics truth
    /// - replay
    /// </summary>
    public readonly record struct SimWorldLayer(
        string LayerId,
        string DisplayName,
        SimWorldLayerKind Kind,
        bool VisibleByDefault,
        bool Locked,
        int DrawOrder,
        IReadOnlyList<string> ObjectIds
    )
    {
        public static SimWorldLayer Create(
            string layerId,
            string displayName,
            SimWorldLayerKind kind,
            bool visibleByDefault = true,
            int drawOrder = 0
        )
        {
            return new SimWorldLayer(
                LayerId: Normalize(layerId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(displayName, "World Layer"),
                Kind: kind,
                VisibleByDefault: visibleByDefault,
                Locked: false,
                DrawOrder: drawOrder,
                ObjectIds: Array.Empty<string>()
            );
        }

        public SimWorldLayer Sanitized()
        {
            return new SimWorldLayer(
                LayerId: Normalize(LayerId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(DisplayName, "World Layer"),
                Kind: Kind,
                VisibleByDefault: VisibleByDefault,
                Locked: Locked,
                DrawOrder: DrawOrder,
                ObjectIds: NormalizeObjectIds(ObjectIds)
            );
        }

        public SimWorldLayer WithObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return this;

            var ids = NormalizeObjectIds(ObjectIds).ToList();
            var id = objectId.Trim();

            if (!ids.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                ids.Add(id);

            return this with
            {
                ObjectIds = ids.ToArray()
            };
        }

        public SimWorldLayer WithoutObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return this;

            var id = objectId.Trim();

            return this with
            {
                ObjectIds = NormalizeObjectIds(ObjectIds)
                    .Where(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            };
        }

        private static IReadOnlyList<string> NormalizeObjectIds(IReadOnlyList<string>? ids)
        {
            if (ids is null || ids.Count == 0)
                return Array.Empty<string>();

            return ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
