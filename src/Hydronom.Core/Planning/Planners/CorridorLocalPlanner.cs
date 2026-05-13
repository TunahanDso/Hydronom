using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// İlk local planner implementasyonu.
    ///
    /// Bu sürüm hedefe giden düz hattın yakınındaki engelleri inceler.
    /// Risk düşükse global path'i korur.
    /// Risk yüksekse hattı küçük bir yan offset ile corridor noktasına dönüştürür.
    /// </summary>
    public sealed class CorridorLocalPlanner : ILocalPlanner
    {
        public PlannedPath RefineLocal(
            PlanningContext context,
            PlannedPath globalPath)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var global = (globalPath ?? PlannedPath.Empty).Sanitized();

            if (!global.IsValid || global.Points.Count < 2)
                return global with
                {
                    Risk = PlanningRiskReport.Clear,
                    Summary = "LOCAL_SKIPPED_INVALID_GLOBAL"
                };

            var start = safe.VehicleState.Position.Sanitized();
            var target = global.LastPoint?.Position ?? safe.Goal.TargetPosition;

            var blocking = safe.BlockingObjects();
            var risk = EvaluateLineRisk(
                safe,
                start,
                target,
                blocking);

            if (!risk.RequiresReplan)
            {
                return global with
                {
                    Risk = risk,
                    RequiresReplan = false,
                    Source = nameof(CorridorLocalPlanner),
                    Summary = $"LOCAL_CLEAR {risk.Summary}"
                };
            }

            var detour = BuildDetourPoint(
                safe,
                start,
                target,
                blocking);

            var heading1 = HeadingDeg(start, detour);
            var heading2 = HeadingDeg(detour, target);

            var points = new List<PlannedPathPoint>
            {
                new PlannedPathPoint
                {
                    Id = "local-start",
                    Position = start,
                    PreferredHeadingDeg = heading1,
                    PreferredSpeedMps = 0.0,
                    AcceptanceRadiusMeters = Math.Max(0.5, safe.VehicleRadiusMeters),
                    ClearanceMeters = risk.MinimumClearanceMeters,
                    RiskScore = risk.RiskScore,
                    IsMandatory = true,
                    Reason = "LOCAL_START"
                },
                new PlannedPathPoint
                {
                    Id = "local-corridor-detour",
                    Position = detour,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = Math.Min(safe.Goal.DesiredCruiseSpeedMps, safe.MaxPlanSpeedMps) * 0.65,
                    AcceptanceRadiusMeters = Math.Max(0.8, safe.VehicleRadiusMeters + safe.SafetyMarginMeters),
                    ClearanceMeters = risk.MinimumClearanceMeters,
                    RiskScore = risk.RiskScore,
                    IsMandatory = true,
                    IsCorridorPoint = true,
                    Reason = "LOCAL_CORRIDOR_DETOUR"
                },
                new PlannedPathPoint
                {
                    Id = safe.Goal.GoalId,
                    Position = target,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = Math.Min(safe.Goal.DesiredCruiseSpeedMps, safe.MaxPlanSpeedMps),
                    AcceptanceRadiusMeters = safe.Goal.AcceptanceRadiusMeters,
                    ClearanceMeters = risk.MinimumClearanceMeters,
                    RiskScore = risk.RiskScore,
                    IsMandatory = true,
                    Reason = "LOCAL_TARGET"
                }
            };

            return new PlannedPath
            {
                Mode = PlanningMode.Corridor,
                Goal = safe.Goal,
                Points = points,
                Risk = risk,
                IsValid = true,
                RequiresReplan = false,
                Source = nameof(CorridorLocalPlanner),
                Summary = $"LOCAL_CORRIDOR detour=({detour.X:F1},{detour.Y:F1}) {risk.Summary}"
            }.Sanitized();
        }

        private static PlanningRiskReport EvaluateLineRisk(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            if (blocking.Count == 0)
                return PlanningRiskReport.Clear;

            var safetyDistance = context.VehicleRadiusMeters + context.SafetyMarginMeters;
            var minClearance = double.PositiveInfinity;
            var considered = 0;
            var blockingOnLine = 0;

            foreach (var obj in blocking)
            {
                considered++;

                var objectPosition = new Vec3(obj.X, obj.Y, obj.Z);
                var clearance = DistancePointToSegment2D(objectPosition, start, target) - Math.Max(0.0, obj.Radius);

                if (double.IsFinite(clearance))
                    minClearance = Math.Min(minClearance, clearance);

                if (clearance <= safetyDistance)
                    blockingOnLine++;
            }

            if (blockingOnLine == 0)
            {
                return new PlanningRiskReport
                {
                    RiskScore = 0.15,
                    ObstacleRisk = 0.15,
                    MinimumClearanceMeters = minClearance,
                    BlockingObjectCount = 0,
                    ConsideredObjectCount = considered,
                    RequiresReplan = false,
                    RequiresSlowMode = false,
                    Summary = $"CLEAR minClear={FormatClearance(minClearance)}"
                }.Sanitized();
            }

            var risk = Math.Clamp(1.0 - (minClearance / Math.Max(0.01, safetyDistance)), 0.45, 1.0);

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = risk,
                MinimumClearanceMeters = minClearance,
                BlockingObjectCount = blockingOnLine,
                ConsideredObjectCount = considered,
                RequiresReplan = true,
                RequiresSlowMode = true,
                Summary = $"BLOCKED count={blockingOnLine} minClear={FormatClearance(minClearance)}"
            }.Sanitized();
        }

        private static Vec3 BuildDetourPoint(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);

            if (len <= 1e-6)
                return target;

            var ux = dx / len;
            var uy = dy / len;

            var nx = -uy;
            var ny = ux;

            var midpoint = new Vec3(
                start.X + dx * 0.45,
                start.Y + dy * 0.45,
                start.Z + (target.Z - start.Z) * 0.45);

            var leftScore = ScoreSide(midpoint, nx, ny, blocking);
            var rightScore = ScoreSide(midpoint, -nx, -ny, blocking);

            var sign = leftScore >= rightScore ? 1.0 : -1.0;
            var offset = Math.Max(
                context.VehicleRadiusMeters + context.SafetyMarginMeters + 1.0,
                2.5);

            return new Vec3(
                midpoint.X + nx * sign * offset,
                midpoint.Y + ny * sign * offset,
                midpoint.Z);
        }

        private static double ScoreSide(
            Vec3 midpoint,
            double nx,
            double ny,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            if (blocking.Count == 0)
                return double.PositiveInfinity;

            double minDistance = double.PositiveInfinity;

            var sample = new Vec3(
                midpoint.X + nx * 2.5,
                midpoint.Y + ny * 2.5,
                midpoint.Z);

            foreach (var obj in blocking)
            {
                var dx = sample.X - obj.X;
                var dy = sample.Y - obj.Y;
                var d = Math.Sqrt(dx * dx + dy * dy) - Math.Max(0.0, obj.Radius);

                minDistance = Math.Min(minDistance, d);
            }

            return minDistance;
        }

        private static double DistancePointToSegment2D(Vec3 point, Vec3 a, Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var c1 = vx * wx + vy * wy;
            if (c1 <= 0.0)
                return Distance2D(point, a);

            var c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return Distance2D(point, b);

            var t = c1 / c2;

            var projection = new Vec3(
                a.X + t * vx,
                a.Y + t * vy,
                a.Z);

            return Distance2D(point, projection);
        }

        private static double Distance2D(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double HeadingDeg(Vec3 from, Vec3 to)
        {
            return NormalizeDeg(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI);
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }

        private static string FormatClearance(double clearance)
        {
            return double.IsFinite(clearance) ? $"{clearance:F2}m" : "inf";
        }
    }
}