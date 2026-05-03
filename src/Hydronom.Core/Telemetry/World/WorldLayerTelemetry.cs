using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.World;

namespace Hydronom.Core.Telemetry.World
{
    /// <summary>
    /// Ops/Gateway/Ground Station iÃ§in dÃ¼nya layer telemetry modeli.
    ///
    /// Ops bu bilgiyle obstacle, mission objects, zones, environment, sensor debug,
    /// physics truth ve replay katmanlarÄ±nÄ± ayrÄ± ayrÄ± gÃ¶sterebilir/gizleyebilir.
    /// </summary>
    public readonly record struct WorldLayerTelemetry(
        string LayerId,
        string DisplayName,
        string Kind,
        bool VisibleByDefault,
        bool Locked,
        int DrawOrder,
        IReadOnlyList<string> ObjectIds
    )
    {
        public static WorldLayerTelemetry FromLayer(SimWorldLayer layer)
        {
            var safe = layer.Sanitized();

            return new WorldLayerTelemetry(
                LayerId: safe.LayerId,
                DisplayName: safe.DisplayName,
                Kind: safe.Kind.ToString(),
                VisibleByDefault: safe.VisibleByDefault,
                Locked: safe.Locked,
                DrawOrder: safe.DrawOrder,
                ObjectIds: NormalizeObjectIds(safe.ObjectIds)
            );
        }

        public WorldLayerTelemetry Sanitized()
        {
            return new WorldLayerTelemetry(
                LayerId: Normalize(LayerId, "world_layer"),
                DisplayName: Normalize(DisplayName, "World Layer"),
                Kind: Normalize(Kind, "Unknown"),
                VisibleByDefault: VisibleByDefault,
                Locked: Locked,
                DrawOrder: DrawOrder,
                ObjectIds: NormalizeObjectIds(ObjectIds)
            );
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
