using System;
using System.Globalization;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    public sealed partial class CorridorLocalPlanner
    {
        private static bool IsCorridorMarker(HydronomWorldObject obj)
        {
            if (!obj.IsActive)
                return false;

            if (IsObstacleLike(obj))
                return false;

            if (TryGetTagBool(obj, "corridorMarker"))
                return true;

            if (TryGetTag(obj, "side", out _))
                return true;

            if (TryGetTag(obj, "gateSide", out _))
                return true;

            if (TryGetTag(obj, "gate.side", out _))
                return true;

            if (TryGetGateIndex(obj) is not null)
                return true;

            if (IsBoundaryLike(obj))
                return true;

            if (IsGateLike(obj))
                return true;

            return obj.Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase) &&
                   (obj.Id.Contains("left", StringComparison.OrdinalIgnoreCase) ||
                    obj.Id.Contains("right", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsObstacleLike(HydronomWorldObject obj)
        {
            if (obj.Kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return true;

            if (obj.Layer.Equals("obstacle", StringComparison.OrdinalIgnoreCase) ||
                obj.Layer.Equals("scenario_obstacles", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "type", out var type) &&
                type.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "kind", out var kind) &&
                kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "layer", out var layer) &&
                (layer.Equals("obstacle", StringComparison.OrdinalIgnoreCase) ||
                 layer.Equals("scenario_obstacles", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (TryGetTag(obj, "role", out var role) &&
                role.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return true;

            return obj.Id.Contains("obs", StringComparison.OrdinalIgnoreCase) ||
                   obj.Id.Contains("blocker", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBoundaryLike(HydronomWorldObject obj)
        {
            if (obj.Layer.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                return true;

            if (obj.Kind.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "role", out var role) &&
                role.Contains("boundary", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "type", out var type) &&
                type.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetTag(obj, "layer", out var layer) &&
                layer.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                return true;

            return obj.Id.Contains("boundary", StringComparison.OrdinalIgnoreCase) ||
                   obj.Id.Contains("corridor_left", StringComparison.OrdinalIgnoreCase) ||
                   obj.Id.Contains("corridor_right", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGateLike(HydronomWorldObject obj)
        {
            if (TryGetTag(obj, "role", out var role) &&
                (role.Equals("gate_left", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("gate_right", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (TryGetTag(obj, "type", out var type) &&
                (type.Equals("gate_left", StringComparison.OrdinalIgnoreCase) ||
                 type.Equals("gate_right", StringComparison.OrdinalIgnoreCase)))
                return true;

            return obj.Id.Contains("gate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLeftMarker(HydronomWorldObject obj)
        {
            if (TryGetTag(obj, "side", out var side))
                return side.Equals("left", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "gateSide", out var gateSide))
                return gateSide.Equals("left", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "gate.side", out var dottedGateSide))
                return dottedGateSide.Equals("left", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "role", out var role))
                return role.Equals("gate_left", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("left_boundary", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "type", out var type))
                return type.Equals("gate_left", StringComparison.OrdinalIgnoreCase) ||
                       type.Equals("left_boundary", StringComparison.OrdinalIgnoreCase);

            return obj.Id.Contains("left", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.Contains("L-", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.EndsWith("-L", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRightMarker(HydronomWorldObject obj)
        {
            if (TryGetTag(obj, "side", out var side))
                return side.Equals("right", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "gateSide", out var gateSide))
                return gateSide.Equals("right", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "gate.side", out var dottedGateSide))
                return dottedGateSide.Equals("right", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "role", out var role))
                return role.Equals("gate_right", StringComparison.OrdinalIgnoreCase) ||
                       role.Equals("right_boundary", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "type", out var type))
                return type.Equals("gate_right", StringComparison.OrdinalIgnoreCase) ||
                       type.Equals("right_boundary", StringComparison.OrdinalIgnoreCase);

            return obj.Id.Contains("right", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.Contains("R-", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.EndsWith("-R", StringComparison.OrdinalIgnoreCase);
        }

        private static int? TryGetGateIndex(HydronomWorldObject obj)
        {
            return TryGetTagInt(obj, "gateIndex") ??
                   TryGetTagInt(obj, "gate.index") ??
                   TryGetTagInt(obj, "gate_index");
        }

        private static int? TryGetTagInt(HydronomWorldObject obj, string key)
        {
            if (!TryGetTag(obj, key, out var raw))
                return null;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static bool TryGetTagBool(HydronomWorldObject obj, string key)
        {
            if (!TryGetTag(obj, key, out var raw))
                return false;

            return bool.TryParse(raw, out var value) && value;
        }

        private static bool TryGetTag(HydronomWorldObject obj, string key, out string value)
        {
            value = string.Empty;

            if (obj.Tags is null)
                return false;

            if (!obj.Tags.TryGetValue(key, out var raw))
                return false;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            value = raw.Trim();
            return true;
        }
    }
}