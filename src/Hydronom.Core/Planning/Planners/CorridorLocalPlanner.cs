using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Abstractions;
using Hydronom.Core.Planning.Models;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    /// <summary>
    /// World-aware local planner.
    ///
    /// Bu planner artık yalnızca "hedefe giden düz çizgide engel var mı?" diye bakmaz.
    /// RuntimeWorldModel içindeki semantik dünya objelerini okuyarak:
    ///
    /// - Sol/sağ buoy çiftlerinden gate/corridor merkezleri çıkarır.
    /// - Checkpoint/finish gibi görev objelerini path'e bağlar.
    /// - Corridor centerline üretir.
    /// - Düz hat riskini hesaplar.
    /// - Corridor varsa hedefe çıplak saldırmak yerine güvenli geçiş hattı üretir.
    ///
    /// Bu hâlâ ilk ürün seviyesi sürümdür; ileride grid/A*/RRT/MPC tabanlı local planner
    /// bu sınıfın yerine veya altına eklenebilir. Ancak bu sürüm artık gerçek world model
    /// semantiğini kullanır.
    /// </summary>
    public sealed class CorridorLocalPlanner : ILocalPlanner
    {
        private const double GatePairMaxXDeltaMeters = 2.75;
        private const double GatePairMinWidthMeters = 1.5;
        private const double GatePairMaxWidthMeters = 24.0;
        private const double DuplicatePointDistanceMeters = 0.35;

        public PlannedPath RefineLocal(
            PlanningContext context,
            PlannedPath globalPath)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var global = (globalPath ?? PlannedPath.Empty).Sanitized();

            if (!global.IsValid || global.Points.Count < 2)
            {
                return global with
                {
                    Risk = PlanningRiskReport.Clear,
                    Summary = "LOCAL_SKIPPED_INVALID_GLOBAL"
                };
            }

            var start = safe.VehicleState.Position.Sanitized();
            var target = global.LastPoint?.Position ?? safe.Goal.TargetPosition.Sanitized();

            var blocking = safe.BlockingObjects();
            var directRisk = EvaluateLineRisk(
                safe,
                start,
                target,
                blocking);

            var corridor = BuildWorldCorridor(
                safe,
                start,
                target);

            if (corridor.Count > 0)
            {
                var corridorPath = BuildCorridorPath(
                    safe,
                    start,
                    target,
                    corridor,
                    directRisk,
                    blocking);

                if (corridorPath.IsValid)
                    return corridorPath.Sanitized();
            }

            if (!directRisk.RequiresReplan)
            {
                return global with
                {
                    Risk = directRisk,
                    RequiresReplan = false,
                    Source = nameof(CorridorLocalPlanner),
                    Summary = $"LOCAL_WORLD_DIRECT_CLEAR {directRisk.Summary}"
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
                    ClearanceMeters = directRisk.MinimumClearanceMeters,
                    RiskScore = directRisk.RiskScore,
                    IsMandatory = true,
                    Reason = "LOCAL_START"
                },
                new PlannedPathPoint
                {
                    Id = "local-detour",
                    Position = detour,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = Math.Min(safe.Goal.DesiredCruiseSpeedMps, safe.MaxPlanSpeedMps) * 0.55,
                    AcceptanceRadiusMeters = Math.Max(0.8, safe.VehicleRadiusMeters + safe.SafetyMarginMeters),
                    ClearanceMeters = directRisk.MinimumClearanceMeters,
                    RiskScore = directRisk.RiskScore,
                    IsMandatory = true,
                    IsCorridorPoint = true,
                    Reason = "LOCAL_DETOUR_FROM_BLOCKING_OBJECT"
                },
                new PlannedPathPoint
                {
                    Id = safe.Goal.GoalId,
                    Position = target,
                    PreferredHeadingDeg = heading2,
                    PreferredSpeedMps = Math.Min(safe.Goal.DesiredCruiseSpeedMps, safe.MaxPlanSpeedMps),
                    AcceptanceRadiusMeters = safe.Goal.AcceptanceRadiusMeters,
                    ClearanceMeters = directRisk.MinimumClearanceMeters,
                    RiskScore = directRisk.RiskScore,
                    IsMandatory = true,
                    Reason = "LOCAL_TARGET_AFTER_DETOUR"
                }
            };

            return new PlannedPath
            {
                Mode = PlanningMode.Avoidance,
                Goal = safe.Goal,
                Points = points,
                Risk = directRisk,
                IsValid = true,
                RequiresReplan = false,
                Source = nameof(CorridorLocalPlanner),
                Summary = $"LOCAL_AVOIDANCE detour=({detour.X:F1},{detour.Y:F1}) {directRisk.Summary}"
            }.Sanitized();
        }

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

            for (var i = 0; i < corridor.Count; i++)
            {
                var gate = corridor[i];

                var next = i + 1 < corridor.Count
                    ? corridor[i + 1].Center
                    : target;

                var gateRisk = EvaluateGateRisk(
                    context,
                    gate,
                    blocking);

                var speedScale = gateRisk.RequiresSlowMode ? 0.55 : 0.85;

                points.Add(new PlannedPathPoint
                {
                    Id = gate.Id,
                    Position = gate.Center,
                    PreferredHeadingDeg = HeadingDeg(gate.Center, next),
                    PreferredSpeedMps = Math.Min(context.Goal.DesiredCruiseSpeedMps, context.MaxPlanSpeedMps) * speedScale,
                    AcceptanceRadiusMeters = Math.Max(0.75, Math.Min(gate.WidthMeters * 0.35, 2.5)),
                    ClearanceMeters = Math.Min(gate.ClearanceMeters, gateRisk.MinimumClearanceMeters),
                    RiskScore = Math.Max(gate.RiskScore, gateRisk.RiskScore),
                    IsMandatory = true,
                    IsCorridorPoint = true,
                    Reason = gate.Reason
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
                PreferredSpeedMps = Math.Min(context.Goal.DesiredCruiseSpeedMps, context.MaxPlanSpeedMps),
                AcceptanceRadiusMeters = context.Goal.AcceptanceRadiusMeters,
                ClearanceMeters = targetRisk.MinimumClearanceMeters,
                RiskScore = targetRisk.RiskScore,
                IsMandatory = true,
                IsCorridorPoint = false,
                Reason = "WORLD_CORRIDOR_TARGET"
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
                RequiresReplan = false,
                Source = nameof(CorridorLocalPlanner),
                Summary =
                    $"WORLD_CORRIDOR gates={corridor.Count} " +
                    $"points={points.Count} " +
                    $"risk={combinedRisk.RiskScore:F2} " +
                    $"minClear={FormatClearance(combinedRisk.MinimumClearanceMeters)}"
            }.Sanitized();
        }

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
                .Where(x => TryGetTagInt(x, "gateIndex") is not null)
                .ToArray();

            if (markers.Length == 0)
                return Array.Empty<CorridorGate>();

            var gates = new List<CorridorGate>();

            foreach (var group in markers.GroupBy(x => TryGetTagInt(x, "gateIndex")!.Value))
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
                .Where(x => x.Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
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

            var clearance = Math.Max(0.0, width - left.Radius - right.Radius);

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

            var sx = Math.Min(start.X, target.X) - 3.0;
            var ex = Math.Max(start.X, target.X) + 3.0;

            var selected = gates
                .Where(g => g.Center.X >= sx && g.Center.X <= ex)
                .OrderBy(g => ProjectionAlongSegment(g.Center, start, target))
                .ToArray();

            if (selected.Length > 0)
                return selected;

            return gates
                .OrderBy(g => DistancePointToSegment2D(g.Center, start, target))
                .Take(4)
                .OrderBy(g => ProjectionAlongSegment(g.Center, start, target))
                .ToArray();
        }

        private static PlanningRiskReport EvaluateGateRisk(
            PlanningContext context,
            CorridorGate gate,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var margin = context.VehicleRadiusMeters + context.SafetyMarginMeters;
            var clearance = gate.ClearanceMeters - margin;

            var risk = Math.Max(
                gate.RiskScore,
                clearance switch
                {
                    < 0.0 => 0.95,
                    < 0.75 => 0.75,
                    < 1.5 => 0.45,
                    _ => 0.15
                });

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = risk,
                CorridorRisk = risk,
                MinimumClearanceMeters = Math.Max(0.0, clearance),
                BlockingObjectCount = risk >= 0.75 ? 1 : 0,
                ConsideredObjectCount = blocking.Count,
                RequiresReplan = risk >= 0.90,
                RequiresSlowMode = risk >= 0.45,
                Summary = $"GATE width={gate.WidthMeters:F2}m clearance={clearance:F2}m"
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
                return PlanningRiskReport.Clear;

            var safetyDistance = context.VehicleRadiusMeters + context.SafetyMarginMeters;
            var minClearance = double.PositiveInfinity;
            var considered = 0;
            var blockingOnLine = 0;

            foreach (var obj in relevantBlocking)
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
                    RiskScore = 0.10,
                    ObstacleRisk = 0.10,
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

            return new PlanningRiskReport
            {
                RiskScore = risk,
                ObstacleRisk = Math.Max(direct.ObstacleRisk, target.ObstacleRisk),
                CorridorRisk = maxPointRisk,
                MinimumClearanceMeters = minClearance,
                BlockingObjectCount = direct.BlockingObjectCount + target.BlockingObjectCount,
                ConsideredObjectCount = direct.ConsideredObjectCount + target.ConsideredObjectCount,
                RequiresReplan = risk >= 0.90,
                RequiresSlowMode = risk >= 0.45,
                Summary = $"WORLD_CORRIDOR_RISK risk={risk:F2} minClear={FormatClearance(minClearance)}"
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

            var nonCorridorBlocking = blocking
                .Where(x => !IsCorridorMarker(x))
                .ToArray();

            var leftScore = ScoreSide(midpoint, nx, ny, nonCorridorBlocking);
            var rightScore = ScoreSide(midpoint, -nx, -ny, nonCorridorBlocking);

            var sign = leftScore >= rightScore ? 1.0 : -1.0;
            var offset = Math.Max(
                context.VehicleRadiusMeters + context.SafetyMarginMeters + 1.0,
                2.5);

            return new Vec3(
                midpoint.X + nx * sign * offset,
                midpoint.Y + ny * sign * offset,
                midpoint.Z);
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

        private static bool IsCorridorMarker(HydronomWorldObject obj)
        {
            if (!obj.Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
                return false;

            if (TryGetTagBool(obj, "corridorMarker"))
                return true;

            if (TryGetTag(obj, "side", out _))
                return true;

            return obj.Id.Contains("buoy", StringComparison.OrdinalIgnoreCase) &&
                   (obj.Id.Contains("left", StringComparison.OrdinalIgnoreCase) ||
                    obj.Id.Contains("right", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLeftMarker(HydronomWorldObject obj)
        {
            if (TryGetTag(obj, "side", out var side))
                return side.Equals("left", StringComparison.OrdinalIgnoreCase);

            if (TryGetTag(obj, "gateSide", out var gateSide))
                return gateSide.Equals("left", StringComparison.OrdinalIgnoreCase);

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

            return obj.Id.Contains("right", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.Contains("R-", StringComparison.OrdinalIgnoreCase) ||
                   obj.Name.EndsWith("-R", StringComparison.OrdinalIgnoreCase);
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

        private static double ProjectionAlongSegment(Vec3 point, Vec3 a, Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var c2 = vx * vx + vy * vy;
            if (c2 <= 1e-9)
                return 0.0;

            return (vx * wx + vy * wy) / c2;
        }

        private static Vec3 ToVec3(HydronomWorldObject obj)
        {
            return new Vec3(obj.X, obj.Y, obj.Z);
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