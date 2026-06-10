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
    /// Paket-7A.4:
    /// Local detour noktaları artık parkur/boundary çizgisinin dışına taşamaz.
    ///
    /// Kök problem:
    /// Slalomda planner engelden kaçarken local-detour@(45.2,8.3) gibi
    /// boundary arkasına taşan nokta üretebiliyordu. Sonra trajectory bu noktaya bakıyor,
    /// GEOM-AUTH / risk zinciri safe=false / planRisk=0.95 tarafına düşüyordu.
    ///
    /// Yeni davranış:
    /// - WorldObjects içindeki left_boundary/right_boundary objelerinden yerel Y bandı çözülür.
    /// - Detour noktası bu bandın içine, araç yarıçapı payıyla clamp edilir.
    /// - Clamp edilen aday summary/tag içinde görünür.
    /// - Boundary bulunamazsa eski davranış korunur.
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

            var candidates = new List<LocalPathCandidate>();

            var directCandidate = BuildDirectCandidate(
                safe,
                global,
                start,
                target,
                blocking);

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

            var detourCandidates = BuildDetourCandidates(
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
                    Summary = "LOCAL_NO_CANDIDATE_FALLBACK " + directCandidate.Diagnostics
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
                    best.Diagnostics
            };
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
            var nonCorridorBlocking = blocking
                .Where(x => !IsCorridorMarker(x))
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

            var baseOffset = Math.Max(
                context.VehicleRadiusMeters + context.SafetyMarginMeters + 1.0,
                2.2);

            var fractions = new[] { 0.25, 0.38, 0.50, 0.62, 0.76 };
            var multipliers = new[] { 0.85, 1.15, 1.55, 2.05, 2.65 };
            var signs = new[] { 1.0, -1.0 };

            var boundaryBand = TryResolveLocalBoundaryBand(
                context,
                start,
                target);

            var candidates = new List<LocalPathCandidate>();

            foreach (var fraction in fractions)
            {
                var anchor = new Vec3(
                    start.X + dx * fraction,
                    start.Y + dy * fraction,
                    start.Z + (target.Z - start.Z) * fraction);

                foreach (var sign in signs)
                {
                    foreach (var multiplier in multipliers)
                    {
                        var offset = baseOffset * multiplier;

                        var rawDetour = new Vec3(
                            anchor.X + nx * sign * offset,
                            anchor.Y + ny * sign * offset,
                            anchor.Z);

                        var detour = ClampDetourToBoundaryBand(
                            rawDetour,
                            boundaryBand,
                            out var boundaryTag);

                        var tag =
                            $"f={fraction:F2}," +
                            $"side={(sign > 0 ? "L" : "R")}," +
                            $"off={offset:F2}" +
                            boundaryTag;

                        var candidatePath = BuildDetourPath(
                            context,
                            start,
                            detour,
                            target,
                            blocking,
                            tag: tag);

                        candidates.Add(
                            ScorePathCandidate(
                                kind: "detour",
                                context: context,
                                start: start,
                                target: target,
                                path: candidatePath,
                                diagnostics: candidatePath.Summary));
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