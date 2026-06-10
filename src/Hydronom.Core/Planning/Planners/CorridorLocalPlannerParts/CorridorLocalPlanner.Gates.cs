using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    public sealed partial class CorridorLocalPlanner
    {
        private static IReadOnlyList<CorridorGate> BuildWorldCorridor(
            PlanningContext context,
            Vec3 start,
            Vec3 target)
        {
            var explicitGates = BuildExplicitTaggedGates(context.WorldObjects);
            if (explicitGates.Count > 0)
                return SelectRelevantGates(explicitGates, start, target);

            var inferredGates = InferGatesFromBuoyPairs(context.WorldObjects);
            if (inferredGates.Count > 0)
                return SelectRelevantGates(inferredGates, start, target);

            return Array.Empty<CorridorGate>();
        }

        private static IReadOnlyList<CorridorGate> BuildExplicitTaggedGates(
            IReadOnlyList<HydronomWorldObject> objects)
        {
            var markers = objects
                .Where(x => x.IsActive)
                .Where(IsCorridorMarker)
                .Where(x => TryGetGateIndex(x) is not null)
                .ToArray();

            if (markers.Length == 0)
                return Array.Empty<CorridorGate>();

            var gates = new List<CorridorGate>();

            foreach (var group in markers.GroupBy(x => TryGetGateIndex(x)!.Value))
            {
                var left = group.FirstOrDefault(IsLeftMarker);
                var right = group.FirstOrDefault(IsRightMarker);

                if (left is null || right is null)
                    continue;

                var gate = BuildGateFromPair(
                    $"gate-{group.Key}",
                    left,
                    right,
                    $"WORLD_TAGGED_GATE index={group.Key}");

                if (gate is not null)
                    gates.Add(gate);
            }

            return gates
                .OrderBy(x => x.Center.X)
                .ThenBy(x => x.Center.Y)
                .ToArray();
        }

        private static IReadOnlyList<CorridorGate> InferGatesFromBuoyPairs(
            IReadOnlyList<HydronomWorldObject> objects)
        {
            var buoys = objects
                .Where(x => x.IsActive)
                .Where(IsCorridorMarker)
                .Where(x => x.Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
                .Where(x => !IsObstacleLike(x))
                .ToArray();

            if (buoys.Length < 2)
                return Array.Empty<CorridorGate>();

            var lefts = buoys.Where(IsLeftMarker).ToArray();
            var rights = buoys.Where(IsRightMarker).ToArray();

            if (lefts.Length == 0 || rights.Length == 0)
                return Array.Empty<CorridorGate>();

            var gates = new List<CorridorGate>();
            var usedRights = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var left in lefts.OrderBy(x => x.X))
            {
                var right = rights
                    .Where(x => !usedRights.Contains(x.Id))
                    .Where(x => Math.Abs(x.X - left.X) <= GatePairMaxXDeltaMeters)
                    .OrderBy(x => Math.Abs(x.X - left.X))
                    .ThenBy(x => Distance2D(ToVec3(left), ToVec3(x)))
                    .FirstOrDefault();

                if (right is null)
                    continue;

                var gate = BuildGateFromPair(
                    $"inferred-gate-{gates.Count + 1}",
                    left,
                    right,
                    "WORLD_INFERRED_GATE_FROM_BUOYS");

                if (gate is null)
                    continue;

                usedRights.Add(right.Id);
                gates.Add(gate);
            }

            return gates
                .OrderBy(x => x.Center.X)
                .ThenBy(x => x.Center.Y)
                .ToArray();
        }

        private static CorridorGate? BuildGateFromPair(
            string id,
            HydronomWorldObject left,
            HydronomWorldObject right,
            string reason)
        {
            var leftPos = ToVec3(left);
            var rightPos = ToVec3(right);

            var width = Distance2D(leftPos, rightPos);

            if (width < GatePairMinWidthMeters || width > GatePairMaxWidthMeters)
                return null;

            var center = new Vec3(
                (left.X + right.X) * 0.5,
                (left.Y + right.Y) * 0.5,
                (left.Z + right.Z) * 0.5);

            var clearance = Math.Max(
                0.0,
                width - Math.Max(0.0, left.Radius) - Math.Max(0.0, right.Radius));

            var risk = clearance switch
            {
                < 1.5 => 0.85,
                < 2.5 => 0.65,
                < 4.0 => 0.35,
                _ => 0.12
            };

            return new CorridorGate(
                Id: id,
                Center: center,
                LeftObjectId: left.Id,
                RightObjectId: right.Id,
                WidthMeters: width,
                ClearanceMeters: clearance,
                RiskScore: risk,
                Reason: reason);
        }

        private static IReadOnlyList<CorridorGate> SelectRelevantGates(
            IReadOnlyList<CorridorGate> gates,
            Vec3 start,
            Vec3 target)
        {
            if (gates.Count == 0)
                return Array.Empty<CorridorGate>();

            var segmentLength = Distance2D(start, target);
            if (segmentLength <= 1e-6)
                return Array.Empty<CorridorGate>();

            var sx = Math.Min(start.X, target.X) - 4.0;
            var ex = Math.Max(start.X, target.X) + 4.0;

            // Projection rule:
            // - projection < 0 means the gate is behind the current segment.
            // - very tiny projection can mean the vehicle is already at/past the gate,
            //   which caused stale-corridor lock in the previous test.
            // - projection > 1 means beyond target; allow tiny overshoot only.
            var selected = gates
                .Select(g => new
                {
                    Gate = g,
                    Projection = ProjectionAlongSegment(g.Center, start, target),
                    DistanceToSegment = DistancePointToSegment2D(g.Center, start, target),
                    DistanceFromStart = Distance2D(start, g.Center)
                })
                .Where(x => x.Gate.Center.X >= sx && x.Gate.Center.X <= ex)
                .Where(x => x.Projection >= 0.06 && x.Projection <= 1.08)
                .Where(x => x.DistanceFromStart >= Math.Max(0.75, x.Gate.WidthMeters * 0.12))
                .OrderBy(x => x.Projection)
                .ThenBy(x => x.DistanceToSegment)
                .Select(x => x.Gate)
                .ToArray();

            if (selected.Length > 0)
                return selected;

            // Fallback: take only gates that are ahead-ish and close to the segment.
            // This prevents a random old gate behind the vehicle from hijacking the route.
            var fallback = gates
                .Select(g => new
                {
                    Gate = g,
                    Projection = ProjectionAlongSegment(g.Center, start, target),
                    DistanceToSegment = DistancePointToSegment2D(g.Center, start, target),
                    DistanceFromStart = Distance2D(start, g.Center)
                })
                .Where(x => x.Projection >= -0.05 && x.Projection <= 1.15)
                .Where(x => x.DistanceFromStart >= 0.75)
                .OrderBy(x => x.DistanceToSegment)
                .ThenBy(x => x.Projection)
                .Take(3)
                .OrderBy(x => x.Projection)
                .Select(x => x.Gate)
                .ToArray();

            return fallback;
        }

        private sealed record CorridorGate(
            string Id,
            Vec3 Center,
            string LeftObjectId,
            string RightObjectId,
            double WidthMeters,
            double ClearanceMeters,
            double RiskScore,
            string Reason);
    }
}