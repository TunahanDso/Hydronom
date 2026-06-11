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
        /// <summary>
        /// Paket-8F:
        /// World-aware obstacle bypass candidate generation + bypass decision telemetry.
        ///
        /// Bu method eski generic detour fan'ın üstünde çalışır.
        /// Önce gerçek world-object semantic classifier kullanır,
        /// sonra start-target hattındaki kritik engelleri bulur,
        /// sonra obstacle-centered left/right bypass candidate üretir.
        ///
        /// Generic BuildDetourCandidates artık sadece fallback'tir.
        /// </summary>
        private static IReadOnlyList<LocalPathCandidate> BuildWorldAwareDetourCandidates(
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<HydronomWorldObject> blocking)
        {
            var safe = (context ?? PlanningContext.Idle).Sanitized();
            var rawBlocking = blocking ?? Array.Empty<HydronomWorldObject>();

            var candidates = new List<LocalPathCandidate>();

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);

            if (len <= 1e-6)
                return BuildDetourCandidates(safe, start, target, rawBlocking);

            var ux = dx / len;
            var uy = dy / len;

            double ProjectRouteT(Vec3 point)
            {
                var px = point.X - start.X;
                var py = point.Y - start.Y;

                return (px * ux + py * uy) / len;
            }

            bool IsForwardUsable(Vec3 point)
            {
                var px = point.X - start.X;
                var py = point.Y - start.Y;

                var forward = px * ux + py * uy;
                var t = forward / len;

                if (t < -0.05 || t > 1.25)
                    return false;

                if (forward < 0.20)
                    return false;

                return double.IsFinite(point.X) &&
                       double.IsFinite(point.Y);
            }

            bool IsDuplicate(Vec3 point)
            {
                return candidates.Any(x =>
                    x.Path.Points.Count >= 2 &&
                    Distance2D(x.Path.Points[1].Position, point) < DuplicatePointDistanceMeters);
            }

            var allWorldObjects =
                safe.WorldObjects is { Count: > 0 }
                    ? safe.WorldObjects
                    : rawBlocking;

            var classified = WorldObjectPlanningClassifier.Classify(
                allWorldObjects,
                safe.VehicleRadiusMeters,
                safe.SafetyMarginMeters,
                source: "planner-world-model");

            var planningBlockers = classified
                .Where(x => x.CanBlockRoute)
                .ToArray();

            var critical = CriticalRouteObstacleDetector.Detect(
                    start,
                    target,
                    planningBlockers,
                    safe.VehicleRadiusMeters,
                    safe.SafetyMarginMeters,
                    safe.MaxPlanSpeedMps,
                    maxResults: 6)
                .ToArray();

            var mostCritical = critical
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();

            var bypassCandidates = ObstacleBypassCandidateFactory.BuildCandidates(
                    start,
                    target,
                    critical,
                    safe.VehicleRadiusMeters,
                    safe.SafetyMarginMeters,
                    safe.MaxPlanSpeedMps,
                    maxObstacles: 4)
                .ToArray();

            var boundaryBand = TryResolveLocalBoundaryBand(
                safe,
                start,
                target);

            var acceptedBypass = 0;
            var rejectedInvalid = 0;
            var rejectedBehindOrOutOfWindow = 0;
            var rejectedTooCloseToStart = 0;
            var rejectedTooCloseToTarget = 0;
            var rejectedDuplicate = 0;

            var decisionPrefix = BuildBypassDecisionPrefix(
                critical,
                bypassCandidates,
                planningBlockers.Length,
                boundaryBand);

            foreach (var bypass in bypassCandidates)
            {
                if (!bypass.IsValid)
                {
                    rejectedInvalid++;
                    continue;
                }

                var clamped = ClampDetourToBoundaryBand(
                    bypass.Point,
                    boundaryBand,
                    out var boundaryTag);

                /*
                 * Boundary clamp geçerli bir sağ/sol bypass noktasını aracın gerisine
                 * veya hedef penceresinin çok dışına taşıyabilir. Böyle bir candidate
                 * kesinlikle local-detour olamaz.
                 */
                if (!IsForwardUsable(clamped))
                {
                    rejectedBehindOrOutOfWindow++;
                    continue;
                }

                if (Distance2D(start, clamped) < 0.60)
                {
                    rejectedTooCloseToStart++;
                    continue;
                }

                if (Distance2D(clamped, target) < 0.60)
                {
                    rejectedTooCloseToTarget++;
                    continue;
                }

                if (IsDuplicate(clamped))
                {
                    rejectedDuplicate++;
                    continue;
                }

                var tag =
                    bypass.Tag +
                    boundaryTag +
                    $",clampedT={ProjectRouteT(clamped):F2}";

                var candidatePath = BuildDetourPath(
                    safe,
                    start,
                    clamped,
                    target,
                    rawBlocking,
                    tag: tag);

                acceptedBypass++;

                var diagnostics =
                    decisionPrefix +
                    " " +
                    "BYPASS_WORLD_AWARE " +
                    bypass.Summary +
                    " " +
                    candidatePath.Summary;

                candidates.Add(
                    ScorePathCandidate(
                        kind: "obstacle-bypass",
                        context: safe,
                        start: start,
                        target: target,
                        path: candidatePath,
                        diagnostics: diagnostics));
            }

            var rejectionSummary =
                $"BYPASS_FILTER accepted={acceptedBypass} " +
                $"rejectInvalid={rejectedInvalid} " +
                $"rejectBehindOrOut={rejectedBehindOrOutOfWindow} " +
                $"rejectNearStart={rejectedTooCloseToStart} " +
                $"rejectNearTarget={rejectedTooCloseToTarget} " +
                $"rejectDuplicate={rejectedDuplicate}";

            /*
             * Fallback:
             * Eğer obstacle-centered bypass hiç üretemezse veya scoring sonunda düşük kalırsa
             * eski generic detour fan hâlâ candidate havuzunda kalır.
             *
             * Ama artık ana strateji bu değildir.
             */
            var genericFallback = BuildDetourCandidates(
                safe,
                start,
                target,
                rawBlocking);

            foreach (var fallback in genericFallback)
            {
                candidates.Add(
                    fallback with
                    {
                        Diagnostics =
                            decisionPrefix +
                            " " +
                            rejectionSummary +
                            " GENERIC_FALLBACK " +
                            fallback.Diagnostics
                    });
            }

            if (critical.Length > 0 && candidates.Count == 0)
            {
                /*
                 * Critical obstacle var ama bypass ve fallback üretilemedi.
                 * Bu durumda local planner aday döndürmez; üst katman replan/escape tarafına düşmeli.
                 */
                return Array.Empty<LocalPathCandidate>();
            }

            if (critical.Length > 0)
            {
                return candidates
                    .OrderBy(x => x.Kind.Equals("obstacle-bypass", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenByDescending(x => x.IsFeasible)
                    .ThenByDescending(x => x.IsSafe)
                    .ThenByDescending(x => x.Score)
                    .ToArray();
            }

            return candidates
                .OrderByDescending(x => x.Score)
                .ToArray();
        }

        private static string BuildBypassDecisionPrefix(
            IReadOnlyList<CriticalRouteObstacle> critical,
            IReadOnlyList<ObstacleBypassCandidate> bypassCandidates,
            int blockerCount,
            CorridorBoundaryBand? boundaryBand)
        {
            if (critical.Count == 0)
            {
                return
                    $"BYPASS_DECISION none " +
                    $"blockers={blockerCount} " +
                    $"rawBypass={bypassCandidates.Count} " +
                    $"boundary={(boundaryBand is null ? "none" : "active")}";
            }

            var top = critical
                .OrderByDescending(x => x.Severity)
                .First();

            var leftCount = bypassCandidates.Count(x => x.Side == ObstacleBypassSide.Left);
            var rightCount = bypassCandidates.Count(x => x.Side == ObstacleBypassSide.Right);

            var sideHint = ResolvePreferredSideHint(
                bypassCandidates);

            return
                $"BYPASS_DECISION blocker={top.ObstacleId} " +
                $"reason={top.Reason} " +
                $"t={top.ProjectionT:F2} " +
                $"pClear={top.PhysicalClearanceMeters:F2} " +
                $"sClear={top.SafetyClearanceMeters:F2} " +
                $"req={top.RequiredPhysicalClearanceMeters:F2} " +
                $"severity={top.Severity:F2} " +
                $"criticalCount={critical.Count} " +
                $"blockers={blockerCount} " +
                $"rawBypass={bypassCandidates.Count} " +
                $"left={leftCount} " +
                $"right={rightCount} " +
                $"sideHint={sideHint} " +
                $"boundary={(boundaryBand is null ? "none" : "active")}";
        }

        private static string ResolvePreferredSideHint(
            IReadOnlyList<ObstacleBypassCandidate> bypassCandidates)
        {
            if (bypassCandidates.Count == 0)
                return "none";

            var left = bypassCandidates
                .Where(x => x.Side == ObstacleBypassSide.Left)
                .OrderBy(x => x.LateralOffsetMeters)
                .FirstOrDefault();

            var right = bypassCandidates
                .Where(x => x.Side == ObstacleBypassSide.Right)
                .OrderBy(x => x.LateralOffsetMeters)
                .FirstOrDefault();

            if (left is null && right is null)
                return "none";

            if (left is null)
                return "Right";

            if (right is null)
                return "Left";

            /*
             * CandidateFactory simetrik üretir; gerçek karar scoring tarafından verilir.
             * Burada sadece telemetry için hangi tarafın daha küçük offset ile mümkün
             * göründüğünü yazıyoruz.
             */
            if (left.LateralOffsetMeters < right.LateralOffsetMeters - 0.05)
                return "Left";

            if (right.LateralOffsetMeters < left.LateralOffsetMeters - 0.05)
                return "Right";

            return "balanced";
        }
    }
}