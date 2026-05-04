using System;

namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        private static void AccumulateSectorRisk(
            ObstacleSample sample,
            double[] sectorRisk,
            double[] sectorClearance,
            int sectorCount,
            double aheadDistanceM,
            double halfFovDeg,
            double frontWeight,
            double sizeWeight)
        {
            bool obstacleAffectsFov =
                Math.Abs(sample.RelativeAngleDeg) <= halfFovDeg + sample.AngularRadiusDeg;

            bool obstacleInRange =
                sample.CenterDistanceM <= aheadDistanceM + sample.RadiusM;

            if (!obstacleAffectsFov || !obstacleInRange)
                return;

            for (int i = 0; i < sectorCount; i++)
            {
                double sectorAngleDeg = SectorAngleDeg(i, sectorCount, halfFovDeg);
                double angleDiff = Math.Abs(NormalizeDeg(sample.RelativeAngleDeg - sectorAngleDeg));

                if (angleDiff > sample.AngularRadiusDeg)
                    continue;

                double distanceRisk = 1.0 - Math.Clamp(sample.SurfaceDistanceM / aheadDistanceM, 0.0, 1.0);

                double frontFactor = 1.0 - Math.Min(Math.Abs(sectorAngleDeg) / Math.Max(1e-6, halfFovDeg), 1.0);
                frontFactor = 1.0 + frontFactor * (frontWeight - 1.0);

                double sizeFactor =
                    1.0 +
                    Math.Min(sample.RadiusM / Math.Max(0.1, aheadDistanceM), 1.0) * sizeWeight;

                double angularFactor =
                    1.0 -
                    Math.Clamp(angleDiff / Math.Max(1e-6, sample.AngularRadiusDeg), 0.0, 1.0);

                double riskContribution =
                    distanceRisk *
                    frontFactor *
                    sizeFactor *
                    (0.35 + 0.65 * angularFactor);

                if (!double.IsFinite(riskContribution))
                    riskContribution = 0.0;

                sectorRisk[i] += riskContribution;
                sectorClearance[i] = Math.Min(sectorClearance[i], sample.SurfaceDistanceM);
            }
        }

        private static double[] BuildSectorScores(
            double[] sectorRisk,
            double[] sectorClearance,
            double aheadDistanceM)
        {
            double[] sectorScore = new double[sectorRisk.Length];

            for (int i = 0; i < sectorScore.Length; i++)
            {
                double clearanceNorm = Math.Clamp(sectorClearance[i] / aheadDistanceM, 0.0, 1.0);
                double riskNorm = 1.0 / (1.0 + Math.Max(0.0, sectorRisk[i]));

                sectorScore[i] = 0.65 * clearanceNorm + 0.35 * riskNorm;

                if (!double.IsFinite(sectorScore[i]))
                    sectorScore[i] = 0.0;
            }

            return sectorScore;
        }

        private static double ComputeFrontRiskScore(double[] sectorRisk, int sectorCount)
        {
            int centerIndex = sectorCount / 2;
            int frontWindow = Math.Max(1, sectorCount / 6);

            double frontRiskScore = 0.0;
            int used = 0;

            for (int i = centerIndex - frontWindow; i <= centerIndex + frontWindow; i++)
            {
                if (i < 0 || i >= sectorCount)
                    continue;

                frontRiskScore += sectorRisk[i];
                used++;
            }

            return used > 0 ? frontRiskScore / used : 0.0;
        }

        private static (double leftScore, double rightScore) ComputeSideScores(
            double[] sectorScore,
            int sectorCount,
            double halfFovDeg)
        {
            double leftScore = 0.0;
            double rightScore = 0.0;
            int leftCount = 0;
            int rightCount = 0;

            for (int i = 0; i < sectorCount; i++)
            {
                double sectorAngleDeg = SectorAngleDeg(i, sectorCount, halfFovDeg);

                if (sectorAngleDeg < -5.0)
                {
                    leftScore += sectorScore[i];
                    leftCount++;
                }
                else if (sectorAngleDeg > 5.0)
                {
                    rightScore += sectorScore[i];
                    rightCount++;
                }
            }

            if (leftCount > 0) leftScore /= leftCount;
            if (rightCount > 0) rightScore /= rightCount;

            return (leftScore, rightScore);
        }

        private static void SmoothInPlace(double[] values, int passes)
        {
            if (values.Length < 3 || passes <= 0)
                return;

            var temp = new double[values.Length];

            for (int p = 0; p < passes; p++)
            {
                temp[0] = values[0] * 0.7 + values[1] * 0.3;

                for (int i = 1; i < values.Length - 1; i++)
                    temp[i] = values[i - 1] * 0.25 + values[i] * 0.5 + values[i + 1] * 0.25;

                temp[^1] = values[^2] * 0.3 + values[^1] * 0.7;

                Array.Copy(temp, values, values.Length);
            }
        }

        private static double SectorAngleDeg(int index, int sectorCount, double halfFovDeg)
        {
            if (sectorCount <= 1)
                return 0.0;

            return Lerp(-halfFovDeg, halfFovDeg, index / (double)(sectorCount - 1));
        }
    }
}