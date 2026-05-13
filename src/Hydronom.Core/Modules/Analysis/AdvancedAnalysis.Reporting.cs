using System;

namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        private static AdvancedAnalysisReport BuildReport(
            bool hasObstacleAhead,
            double leftClear,
            double rightClear,
            double closestSurfaceDistance,
            int totalObstacleCount,
            int consideredObstacleCount,
            double[] sectorRisk,
            double[] sectorClearance,
            AnalysisParameters parameters,
            PassableCorridorCandidate corridor)
        {
            double[] sectorScore = BuildSectorScores(
                sectorRisk,
                sectorClearance,
                parameters.AheadDistanceM);

            SmoothInPlace(sectorScore, passes: 2);

            int centerIndex = parameters.SectorCount / 2;
            int bestIndex = centerIndex;
            double bestScore = double.MinValue;

            for (int i = 0; i < parameters.SectorCount; i++)
            {
                double sectorAngleDeg = SectorAngleDeg(i, parameters.SectorCount, parameters.HalfFovDeg);
                double centerBiasPenalty = Math.Abs(sectorAngleDeg) / Math.Max(1e-6, parameters.HalfFovDeg) * parameters.CenterBiasWeight;

                double finalScore = sectorScore[i] - centerBiasPenalty;

                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestIndex = i;
                }
            }

            double bestHeadingOffsetDeg = SectorAngleDeg(bestIndex, parameters.SectorCount, parameters.HalfFovDeg);

            double frontRiskScore = ComputeFrontRiskScore(sectorRisk, parameters.SectorCount);
            (double leftScore, double rightScore) = ComputeSideScores(
                sectorScore,
                parameters.SectorCount,
                parameters.HalfFovDeg);

            string suggestedSide = "center";
            if (bestHeadingOffsetDeg < -8.0) suggestedSide = "left";
            else if (bestHeadingOffsetDeg > 8.0) suggestedSide = "right";

            bool hasPassableCorridor = corridor.IsValid;

            if (hasPassableCorridor)
            {
                bestHeadingOffsetDeg = corridor.CenterOffsetDeg;
                suggestedSide = "corridor";
            }

            if (Math.Min(leftClear, rightClear) < parameters.DangerDistanceM ||
                closestSurfaceDistance < parameters.DangerDistanceM ||
                frontRiskScore > parameters.FrontCriticalRiskThreshold)
            {
                hasObstacleAhead = true;
            }

            return new AdvancedAnalysisReport(
                HasObstacleAhead: hasObstacleAhead,
                ClearanceLeft: leftClear,
                ClearanceRight: rightClear,
                FrontRiskScore: frontRiskScore,
                LeftScore: leftScore,
                RightScore: rightScore,
                BestHeadingOffsetDeg: bestHeadingOffsetDeg,
                SuggestedSide: suggestedSide,
                ClosestSurfaceDistanceM: closestSurfaceDistance,
                TotalObstacleCount: totalObstacleCount,
                ConsideredObstacleCount: consideredObstacleCount,
                SectorCount: parameters.SectorCount,
                AheadDistanceM: parameters.AheadDistanceM,
                HalfFovDeg: parameters.HalfFovDeg,
                HasPassableCorridor: hasPassableCorridor,
                CorridorCenterOffsetDeg: corridor.CenterOffsetDeg,
                CorridorWidthMeters: corridor.WidthMeters,
                CorridorClearanceMeters: corridor.ClearanceMeters,
                CorridorConfidence: corridor.Confidence
            );
        }
    }
}