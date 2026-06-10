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
        private static PlannedPath BuildCorridorPath(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<CorridorGate> corridor,
            PlanningRiskReport directRisk,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var points = new List<PlannedPathPoint>();

            var firstTarget = corridor.Count > 0
                ? corridor[0].Center
                : target;

            points.Add(new PlannedPathPoint
            {
                Id = "local-start",
                Position = start,
                PreferredHeadingDeg = HeadingDeg(start, firstTarget),
                PreferredSpeedMps = 0.0,
                AcceptanceRadiusMeters = Math.Max(0.5, context.VehicleRadiusMeters),
                ClearanceMeters = directRisk.MinimumClearanceMeters,
                RiskScore = directRisk.RiskScore,
                IsMandatory = true,
                Reason = "WORLD_CORRIDOR_START"
            });

            var cruise = Math.Min(context.Goal.DesiredCruiseSpeedMps, context.MaxPlanSpeedMps);

            for (var i = 0; i < corridor.Count; i++)
            {
                var gate = corridor[i];

                var previous = points[^1].Position;
                var next = i + 1 < corridor.Count
                    ? corridor[i + 1].Center
                    : target;

                var gateRisk = EvaluateGateRisk(
                    context,
                    gate,
                    blocking);

                var segmentRisk = EvaluateLineRisk(
                    context,
                    previous,
                    gate.Center,
                    blocking);

                var risk = CombineRisks(
                    gateRisk,
                    segmentRisk,
                    Array.Empty<PlannedPathPoint>());

                var speedScale = SpeedScaleFromRisk(risk);

                AddIfNotDuplicate(points, new PlannedPathPoint
                {
                    Id = gate.Id,
                    Position = gate.Center,
                    PreferredHeadingDeg = HeadingDeg(gate.Center, next),
                    PreferredSpeedMps = cruise * speedScale,
                    AcceptanceRadiusMeters = Math.Max(0.75, Math.Min(gate.WidthMeters * 0.35, 2.5)),
                    ClearanceMeters = Math.Min(gate.ClearanceMeters, risk.MinimumClearanceMeters),
                    RiskScore = Math.Max(gate.RiskScore, risk.RiskScore),
                    IsMandatory = true,
                    IsCorridorPoint = true,
                    Reason = $"{gate.Reason} {risk.Summary}"
                });
            }

            var lastHeadingFrom = points.Count > 0
                ? points[^1].Position
                : start;

            var targetRisk = EvaluateLineRisk(
                context,
                lastHeadingFrom,
                target,
                blocking);

            AddIfNotDuplicate(points, new PlannedPathPoint
            {
                Id = context.Goal.GoalId,
                Position = target,
                PreferredHeadingDeg = HeadingDeg(lastHeadingFrom, target),
                PreferredSpeedMps = cruise,
                AcceptanceRadiusMeters = context.Goal.AcceptanceRadiusMeters,
                ClearanceMeters = targetRisk.MinimumClearanceMeters,
                RiskScore = targetRisk.RiskScore,
                IsMandatory = true,
                IsCorridorPoint = false,
                Reason = $"WORLD_CORRIDOR_TARGET {targetRisk.Summary}"
            });

            var combinedRisk = CombineRisks(
                directRisk,
                targetRisk,
                points);

            return new PlannedPath
            {
                Mode = PlanningMode.Corridor,
                Goal = context.Goal,
                Points = points,
                Risk = combinedRisk,
                IsValid = points.Count >= 2,
                RequiresReplan = combinedRisk.RequiresReplan,
                Source = nameof(CorridorLocalPlanner),
                Summary =
                    $"WORLD_CORRIDOR gates={corridor.Count} " +
                    $"points={points.Count} " +
                    $"risk={combinedRisk.RiskScore:F2} " +
                    $"minPhysicalClear={FormatClearance(combinedRisk.MinimumClearanceMeters)} " +
                    combinedRisk.Summary
            }.Sanitized();
        }

        private static PlannedPath BuildDetourPath(
            PlanningContext context,
            Vec3 start,
            Vec3 detour,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking,
            string tag)
        {
            var startToDetourRisk = EvaluateLineRisk(
                context,
                start,
                detour,
                blocking);

            var detourToTargetRisk = EvaluateLineRisk(
                context,
                detour,
                target,
                blocking);

            var combinedRisk = CombineRisks(
                startToDetourRisk,
                detourToTargetRisk,
                Array.Empty<PlannedPathPoint>());

            var heading1 = HeadingDeg(start, detour);
            var heading2 = HeadingDeg(detour, target);

            var cruise = Math.Min(context.Goal.DesiredCruiseSpeedMps, context.MaxPlanSpeedMps);
            var detourSpeedScale = SpeedScaleFromRisk(combinedRisk);

            var points = new List<PlannedPathPoint>
            {
                new PlannedPathPoint
                {
                    Id = "local-start",
                    Position = start,
                    PreferredHeadingDeg = heading1,
                    PreferredSpeedMps = 0.0,
                    AcceptanceRadiusMeters = Math.Max(0.5, context.VehicleRadiusMeters),
                    ClearanceMeters = startToDetourRisk.MinimumClearanceMeters,
                    RiskScore = startToDetourRisk.RiskScore,
                    IsMandatory = true,
                    Reason = "DETOUR_START"
                },
                new PlannedPathPoint
                {
                    Id = "local-detour",
                    Position = detour,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = cruise * detourSpeedScale,
                    AcceptanceRadiusMeters = Math.Max(0.8, context.VehicleRadiusMeters + context.SafetyMarginMeters),
                    ClearanceMeters = Math.Min(
                        startToDetourRisk.MinimumClearanceMeters,
                        detourToTargetRisk.MinimumClearanceMeters),
                    RiskScore = Math.Max(
                        startToDetourRisk.RiskScore,
                        detourToTargetRisk.RiskScore),
                    IsMandatory = true,
                    IsCorridorPoint = true,
                    Reason =
                        $"DETOUR_POINT tag={tag} " +
                        $"in={startToDetourRisk.Summary} " +
                        $"out={detourToTargetRisk.Summary}"
                },
                new PlannedPathPoint
                {
                    Id = context.Goal.GoalId,
                    Position = target,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = cruise,
                    AcceptanceRadiusMeters = context.Goal.AcceptanceRadiusMeters,
                    ClearanceMeters = detourToTargetRisk.MinimumClearanceMeters,
                    RiskScore = detourToTargetRisk.RiskScore,
                    IsMandatory = true,
                    IsCorridorPoint = false,
                    Reason = $"DETOUR_TARGET {detourToTargetRisk.Summary}"
                }
            };

            return new PlannedPath
            {
                Mode = PlanningMode.Avoidance,
                Goal = context.Goal,
                Points = points,
                Risk = combinedRisk,
                IsValid = true,
                RequiresReplan = combinedRisk.RequiresReplan,
                Source = nameof(CorridorLocalPlanner),
                Summary =
                    $"LOCAL_DETOUR tag={tag} " +
                    $"detour=({detour.X:F1},{detour.Y:F1}) " +
                    $"risk={combinedRisk.RiskScore:F2} " +
                    $"in={startToDetourRisk.Summary} " +
                    $"out={detourToTargetRisk.Summary}"
            }.Sanitized();
        }

        private static PlanningRiskReport CombineRisks(
            PlanningRiskReport direct,
            PlanningRiskReport target,
            IReadOnlyList<PlannedPathPoint> points)
        {
            var maxPointRisk = points.Count == 0
                ? 0.0
                : points.Max(x => x.RiskScore);

            var minClearance = new[]
                {
                    direct.MinimumClearanceMeters,
                    target.MinimumClearanceMeters
                }
                .Concat(points.Select(x => x.ClearanceMeters))
                .Where(double.IsFinite)
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min();

            var risk = Math.Max(
                Math.Max(direct.RiskScore, target.RiskScore),
                maxPointRisk);

            var blockingCount = direct.BlockingObjectCount + target.BlockingObjectCount;
            var consideredCount = direct.ConsideredObjectCount + target.ConsideredObjectCount;

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = Math.Max(direct.ObstacleRisk, target.ObstacleRisk),
                CorridorRisk = maxPointRisk,
                MinimumClearanceMeters = minClearance,
                BlockingObjectCount = blockingCount,
                ConsideredObjectCount = consideredCount,
                RequiresReplan = risk >= 0.90 || direct.RequiresReplan || target.RequiresReplan,
                RequiresSlowMode = risk >= 0.45 || direct.RequiresSlowMode || target.RequiresSlowMode,
                Summary =
                    $"COMBINED_RISK risk={risk:F2} " +
                    $"minPhysicalClear={FormatClearance(minClearance)} " +
                    $"blocking={blockingCount} considered={consideredCount}"
            }.Sanitized();
        }

        private static double SpeedScaleFromRisk(PlanningRiskReport risk)
        {
            if (risk.RiskScore >= 0.98)
                return 0.22;

            if (risk.RiskScore >= 0.90)
                return 0.34;

            if (risk.RiskScore >= 0.70)
                return 0.48;

            if (risk.RequiresSlowMode)
                return 0.62;

            return 0.88;
        }

        private static void AddIfNotDuplicate(
            List<PlannedPathPoint> points,
            PlannedPathPoint point)
        {
            if (points.Count > 0 &&
                Distance2D(points[^1].Position, point.Position) <= DuplicatePointDistanceMeters)
            {
                return;
            }

            points.Add(point);
        }
    }
}