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
        private static PlanningRiskReport EvaluateGateRisk(
            PlanningContext context,
            CorridorGate gate,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var vehicleRadius = Math.Max(0.0, context.VehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, context.SafetyMarginMeters);

            var physicalClearance = gate.ClearanceMeters - vehicleRadius;
            var safetyClearance = physicalClearance - safetyMargin;

            var risk = Math.Max(
                gate.RiskScore,
                safetyClearance switch
                {
                    < 0.0 => 0.65,
                    < 0.75 => 0.50,
                    < 1.5 => 0.30,
                    _ => 0.10
                });

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = risk,
                CorridorRisk = risk,
                MinimumClearanceMeters = physicalClearance,
                BlockingObjectCount = physicalClearance < 0.0 ? 1 : 0,
                ConsideredObjectCount = blocking.Count,
                RequiresReplan = physicalClearance < 0.0,
                RequiresSlowMode = safetyClearance < 1.25,
                Summary =
                    $"GATE width={gate.WidthMeters:F2}m " +
                    $"physicalClear={FormatClearance(physicalClearance)} " +
                    $"safetyClear={FormatClearance(safetyClearance)}"
            }.Sanitized();
        }

        private static PlanningRiskReport EvaluateLineRisk(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var relevantBlocking = blocking
                .Where(x => !IsCorridorMarker(x))
                .ToArray();

            if (relevantBlocking.Length == 0)
            {
                return new PlanningRiskReport
                {
                    RiskScore = 0.0,
                    ObstacleRisk = 0.0,
                    CorridorRisk = 0.0,
                    MinimumClearanceMeters = double.PositiveInfinity,
                    BlockingObjectCount = 0,
                    ConsideredObjectCount = 0,
                    RequiresReplan = false,
                    RequiresSlowMode = false,
                    Summary = "CLEAR physicalClear=inf safetyClear=inf nearest=none"
                }.Sanitized();
            }

            var vehicleRadius = Math.Max(0.0, context.VehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, context.SafetyMarginMeters);
            var safetyDistance = Math.Max(0.01, vehicleRadius + safetyMargin);

            var minPhysicalClearance = double.PositiveInfinity;
            var minSafetyClearance = double.PositiveInfinity;
            var considered = 0;
            var blockingOnLine = 0;
            var physicalCollisionCount = 0;

            HydronomWorldObject? nearestObject = null;

            foreach (var obj in relevantBlocking)
            {
                considered++;

                var objectPosition = new Vec3(obj.X, obj.Y, obj.Z);
                var distanceToLine = DistancePointToSegment2D(objectPosition, start, target);
                var obstacleRadius = Math.Max(0.0, obj.Radius);

                // Geometry truth:
                // physicalClearance < 0 -> vehicle body intersects obstacle body.
                // safetyClearance < 0 -> no physical collision yet, but safety envelope is violated.
                var physicalClearance = distanceToLine - obstacleRadius - vehicleRadius;
                var safetyClearance = physicalClearance - safetyMargin;

                if (double.IsFinite(physicalClearance) && physicalClearance < minPhysicalClearance)
                {
                    minPhysicalClearance = physicalClearance;
                    nearestObject = obj;
                }

                if (double.IsFinite(safetyClearance))
                    minSafetyClearance = Math.Min(minSafetyClearance, safetyClearance);

                if (physicalClearance < 0.0)
                    physicalCollisionCount++;

                if (safetyClearance <= 0.0)
                    blockingOnLine++;
            }

            if (blockingOnLine == 0 && physicalCollisionCount == 0)
            {
                return new PlanningRiskReport
                {
                    RiskScore = 0.10,
                    ObstacleRisk = 0.10,
                    CorridorRisk = 0.0,
                    MinimumClearanceMeters = minPhysicalClearance,
                    BlockingObjectCount = 0,
                    ConsideredObjectCount = considered,
                    RequiresReplan = false,
                    RequiresSlowMode = false,
                    Summary =
                        $"CLEAR physicalClear={FormatClearance(minPhysicalClearance)} " +
                        $"safetyClear={FormatClearance(minSafetyClearance)} " +
                        $"nearest={nearestObject?.Id ?? "none"}"
                }.Sanitized();
            }

            var risk =
                physicalCollisionCount > 0
                    ? HardCollisionRisk
                    : Math.Clamp(
                        1.0 - Math.Clamp(minSafetyClearance / safetyDistance, 0.0, 1.0),
                        BlockedRiskFloor,
                        0.65);

            if (physicalCollisionCount == 0 && minPhysicalClearance < 0.20)
                risk = Math.Max(risk, 0.78);

            var requiresReplan = physicalCollisionCount > 0;
            var requiresSlowMode = physicalCollisionCount > 0 || minSafetyClearance < 1.25;

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = risk,
                CorridorRisk = 0.0,
                MinimumClearanceMeters = minPhysicalClearance,
                BlockingObjectCount = physicalCollisionCount,
                ConsideredObjectCount = considered,
                RequiresReplan = requiresReplan,
                RequiresSlowMode = requiresSlowMode,
                Summary =
                    $"{(physicalCollisionCount > 0 ? "COLLISION_CANDIDATE" : "CAUTION")} " +
                    $"count={physicalCollisionCount} safetyHits={blockingOnLine} collisions={physicalCollisionCount} " +
                    $"physicalClear={FormatClearance(minPhysicalClearance)} " +
                    $"safetyClear={FormatClearance(minSafetyClearance)} " +
                    $"nearest={nearestObject?.Id ?? "none"}"
            }.Sanitized();
        }

        private static CandidateSegmentRisk EvaluateCandidateSegment(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var vehicleRadius = Math.Max(0.0, context.VehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, context.SafetyMarginMeters);

            var minPhysicalClearance = double.PositiveInfinity;
            var minSafetyClearance = double.PositiveInfinity;

            foreach (var obj in blocking)
            {
                var objectPosition = new Vec3(obj.X, obj.Y, obj.Z);
                var distanceToLine = DistancePointToSegment2D(objectPosition, start, target);
                var obstacleRadius = Math.Max(0.0, obj.Radius);

                var physicalClearance = distanceToLine - obstacleRadius - vehicleRadius;
                var safetyClearance = physicalClearance - safetyMargin;

                if (double.IsFinite(physicalClearance))
                    minPhysicalClearance = Math.Min(minPhysicalClearance, physicalClearance);

                if (double.IsFinite(safetyClearance))
                    minSafetyClearance = Math.Min(minSafetyClearance, safetyClearance);
            }

            return new CandidateSegmentRisk(
                PhysicalClearance: minPhysicalClearance,
                SafetyClearance: minSafetyClearance);
        }

        private sealed record CandidateSegmentRisk(
            double PhysicalClearance,
            double SafetyClearance);
    }
}