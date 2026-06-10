using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    public sealed partial class CorridorLocalPlanner
    {
        private static LocalPathCandidate? SelectBestCandidate(
            IReadOnlyList<LocalPathCandidate> candidates)
        {
            if (candidates.Count == 0)
                return null;

            var valid = candidates
                .Where(x => x.Path.IsValid && x.Path.Points.Count >= 2)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (valid.Length == 0)
                return null;

            // Öncelik: fiziksel collision olmayan ve safety envelope içinde kalan rota.
            var safe = valid
                .Where(x => x.IsFeasible && x.IsSafe && !x.HasCollision)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (safe.Length > 0)
                return safe[0];

            // İkinci öncelik: fiziksel collision yok ama safety envelope ihlali olabilir.
            // Böyle durumda sistem "tam dur" yerine düşük hız/cautious path üretebilir.
            var feasible = valid
                .Where(x => x.IsFeasible && !x.HasCollision)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (feasible.Length > 0)
                return feasible[0];

            // Son çare: hepsi kötü. En az kötü olanı döndür ama summary zaten bunu gösterecek.
            return valid[0];
        }

        private static LocalPathCandidate ScorePathCandidate(
            string kind,
            PlanningContext context,
            Vec3 start,
            Vec3 target,
            PlannedPath path,
            string diagnostics)
        {
            var points = path.Points
                .Select(x => x.Position)
                .ToArray();

            var pathLength = PathLength2D(points);
            var directLength = Math.Max(0.01, Distance2D(start, target));

            var progress = ComputePathProgress(start, target, points);
            var smoothnessPenalty = ComputeSmoothnessPenalty(points);
            var lengthPenalty = Math.Clamp((pathLength / directLength) - 1.0, 0.0, 4.0);

            var minPhysicalClearance = path.Risk.MinimumClearanceMeters;

            // Mevcut PlanningRiskReport yalnızca MinimumClearanceMeters taşıyor.
            // 6E içinde bu değer physical clearance olarak yorumlanır.
            // Safety clearance risk summary'de görünür, scoring tarafında risk ve blocking count ile temsil edilir.
            var hasCollision = minPhysicalClearance < 0.0 || path.Risk.RiskScore >= 0.999;
            var isFeasible = !hasCollision && minPhysicalClearance >= 0.0;
            var isSafe = isFeasible && !path.Risk.RequiresReplan && path.Risk.RiskScore < 0.90;

            var clearanceReward = ComputeClearanceReward(minPhysicalClearance);
            var progressReward = progress * 24.0;
            var riskPenalty = path.Risk.RiskScore * 36.0;
            var blockingPenalty = Math.Min(12.0, path.Risk.BlockingObjectCount * 3.5);
            var collisionPenalty = hasCollision ? CandidateRejectCollisionPenalty : 0.0;
            var unsafePenalty = !isSafe ? CandidateUnsafePenalty * Math.Clamp(path.Risk.RiskScore, 0.0, 1.0) : 0.0;
            var staleCorridorPenalty = ComputeStaleCorridorPenalty(kind, start, target, points);

            var kindBias = kind switch
            {
                "direct" => path.Risk.RequiresReplan ? -12.0 : 8.0,
                "world-corridor" => 2.0,
                "detour" => 0.0,
                _ => 0.0
            };

            var score =
                kindBias +
                clearanceReward +
                progressReward -
                riskPenalty -
                blockingPenalty -
                collisionPenalty -
                unsafePenalty -
                staleCorridorPenalty -
                lengthPenalty * 8.0 -
                smoothnessPenalty * 7.0;

            var candidateDiagnostics =
                $"{diagnostics} " +
                $"candidate kind={kind} " +
                $"score={score:F2} " +
                $"progress={progress:F2} " +
                $"len={pathLength:F2}m " +
                $"directLen={directLength:F2}m " +
                $"smoothPenalty={smoothnessPenalty:F2} " +
                $"lenPenalty={lengthPenalty:F2} " +
                $"collision={hasCollision} " +
                $"feasible={isFeasible} " +
                $"safe={isSafe}";

            return new LocalPathCandidate(
                Kind: kind,
                Path: path,
                Score: score,
                IsFeasible: isFeasible,
                IsSafe: isSafe,
                HasCollision: hasCollision,
                Progress: progress,
                PathLengthMeters: pathLength,
                MinimumPhysicalClearanceMeters: minPhysicalClearance,
                MinimumSafetyClearanceMeters: double.NaN,
                Diagnostics: candidateDiagnostics);
        }

        private static double ComputeClearanceReward(double minPhysicalClearance)
        {
            if (!double.IsFinite(minPhysicalClearance))
                return 18.0;

            if (minPhysicalClearance < 0.0)
                return -120.0 - Math.Abs(minPhysicalClearance) * 30.0;

            if (minPhysicalClearance < 0.25)
                return -28.0;

            if (minPhysicalClearance < 0.60)
                return -10.0;

            if (minPhysicalClearance < 1.20)
                return 5.0;

            if (minPhysicalClearance < 2.50)
                return 12.0;

            return 18.0;
        }

        private static double ComputePathProgress(
            Vec3 start,
            Vec3 target,
            IReadOnlyList<Vec3> points)
        {
            if (points.Count == 0)
                return 0.0;

            var bestProjection = 0.0;

            foreach (var point in points)
            {
                bestProjection = Math.Max(
                    bestProjection,
                    ProjectionAlongSegment(point, start, target));
            }

            return Math.Clamp(bestProjection, -0.25, 1.25);
        }

        private static double ComputeSmoothnessPenalty(
            IReadOnlyList<Vec3> points)
        {
            if (points.Count < 3)
                return 0.0;

            var total = 0.0;
            var count = 0;

            for (var i = 1; i < points.Count - 1; i++)
            {
                var h1 = HeadingDeg(points[i - 1], points[i]);
                var h2 = HeadingDeg(points[i], points[i + 1]);

                total += Math.Abs(DeltaAngleDeg(h1, h2)) / 180.0;
                count++;
            }

            return count == 0 ? 0.0 : total / count;
        }

        private static double ComputeStaleCorridorPenalty(
            string kind,
            Vec3 start,
            Vec3 target,
            IReadOnlyList<Vec3> points)
        {
            if (!kind.Equals("world-corridor", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            if (points.Count < 2)
                return CandidateStaleCorridorPenalty;

            // İlk gerçek hedef noktası local-start sonrası gelen noktadır.
            var firstNavigationPoint = points.Count >= 2
                ? points[1]
                : points[0];

            var projection = ProjectionAlongSegment(firstNavigationPoint, start, target);
            var distanceFromStart = Distance2D(start, firstNavigationPoint);

            var penalty = 0.0;

            if (projection < 0.06)
                penalty += CandidateStaleCorridorPenalty;

            if (projection > 1.15)
                penalty += CandidateStaleCorridorPenalty * 0.6;

            if (distanceFromStart < 0.75)
                penalty += CandidateStaleCorridorPenalty * 0.8;

            return penalty;
        }
    }
}