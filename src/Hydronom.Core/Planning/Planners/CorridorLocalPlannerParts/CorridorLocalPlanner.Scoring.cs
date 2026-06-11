using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Core.Planning.Planners
{
    public sealed partial class CorridorLocalPlanner
    {
        /*
         * Clearance-aware optimal corridor policy.
         *
         * Planner must not optimize only for shortest progress.
         * A path that scrapes a buoy/blocker is a high-probability recovery trigger.
         *
         * Values below are physical-clearance values after obstacle radius
         * and vehicle radius are already subtracted.
         */
        private const double PlannerAbsoluteMinimumPhysicalClearanceMeters = 0.35;
        private const double PlannerComfortPhysicalClearanceMeters = 0.85;
        private const double PlannerPreferredPhysicalClearanceMeters = 1.15;
        private const double PlannerSpeedClearanceGainMeters = 0.30;

        private static LocalPathCandidate? SelectBestCandidate(
            IReadOnlyList<LocalPathCandidate> candidates)
        {
            if (candidates.Count == 0)
                return null;

            var valid = candidates
                .Where(x => x.Path.IsValid && x.Path.Points.Count >= 2)
                .ToArray();

            if (valid.Length == 0)
                return null;

            /*
             * Paket-8E:
             * Collision candidate normal navigation için seçilemez.
             *
             * Eski davranışta collision candidate sadece çok ceza alıyordu; tüm adaylar kötü olunca
             * yine valid[0] dönebiliyordu. Bu slalomda "duba içine kafa atma" üretiyordu.
             */
            var nonCollision = valid
                .Where(x => !x.HasCollision)
                .ToArray();

            /*
             * 1) Güvenli obstacle-bypass her şeyden önce gelir.
             * Çünkü critical obstacle varsa direct/world-corridor hâlâ hedefe fazla sadık kalabiliyor.
             */
            var safeObstacleBypass = nonCollision
                .Where(x =>
                    x.Kind.Equals("obstacle-bypass", StringComparison.OrdinalIgnoreCase) &&
                    x.IsFeasible &&
                    x.IsSafe)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (safeObstacleBypass.Length > 0)
                return safeObstacleBypass[0];

            /*
             * 2) Genel safe candidate.
             */
            var safe = nonCollision
                .Where(x => x.IsFeasible && x.IsSafe)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (safe.Length > 0)
                return safe[0];

            /*
             * 3) Eğer safe yoksa ama feasible obstacle-bypass varsa onu seç.
             * Bu, dar parkurda "tam güvenli değil ama engelden bilinçli kaçıyor" davranışını
             * direct/corridor hedefine göre daha doğru yapar.
             */
            var feasibleObstacleBypass = nonCollision
                .Where(x =>
                    x.Kind.Equals("obstacle-bypass", StringComparison.OrdinalIgnoreCase) &&
                    x.IsFeasible)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (feasibleObstacleBypass.Length > 0)
                return feasibleObstacleBypass[0];

            /*
             * 4) Collision olmayan feasible candidate.
             */
            var feasible = nonCollision
                .Where(x => x.IsFeasible)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (feasible.Length > 0)
                return feasible[0];

            /*
             * 5) Collision olmayan ama risky candidate varsa döndür.
             * Bu path RequiresReplan taşıyabilir; üst katman replan/avoid/escape'e gidebilir.
             */
            var riskyNonCollision = nonCollision
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (riskyNonCollision.Length > 0)
                return riskyNonCollision[0];

            /*
             * 6) Hepsi collision ise local planner normal candidate seçmesin.
             * RefineLocal fallback'te direct risk ile RequiresReplan dönecek.
             */
            return null;
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

            /*
             * PlanningRiskReport currently carries MinimumClearanceMeters only.
             * Here it is treated as physical clearance.
             */
            var hasCollision = minPhysicalClearance < 0.0 || path.Risk.RiskScore >= 0.999;
            var isFeasible = !hasCollision && minPhysicalClearance >= 0.0;

            var absoluteMinimumClearance = ComputeAbsoluteMinimumPhysicalClearance(context);
            var requiredClearance = ComputeRequiredPhysicalClearance(context, path.Risk.RiskScore);
            var closePassPenalty = ComputeClosePassPenalty(minPhysicalClearance, requiredClearance);
            var recoveryRiskPenalty = ComputeRecoveryRiskPenalty(minPhysicalClearance, requiredClearance, path.Risk.RiskScore);
            var speedClearancePenalty = ComputeSpeedClearancePenalty(context, minPhysicalClearance, requiredClearance);

            var isTooCloseForNominalNavigation =
                double.IsFinite(minPhysicalClearance) &&
                minPhysicalClearance < absoluteMinimumClearance;

            var isSafe =
                isFeasible &&
                !isTooCloseForNominalNavigation &&
                !path.Risk.RequiresReplan &&
                path.Risk.RiskScore < 0.90;

            var clearanceReward = ComputeClearanceReward(minPhysicalClearance);
            var progressReward = progress * 24.0;
            var riskPenalty = path.Risk.RiskScore * 36.0;
            var blockingPenalty = Math.Min(12.0, path.Risk.BlockingObjectCount * 3.5);
            var collisionPenalty = hasCollision ? CandidateRejectCollisionPenalty : 0.0;
            var unsafePenalty = !isSafe ? CandidateUnsafePenalty * Math.Clamp(path.Risk.RiskScore, 0.0, 1.0) : 0.0;
            var staleCorridorPenalty = ComputeStaleCorridorPenalty(kind, start, target, points);

            var kindBias = kind switch
            {
                /*
                 * Direct sadece risksizse ödül alır.
                 * Replan istiyorsa veya collision'a yaklaşıyorsa artık ana aday olamaz.
                 */
                "direct" => path.Risk.RequiresReplan ? -24.0 : 6.0,

                /*
                 * World corridor iyi bir şey ama physical obstacle bypass üstüne çıkamaz.
                 */
                "world-corridor" => path.Risk.RequiresReplan ? -18.0 : 1.5,

                /*
                 * Yeni obstacle-centered bypass ana kaçınma davranışıdır.
                 */
                "obstacle-bypass" => 18.0,

                /*
                 * Generic detour fallback.
                 */
                "detour" => -1.0,

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
                closePassPenalty -
                recoveryRiskPenalty -
                speedClearancePenalty -
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
                $"safe={isSafe} " +
                $"minClear={FormatClearanceForCandidate(minPhysicalClearance)} " +
                $"reqClear={requiredClearance:F2} " +
                $"absClear={absoluteMinimumClearance:F2} " +
                $"closePenalty={closePassPenalty:F2} " +
                $"speedClearPenalty={speedClearancePenalty:F2} " +
                $"recoveryPenalty={recoveryRiskPenalty:F2} " +
                $"kindBias={kindBias:F2}";

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
                return -180.0 - Math.Abs(minPhysicalClearance) * 80.0;

            if (minPhysicalClearance < 0.25)
                return -95.0;

            if (minPhysicalClearance < 0.45)
                return -62.0;

            if (minPhysicalClearance < 0.70)
                return -28.0;

            if (minPhysicalClearance < 1.10)
                return 3.0;

            if (minPhysicalClearance < 2.50)
                return 15.0;

            return 20.0;
        }

        private static double ComputeAbsoluteMinimumPhysicalClearance(PlanningContext context)
        {
            var vehicleRadius = Math.Max(0.0, context.VehicleRadiusMeters);
            var safetyMargin = Math.Max(0.0, context.SafetyMarginMeters);

            return Math.Clamp(
                PlannerAbsoluteMinimumPhysicalClearanceMeters + safetyMargin * 0.25 + vehicleRadius * 0.10,
                0.35,
                0.70);
        }

        private static double ComputeRequiredPhysicalClearance(
            PlanningContext context,
            double riskScore)
        {
            var maxPlanSpeed = Math.Clamp(context.MaxPlanSpeedMps, 0.0, 3.0);
            var speedTerm = Math.Clamp(maxPlanSpeed * PlannerSpeedClearanceGainMeters, 0.0, 0.85);
            var riskTerm = Math.Clamp(riskScore, 0.0, 1.0) * 0.35;

            return Math.Clamp(
                PlannerComfortPhysicalClearanceMeters + speedTerm + riskTerm,
                PlannerComfortPhysicalClearanceMeters,
                PlannerPreferredPhysicalClearanceMeters + 0.95);
        }

        private static double ComputeClosePassPenalty(
            double minPhysicalClearance,
            double requiredClearance)
        {
            if (!double.IsFinite(minPhysicalClearance))
                return 0.0;

            if (minPhysicalClearance < 0.0)
                return 1_000.0;

            var deficit = Math.Max(0.0, requiredClearance - minPhysicalClearance);
            if (deficit <= 1e-6)
                return 0.0;

            var normalized = deficit / Math.Max(0.25, requiredClearance);
            return 18.0 + normalized * normalized * 115.0;
        }

        private static double ComputeSpeedClearancePenalty(
            PlanningContext context,
            double minPhysicalClearance,
            double requiredClearance)
        {
            if (!double.IsFinite(minPhysicalClearance))
                return 0.0;

            var maxPlanSpeed = Math.Clamp(context.MaxPlanSpeedMps, 0.0, 3.0);
            var deficit = Math.Max(0.0, requiredClearance - minPhysicalClearance);

            if (deficit <= 1e-6 || maxPlanSpeed <= 0.20)
                return 0.0;

            return deficit * maxPlanSpeed * 32.0;
        }

        private static double ComputeRecoveryRiskPenalty(
            double minPhysicalClearance,
            double requiredClearance,
            double riskScore)
        {
            if (!double.IsFinite(minPhysicalClearance))
                return 0.0;

            if (minPhysicalClearance < 0.0)
                return 1_000.0;

            var closeRatio = Math.Clamp(
                1.0 - minPhysicalClearance / Math.Max(0.25, requiredClearance),
                0.0,
                1.0);

            var risk = Math.Clamp(riskScore, 0.0, 1.0);
            return closeRatio * closeRatio * (35.0 + risk * 80.0);
        }

        private static string FormatClearanceForCandidate(double value)
        {
            if (double.IsPositiveInfinity(value))
                return "inf";

            if (double.IsNegativeInfinity(value))
                return "-inf";

            if (!double.IsFinite(value))
                return "nan";

            return value.ToString("F2");
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

            /*
             * İlk gerçek hedef noktası local-start sonrası gelen noktadır.
             */
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