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
    /// Hydronom Autonomous Navigation Core - CorridorLocalPlanner.
    ///
    /// Paket-8G:
    /// Critical physical obstacle varken direct/world-corridor candidate artık havuza alınmaz.
    ///
    /// Kök problem:
    /// Sistem start-target hattına basit bir direct path çiziyor, sonra obstacle riskini
    /// scoring/fallback ile çözmeye çalışıyordu. Bu da slalomda dubaya kafa atmaya sebep oluyordu.
    ///
    /// Yeni davranış:
    /// - Start-target hattında critical route obstacle varsa:
    ///   direct candidate ve world-corridor candidate normal candidate havuzuna alınmaz.
    ///   Planner yalnızca obstacle-centered bypass/fallback detour adaylarını değerlendirir.
    /// - Critical obstacle yoksa eski direct/corridor davranışı korunur.
    /// - Hiç bypass candidate üretilemezse direct risk ile RequiresReplan döner.
    /// </summary>
    public sealed partial class CorridorLocalPlanner : ILocalPlanner
    {
        private const double GatePairMaxXDeltaMeters = 2.75;
        private const double GatePairMinWidthMeters = 1.5;
        private const double GatePairMaxWidthMeters = 24.0;
        private const double DuplicatePointDistanceMeters = 0.35;

        private const double HardCollisionRisk = 1.0;
        private const double NearCollisionRisk = 0.92;
        private const double BlockedRiskFloor = 0.20;
        private const double BlockedRiskCeiling = 0.95;

        private const double CandidateRejectCollisionPenalty = 1_000.0;
        private const double CandidateUnsafePenalty = 80.0;
        private const double CandidateStaleCorridorPenalty = 25.0;

        private const double BoundaryRelevantXPadMeters = 8.0;
        private const double BoundaryMinimumUsableWidthMeters = 2.0;

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

            /*
             * Paket-8G critical obstacle gate:
             *
             * Direct/world-corridor candidate üretmeden önce start-target hattını fiziksel
             * obstacle açısından kontrol ediyoruz.
             *
             * Eğer burada critical obstacle varsa direct/corridor seçeneği aday havuzuna bile
             * alınmaz. Çünkü "scoring belki eler" yaklaşımı pratikte dubaya kafa atmaya sebep oldu.
             */
            var allWorldObjects =
                safe.WorldObjects is { Count: > 0 }
                    ? safe.WorldObjects
                    : blocking;

            var classified = WorldObjectPlanningClassifier.Classify(
                allWorldObjects,
                safe.VehicleRadiusMeters,
                safe.SafetyMarginMeters,
                source: "planner-critical-gate");

            var planningBlockers = classified
                .Where(x => x.CanBlockRoute)
                .ToArray();

            var criticalRouteObstacles = CriticalRouteObstacleDetector.Detect(
                    start,
                    target,
                    planningBlockers,
                    safe.VehicleRadiusMeters,
                    safe.SafetyMarginMeters,
                    safe.MaxPlanSpeedMps,
                    maxResults: 6)
                .ToArray();

            var hasCriticalRouteObstacle = criticalRouteObstacles
                .Any(x => x.RequiresBypass);

            var criticalSummary = BuildCriticalGateSummary(
                hasCriticalRouteObstacle,
                planningBlockers.Length,
                criticalRouteObstacles);

            var candidates = new List<LocalPathCandidate>();

            var directCandidate = BuildDirectCandidate(
                safe,
                global,
                start,
                target,
                blocking);

            /*
             * Critical route obstacle yoksa eski direct/corridor seçenekleri korunur.
             * Critical varsa bu ikisi candidate havuzuna GİRMEZ.
             */
            if (!hasCriticalRouteObstacle)
            {
                candidates.Add(directCandidate);

                var corridor = BuildWorldCorridor(
                    safe,
                    start,
                    target);

                if (corridor.Count > 0)
                {
                    var corridorCandidate = BuildWorldCorridorCandidate(
                        safe,
                        start,
                        target,
                        corridor,
                        blocking);

                    candidates.Add(corridorCandidate);
                }
            }

            var detourCandidates = BuildWorldAwareDetourCandidates(
                safe,
                start,
                target,
                blocking);

            candidates.AddRange(detourCandidates);

            var best = SelectBestCandidate(candidates);

            if (best is null)
            {
                return global with
                {
                    Risk = directCandidate.Path.Risk,
                    RequiresReplan = true,
                    Source = nameof(CorridorLocalPlanner),
                    Summary =
                        "LOCAL_NO_CANDIDATE_FALLBACK " +
                        criticalSummary +
                        " " +
                        directCandidate.Diagnostics
                };
            }

            return best.Path with
            {
                Source = nameof(CorridorLocalPlanner),
                Summary =
                    $"NAVCORE_SELECTED kind={best.Kind} " +
                    $"score={best.Score:F2} " +
                    $"feasible={best.IsFeasible} safe={best.IsSafe} " +
                    $"progress={best.Progress:F2} " +
                    $"length={best.PathLengthMeters:F2}m " +
                    $"risk={best.Path.Risk.RiskScore:F2} " +
                    criticalSummary +
                    " " +
                    best.Diagnostics
            };
        }

        private static string BuildCriticalGateSummary(
            bool hasCriticalRouteObstacle,
            int planningBlockerCount,
            IReadOnlyList<CriticalRouteObstacle> criticalRouteObstacles)
        {
            if (!hasCriticalRouteObstacle || criticalRouteObstacles.Count == 0)
            {
                return
                    $"CRITICAL_GATE clear " +
                    $"blockers={planningBlockerCount} " +
                    $"critical=0";
            }

            var top = criticalRouteObstacles
                .OrderByDescending(x => x.Severity)
                .First();

            return
                $"CRITICAL_GATE active " +
                $"blockers={planningBlockerCount} " +
                $"critical={criticalRouteObstacles.Count} " +
                $"top={top.ObstacleId} " +
                $"reason={top.Reason} " +
                $"t={top.ProjectionT:F2} " +
                $"pClear={top.PhysicalClearanceMeters:F2} " +
                $"sClear={top.SafetyClearanceMeters:F2} " +
                $"req={top.RequiredPhysicalClearanceMeters:F2} " +
                $"severity={top.Severity:F2} " +
                $"directSuppressed=True " +
                $"corridorSuppressed=True";
        }

        private static LocalPathCandidate BuildDirectCandidate(
            PlanningContext context,
            PlannedPath global,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var risk = EvaluateLineRisk(
                context,
                start,
                target,
                blocking);

            var path = global with
            {
                Risk = risk,
                RequiresReplan = risk.RequiresReplan,
                Source = nameof(CorridorLocalPlanner),
                Summary = $"DIRECT {risk.Summary}"
            };

            return ScorePathCandidate(
                kind: "direct",
                context: context,
                start: start,
                target: target,
                path: path.Sanitized(),
                diagnostics: risk.Summary);
        }

        private static LocalPathCandidate BuildWorldCorridorCandidate(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<CorridorGate> corridor,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var directRisk = EvaluateLineRisk(
                context,
                start,
                target,
                blocking);

            var path = BuildCorridorPath(
                context,
                start,
                target,
                corridor,
                directRisk,
                blocking);

            return ScorePathCandidate(
                kind: "world-corridor",
                context: context,
                start: start,
                target: target,
                path: path.Sanitized(),
                diagnostics: path.Summary);
        }

        private static IReadOnlyList<LocalPathCandidate> BuildDetourCandidates(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            /*
             * Paket-7C.1 - Obstacle-centered bypass generation.
             *
             * The previous generic detour fan used route fractions and lateral offsets.
             * That is not enough for real autonomy. If an obstacle intersects the
             * start-target corridor, the bypass candidates must be generated from
             * the obstacle geometry itself.
             *
             * This method now:
             *  - detects critical blockers on the current route segment,
             *  - creates left/right bypass points around each blocker,
             *  - scores them with the existing risk/scoring pipeline,
             *  - keeps the old generic detour fan only as fallback coverage.
             */
            var nonCorridorBlocking = blocking
                .Where(x => x.IsActive)
                .Where(x => !IsCorridorMarker(x))
                .Where(x => !IsBoundaryLike(x))
                .Where(x => double.IsFinite(x.X) && double.IsFinite(x.Y))
                .ToArray();

            if (nonCorridorBlocking.Length == 0)
                return Array.Empty<LocalPathCandidate>();

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);

            if (len <= 1e-6)
                return Array.Empty<LocalPathCandidate>();

            var ux = dx / len;
            var uy = dy / len;

            var nx = -uy;
            var ny = ux;

            var vehicleRadius = Math.Max(0.0, context.VehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, context.SafetyMarginMeters);
            var speedBuffer = Math.Clamp(context.MaxPlanSpeedMps * 0.22, 0.0, 0.75);

            var preferredPhysicalClearance = Math.Clamp(
                safetyMargin + 0.85 + speedBuffer,
                1.05,
                2.40);

            var boundaryBand = TryResolveLocalBoundaryBand(
                context,
                start,
                target);

            var candidates = new List<LocalPathCandidate>();

            double ProjectRouteT(Vec3 point)
            {
                var px = point.X - start.X;
                var py = point.Y - start.Y;

                return (px * ux + py * uy) / len;
            }

            void AddCandidate(Vec3 rawDetour, string tag)
            {
                var rawT = ProjectRouteT(rawDetour);

                /*
                 * The bypass point must be roughly in the local route window.
                 * Large negative points are behind the vehicle and cause corner-cutting
                 * / reverse-looking behavior.
                 */
                if (rawT < -0.08 || rawT > 1.20)
                    return;

                var detour = ClampDetourToBoundaryBand(
                    rawDetour,
                    boundaryBand,
                    out var boundaryTag);

                /*
                 * Clamp may move a valid raw bypass point onto a boundary line behind
                 * the vehicle. Such a point must never become local-detour; otherwise
                 * trajectory sticks to a past point and the boat cuts into the obstacle.
                 */
                var clampedT = ProjectRouteT(detour);
                if (clampedT < 0.06 || clampedT > 1.20)
                    return;

                var toDetourX = detour.X - start.X;
                var toDetourY = detour.Y - start.Y;
                var forwardDot = toDetourX * ux + toDetourY * uy;

                if (forwardDot < 0.25)
                    return;

                if (Distance2D(start, detour) < 0.60)
                    return;

                if (Distance2D(detour, target) < 0.60)
                    return;

                if (candidates.Any(x =>
                        x.Path.Points.Count >= 2 &&
                        Distance2D(x.Path.Points[1].Position, detour) < DuplicatePointDistanceMeters))
                {
                    return;
                }

                var candidatePath = BuildDetourPath(
                    context,
                    start,
                    detour,
                    target,
                    blocking,
                    tag: tag + boundaryTag);

                candidates.Add(
                    ScorePathCandidate(
                        kind: "detour",
                        context: context,
                        start: start,
                        target: target,
                        path: candidatePath,
                        diagnostics: candidatePath.Summary));
            }

            var criticalObstacles = nonCorridorBlocking
                .Select(obj =>
                {
                    var center = new Vec3(obj.X, obj.Y, obj.Z);
                    var rawT = ProjectRouteT(center);
                    var clampedT = Math.Clamp(rawT, 0.0, 1.0);

                    var closest = new Vec3(
                        start.X + ux * len * clampedT,
                        start.Y + uy * len * clampedT,
                        start.Z + (target.Z - start.Z) * clampedT);

                    var distanceToRoute = Distance2D(center, closest);
                    var obstacleRadius = Math.Max(0.0, obj.Radius);
                    var physicalClearance = distanceToRoute - obstacleRadius - vehicleRadius;
                    var safetyClearance = physicalClearance - safetyMargin;

                    var isInRouteWindow = rawT >= -0.10 && rawT <= 1.15;
                    var isCritical =
                        isInRouteWindow &&
                        physicalClearance < preferredPhysicalClearance;

                    return new
                    {
                        Obj = obj,
                        Center = center,
                        RawT = rawT,
                        ClampedT = clampedT,
                        Closest = closest,
                        ObstacleRadius = obstacleRadius,
                        PhysicalClearance = physicalClearance,
                        SafetyClearance = safetyClearance,
                        IsCritical = isCritical
                    };
                })
                .Where(x => x.IsCritical)
                .OrderBy(x => x.SafetyClearance)
                .ThenBy(x => x.RawT)
                .Take(4)
                .ToArray();

            /*
             * 1) Obstacle-centered candidates.
             * These are the serious candidates. They are generated from the blocker
             * geometry, not from arbitrary route fractions.
             */
            foreach (var blocker in criticalObstacles)
            {
                var baseClearance = Math.Clamp(
                    blocker.ObstacleRadius + vehicleRadius + safetyMargin + 0.75 + speedBuffer,
                    1.75,
                    5.00);

                var forwardBias = Math.Clamp(
                    blocker.ObstacleRadius + vehicleRadius * 0.50 + 0.85,
                    0.80,
                    2.40);

                var clearanceMultipliers = new[] { 0.95, 1.15, 1.45 };
                var signs = new[] { 1.0, -1.0 };

                foreach (var sign in signs)
                {
                    foreach (var multiplier in clearanceMultipliers)
                    {
                        var lateralOffset = baseClearance * multiplier;

                        /*
                         * Bypass point is placed around the obstacle itself and pushed
                         * slightly forward along the route. This prevents the planner from
                         * producing a detour point behind the vehicle after the boat has
                         * already approached the obstacle.
                         */
                        var rawDetour = new Vec3(
                            blocker.Center.X + nx * sign * lateralOffset + ux * forwardBias,
                            blocker.Center.Y + ny * sign * lateralOffset + uy * forwardBias,
                            blocker.Center.Z);

                        AddCandidate(
                            rawDetour,
                            tag:
                                $"obstacle={blocker.Obj.Id}," +
                                $"side={(sign > 0.0 ? "L" : "R")}," +
                                $"mode=obstacle-centered," +
                                $"t={blocker.RawT:F2}," +
                                $"obsR={blocker.ObstacleRadius:F2}," +
                                $"pClear={blocker.PhysicalClearance:F2}," +
                                $"sClear={blocker.SafetyClearance:F2}," +
                                $"lat={lateralOffset:F2}," +
                                $"fwd={forwardBias:F2}");
                    }
                }
            }

            /*
             * 2) Generic fallback fan.
             * This remains as coverage for cases where there is no single dominant
             * blocker, but it is no longer the primary obstacle avoidance strategy.
             */
            var baseOffset = Math.Max(
                vehicleRadius + safetyMargin + 1.0,
                2.2);

            var fractions = new[] { 0.25, 0.38, 0.50, 0.62, 0.76 };
            var multipliers = new[] { 0.85, 1.15, 1.55, 2.05, 2.65 };
            var genericSigns = new[] { 1.0, -1.0 };

            foreach (var fraction in fractions)
            {
                var anchor = new Vec3(
                    start.X + dx * fraction,
                    start.Y + dy * fraction,
                    start.Z + (target.Z - start.Z) * fraction);

                foreach (var sign in genericSigns)
                {
                    foreach (var multiplier in multipliers)
                    {
                        var offset = baseOffset * multiplier;

                        var rawDetour = new Vec3(
                            anchor.X + nx * sign * offset,
                            anchor.Y + ny * sign * offset,
                            anchor.Z);

                        AddCandidate(
                            rawDetour,
                            tag:
                                $"mode=generic-fallback," +
                                $"f={fraction:F2}," +
                                $"side={(sign > 0 ? "L" : "R")}," +
                                $"off={offset:F2}");
                    }
                }
            }

            return candidates;
        }

        private static CorridorBoundaryBand? TryResolveLocalBoundaryBand(
            PlanningContext context,
            Vec3 start,
            Vec3 target)
        {
            var objects = context.WorldObjects ?? Array.Empty<HydronomWorldObject>();

            if (objects.Count == 0)
                return null;

            var minX = Math.Min(start.X, target.X) - BoundaryRelevantXPadMeters;
            var maxX = Math.Max(start.X, target.X) + BoundaryRelevantXPadMeters;

            var boundaryObjects = objects
                .Where(x => x.IsActive)
                .Where(IsBoundaryLike)
                .Where(x => double.IsFinite(x.X) && double.IsFinite(x.Y))
                .Where(x => x.X >= minX && x.X <= maxX)
                .ToArray();

            /*
             * Eğer lokal X penceresinde boundary bulamazsak tüm boundary objelerine düş.
             * Bu fallback kapalı kalmasın diye var ama lokal pencere öncelikli.
             */
            if (boundaryObjects.Length < 2)
            {
                boundaryObjects = objects
                    .Where(x => x.IsActive)
                    .Where(IsBoundaryLike)
                    .Where(x => double.IsFinite(x.X) && double.IsFinite(x.Y))
                    .ToArray();
            }

            if (boundaryObjects.Length < 2)
                return null;

            var leftYs = boundaryObjects
                .Where(IsLeftMarker)
                .Select(x => x.Y)
                .Where(double.IsFinite)
                .OrderBy(x => x)
                .ToArray();

            var rightYs = boundaryObjects
                .Where(IsRightMarker)
                .Select(x => x.Y)
                .Where(double.IsFinite)
                .OrderBy(x => x)
                .ToArray();

            double minY;
            double maxY;

            if (leftYs.Length > 0 && rightYs.Length > 0)
            {
                var leftY = Median(leftYs);
                var rightY = Median(rightYs);

                minY = Math.Min(leftY, rightY);
                maxY = Math.Max(leftY, rightY);
            }
            else
            {
                var allYs = boundaryObjects
                    .Select(x => x.Y)
                    .Where(double.IsFinite)
                    .OrderBy(x => x)
                    .ToArray();

                if (allYs.Length < 2)
                    return null;

                minY = allYs.First();
                maxY = allYs.Last();
            }

            if (!double.IsFinite(minY) ||
                !double.IsFinite(maxY) ||
                maxY - minY < BoundaryMinimumUsableWidthMeters)
            {
                return null;
            }

            /*
             * Margin:
             * Fiziksel gövde payı. SafetyMargin tamamını kullanırsak dar koridorlarda
             * rota gereksiz boğulur. Bu yüzden vehicleRadius + küçük tampon kullanıyoruz.
             */
            var margin = Math.Clamp(
                Math.Max(0.0, context.VehicleRadiusMeters) + 0.10,
                0.65,
                1.10);

            if (maxY - minY <= margin * 2.0 + 0.75)
                margin = Math.Max(0.20, (maxY - minY - 0.75) * 0.50);

            if (margin <= 0.0 || maxY - minY <= margin * 2.0)
                return null;

            return new CorridorBoundaryBand(
                MinY: minY + margin,
                MaxY: maxY - margin,
                RawMinY: minY,
                RawMaxY: maxY,
                Margin: margin);
        }

        private static Vec3 ClampDetourToBoundaryBand(
            Vec3 rawDetour,
            CorridorBoundaryBand? boundaryBand,
            out string tag)
        {
            tag = "";

            if (boundaryBand is null)
                return rawDetour;

            var band = boundaryBand.Value;

            var clampedY = Math.Clamp(
                rawDetour.Y,
                band.MinY,
                band.MaxY);

            if (Math.Abs(clampedY - rawDetour.Y) <= 1e-6)
            {
                tag =
                    $",boundary=in" +
                    $",band=[{band.MinY:F2},{band.MaxY:F2}]";
                return rawDetour;
            }

            tag =
                $",boundary=clamped" +
                $",rawY={rawDetour.Y:F2}" +
                $",clampedY={clampedY:F2}" +
                $",band=[{band.MinY:F2},{band.MaxY:F2}]" +
                $",rawBand=[{band.RawMinY:F2},{band.RawMaxY:F2}]" +
                $",margin={band.Margin:F2}";

            return new Vec3(
                rawDetour.X,
                clampedY,
                rawDetour.Z);
        }

        private static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return 0.0;

            var mid = values.Count / 2;

            if (values.Count % 2 == 1)
                return values[mid];

            return (values[mid - 1] + values[mid]) * 0.5;
        }

        private readonly record struct CorridorBoundaryBand(
            double MinY,
            double MaxY,
            double RawMinY,
            double RawMaxY,
            double Margin);

        private sealed record LocalPathCandidate(
            string Kind,
            PlannedPath Path,
            double Score,
            bool IsFeasible,
            bool IsSafe,
            bool HasCollision,
            double Progress,
            double PathLengthMeters,
            double MinimumPhysicalClearanceMeters,
            double MinimumSafetyClearanceMeters,
            string Diagnostics);
    }
}