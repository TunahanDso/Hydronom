using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// GeliÅŸmiÅŸ Ã§evre analizi.
    ///
    /// Bu modÃ¼l IAnalysisModule arayÃ¼zÃ¼nÃ¼ bozmadan Ã§alÄ±ÅŸÄ±r:
    /// dÄ±ÅŸarÄ±ya yine Insights dÃ¶ndÃ¼rÃ¼r; iÃ§eride ise daha zengin risk, aÃ§Ä±klÄ±k,
    /// sektÃ¶r ve koridor analizi Ã¼retir.
    ///
    /// Ã–zellikler:
    /// - Ã–n tehlike tespiti
    /// - Sol / saÄŸ aÃ§Ä±klÄ±k analizi
    /// - AÃ§Ä±sal sektÃ¶r tabanlÄ± risk haritasÄ±
    /// - Koridor mantÄ±ÄŸÄ± ile gÃ¼venli yÃ¶n sezgisi
    /// - GÃ¼rÃ¼ltÃ¼ye dayanÄ±klÄ± sektÃ¶r smoothing
    /// - NaN / Infinity / bozuk obstacle korumasÄ±
    /// - Thread-safe canlÄ± parametre gÃ¼ncelleme
    /// - Son analiz iÃ§in aÃ§Ä±klanabilir AdvancedAnalysisReport Ã¼retimi
    /// </summary>
    public sealed class AdvancedAnalysis : IAnalysisModule
    {
        private readonly object _lock = new();

        private double _aheadDistanceM;
        private double _halfFovDeg;

        private int _sectorCount;
        private double _safetyMarginM;
        private double _dangerDistanceM;
        private double _sideWindowDeg;
        private double _frontWeight;
        private double _sizeWeight;
        private double _centerBiasWeight;
        private double _frontCriticalRiskThreshold;

        private AdvancedAnalysisReport _lastReport = AdvancedAnalysisReport.Empty;

        public double AheadDistanceM { get { lock (_lock) return _aheadDistanceM; } }
        public double HalfFovDeg { get { lock (_lock) return _halfFovDeg; } }
        public int SectorCount { get { lock (_lock) return _sectorCount; } }

        public double LastFrontRiskScore { get { lock (_lock) return _lastReport.FrontRiskScore; } }
        public double LastLeftScore { get { lock (_lock) return _lastReport.LeftScore; } }
        public double LastRightScore { get { lock (_lock) return _lastReport.RightScore; } }
        public double LastBestHeadingOffsetDeg { get { lock (_lock) return _lastReport.BestHeadingOffsetDeg; } }
        public string LastSuggestedSide { get { lock (_lock) return _lastReport.SuggestedSide; } }

        /// <summary>
        /// Son analizin aÃ§Ä±klanabilir raporu.
        /// Diagnostics, Ops veya log tarafÄ± buradan daha zengin analiz okuyabilir.
        /// </summary>
        public AdvancedAnalysisReport LastReport
        {
            get { lock (_lock) return _lastReport; }
        }

        public AdvancedAnalysis(
            double aheadDistanceM = 12.0,
            double halfFovDeg = 60.0,
            int sectorCount = 31,
            double safetyMarginM = 0.80,
            double dangerDistanceM = 4.0,
            double sideWindowDeg = 70.0,
            double frontWeight = 1.35,
            double sizeWeight = 0.90,
            double centerBiasWeight = 0.10,
            double frontCriticalRiskThreshold = 1.15)
        {
            _aheadDistanceM = ClampAhead(aheadDistanceM);
            _halfFovDeg = ClampFov(halfFovDeg);
            _sectorCount = ClampSectorCount(sectorCount);
            _safetyMarginM = ClampRange(safetyMarginM, 0.0, 10.0, 0.80);
            _dangerDistanceM = ClampRange(dangerDistanceM, 0.5, 100.0, 4.0);
            _sideWindowDeg = ClampRange(sideWindowDeg, 10.0, 120.0, 70.0);
            _frontWeight = ClampRange(frontWeight, 0.1, 5.0, 1.35);
            _sizeWeight = ClampRange(sizeWeight, 0.0, 5.0, 0.90);
            _centerBiasWeight = ClampRange(centerBiasWeight, 0.0, 1.0, 0.10);
            _frontCriticalRiskThreshold = ClampRange(frontCriticalRiskThreshold, 0.1, 10.0, 1.15);
        }

        /// <summary>
        /// CanlÄ± parametre gÃ¼ncelleme.
        /// Null olmayan alanlar gÃ¼ncellenir.
        /// </summary>
        public void SetParameters(
            double? aheadDistanceM = null,
            double? halfFovDeg = null,
            int? sectorCount = null,
            double? safetyMarginM = null,
            double? dangerDistanceM = null,
            double? sideWindowDeg = null,
            double? frontWeight = null,
            double? sizeWeight = null,
            double? centerBiasWeight = null,
            double? frontCriticalRiskThreshold = null)
        {
            lock (_lock)
            {
                if (aheadDistanceM.HasValue) _aheadDistanceM = ClampAhead(aheadDistanceM.Value);
                if (halfFovDeg.HasValue) _halfFovDeg = ClampFov(halfFovDeg.Value);
                if (sectorCount.HasValue) _sectorCount = ClampSectorCount(sectorCount.Value);
                if (safetyMarginM.HasValue) _safetyMarginM = ClampRange(safetyMarginM.Value, 0.0, 10.0, _safetyMarginM);
                if (dangerDistanceM.HasValue) _dangerDistanceM = ClampRange(dangerDistanceM.Value, 0.5, 100.0, _dangerDistanceM);
                if (sideWindowDeg.HasValue) _sideWindowDeg = ClampRange(sideWindowDeg.Value, 10.0, 120.0, _sideWindowDeg);
                if (frontWeight.HasValue) _frontWeight = ClampRange(frontWeight.Value, 0.1, 5.0, _frontWeight);
                if (sizeWeight.HasValue) _sizeWeight = ClampRange(sizeWeight.Value, 0.0, 5.0, _sizeWeight);
                if (centerBiasWeight.HasValue) _centerBiasWeight = ClampRange(centerBiasWeight.Value, 0.0, 1.0, _centerBiasWeight);
                if (frontCriticalRiskThreshold.HasValue) _frontCriticalRiskThreshold = ClampRange(frontCriticalRiskThreshold.Value, 0.1, 10.0, _frontCriticalRiskThreshold);
            }
        }

        public void Update(double? aheadDistanceM = null, double? halfFovDeg = null)
            => SetParameters(aheadDistanceM: aheadDistanceM, halfFovDeg: halfFovDeg);

        public Insights Analyze(FusedFrame frame)
        {
            double aheadDistanceM;
            double halfFovDeg;
            int sectorCount;
            double safetyMarginM;
            double dangerDistanceM;
            double sideWindowDeg;
            double frontWeight;
            double sizeWeight;
            double centerBiasWeight;
            double frontCriticalRiskThreshold;

            lock (_lock)
            {
                aheadDistanceM = _aheadDistanceM;
                halfFovDeg = _halfFovDeg;
                sectorCount = _sectorCount;
                safetyMarginM = _safetyMarginM;
                dangerDistanceM = _dangerDistanceM;
                sideWindowDeg = _sideWindowDeg;
                frontWeight = _frontWeight;
                sizeWeight = _sizeWeight;
                centerBiasWeight = _centerBiasWeight;
                frontCriticalRiskThreshold = _frontCriticalRiskThreshold;
            }

            sectorCount = ClampSectorCount(sectorCount);
            if (sectorCount % 2 == 0)
                sectorCount++;

            double headingDeg = Sanitize(frame.HeadingDeg);
            double headingRad = DegToRad(headingDeg);

            Vec2 ownPosition = frame.Position;

            double[] sectorRisk = new double[sectorCount];
            double[] sectorClearance = new double[sectorCount];

            for (int i = 0; i < sectorCount; i++)
                sectorClearance[i] = aheadDistanceM;

            bool hasObstacleAhead = false;
            double leftClear = aheadDistanceM;
            double rightClear = aheadDistanceM;

            int totalObstacleCount = 0;
            int consideredObstacleCount = 0;
            double closestSurfaceDistance = aheadDistanceM;

            if (frame.Obstacles is not null)
            {
                foreach (var obstacle in frame.Obstacles)
                {
                    totalObstacleCount++;

                    if (!TryBuildObstacleSample(
                            ownPosition,
                            headingRad,
                            obstacle,
                            safetyMarginM,
                            out var sample))
                    {
                        continue;
                    }

                    consideredObstacleCount++;
                    closestSurfaceDistance = Math.Min(closestSurfaceDistance, sample.SurfaceDistanceM);

                    bool inFrontCone = Math.Abs(sample.RelativeAngleDeg) <= halfFovDeg;
                    bool inRange = sample.CenterDistanceM <= aheadDistanceM + sample.RadiusM + safetyMarginM;

                    if (inFrontCone && inRange)
                        hasObstacleAhead = true;

                    // Ä°ÅŸaret konvansiyonu mevcut kod ile korunuyor:
                    // relAngleDeg < 0 -> left, relAngleDeg >= 0 -> right.
                    if (sample.RelativeAngleDeg < 0.0 && Math.Abs(sample.RelativeAngleDeg) <= sideWindowDeg)
                        leftClear = Math.Min(leftClear, sample.SurfaceDistanceM);

                    if (sample.RelativeAngleDeg >= 0.0 && Math.Abs(sample.RelativeAngleDeg) <= sideWindowDeg)
                        rightClear = Math.Min(rightClear, sample.SurfaceDistanceM);

                    AccumulateSectorRisk(
                        sample,
                        sectorRisk,
                        sectorClearance,
                        sectorCount,
                        aheadDistanceM,
                        halfFovDeg,
                        frontWeight,
                        sizeWeight);
                }
            }

            double[] sectorScore = BuildSectorScores(sectorRisk, sectorClearance, aheadDistanceM);
            SmoothInPlace(sectorScore, passes: 2);

            int centerIndex = sectorCount / 2;
            int bestIndex = centerIndex;
            double bestScore = double.MinValue;

            for (int i = 0; i < sectorCount; i++)
            {
                double sectorAngleDeg = SectorAngleDeg(i, sectorCount, halfFovDeg);
                double centerBiasPenalty = Math.Abs(sectorAngleDeg) / Math.Max(1e-6, halfFovDeg) * centerBiasWeight;

                double finalScore = sectorScore[i] - centerBiasPenalty;

                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestIndex = i;
                }
            }

            double bestHeadingOffsetDeg = SectorAngleDeg(bestIndex, sectorCount, halfFovDeg);

            double frontRiskScore = ComputeFrontRiskScore(sectorRisk, sectorCount);
            (double leftScore, double rightScore) = ComputeSideScores(sectorScore, sectorCount, halfFovDeg);

            string suggestedSide = "center";
            if (bestHeadingOffsetDeg < -8.0) suggestedSide = "left";
            else if (bestHeadingOffsetDeg > 8.0) suggestedSide = "right";

            if (Math.Min(leftClear, rightClear) < dangerDistanceM ||
                closestSurfaceDistance < dangerDistanceM ||
                frontRiskScore > frontCriticalRiskThreshold)
            {
                hasObstacleAhead = true;
            }

            var report = new AdvancedAnalysisReport(
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
                SectorCount: sectorCount,
                AheadDistanceM: aheadDistanceM,
                HalfFovDeg: halfFovDeg
            );

            lock (_lock)
                _lastReport = report;

            return new Insights(
                HasObstacleAhead: report.HasObstacleAhead,
                ClearanceLeft: report.ClearanceLeft,
                ClearanceRight: report.ClearanceRight
            );
        }

        private static bool TryBuildObstacleSample(
            Vec2 ownPosition,
            double headingRad,
            Obstacle obstacle,
            double safetyMarginM,
            out ObstacleSample sample)
        {
            sample = default;

            double ox = Sanitize(obstacle.Position.X);
            double oy = Sanitize(obstacle.Position.Y);
            double radius = Math.Max(0.0, Sanitize(obstacle.RadiusM));

            double dx = ox - Sanitize(ownPosition.X);
            double dy = oy - Sanitize(ownPosition.Y);

            double centerDist = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(centerDist))
                return false;

            if (centerDist < 1e-6)
                centerDist = 1e-6;

            double surfaceDist = Math.Max(0.0, centerDist - radius - safetyMarginM);

            double absAngle = Math.Atan2(dy, dx);
            double relAngleDeg = NormalizeDeg(RadToDeg(absAngle - headingRad));

            double angularRadiusDeg;
            if (centerDist > radius)
            {
                double ratio = Math.Clamp((radius + safetyMarginM) / centerDist, 0.0, 1.0);
                angularRadiusDeg = RadToDeg(Math.Asin(ratio));
            }
            else
            {
                angularRadiusDeg = 90.0;
            }

            if (!double.IsFinite(angularRadiusDeg))
                angularRadiusDeg = 90.0;

            sample = new ObstacleSample(
                CenterDistanceM: centerDist,
                SurfaceDistanceM: surfaceDist,
                RelativeAngleDeg: relAngleDeg,
                AngularRadiusDeg: angularRadiusDeg,
                RadiusM: radius
            );

            return true;
        }

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

        private static double ClampAhead(double value) => ClampRange(value, 1.0, 1000.0, 12.0);

        private static double ClampFov(double value) => ClampRange(value, 5.0, 120.0, 60.0);

        private static int ClampSectorCount(int value)
        {
            int clamped = Math.Clamp(value, 9, 121);

            if (clamped % 2 == 0)
                clamped++;

            if (clamped > 121)
                clamped = 121;

            return clamped;
        }

        private static double ClampRange(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

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

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private readonly record struct ObstacleSample(
            double CenterDistanceM,
            double SurfaceDistanceM,
            double RelativeAngleDeg,
            double AngularRadiusDeg,
            double RadiusM
        );
    }

    /// <summary>
    /// AdvancedAnalysis son analiz raporu.
    ///
    /// IAnalysisModule dÄ±ÅŸarÄ±ya Insights dÃ¶ndÃ¼rmeye devam eder.
    /// Bu rapor ise debug, telemetry, Hydronom Ops ve Diagnostics iÃ§in daha zengin iÃ§erik saÄŸlar.
    /// </summary>
    public readonly record struct AdvancedAnalysisReport(
        bool HasObstacleAhead,
        double ClearanceLeft,
        double ClearanceRight,
        double FrontRiskScore,
        double LeftScore,
        double RightScore,
        double BestHeadingOffsetDeg,
        string SuggestedSide,
        double ClosestSurfaceDistanceM,
        int TotalObstacleCount,
        int ConsideredObstacleCount,
        int SectorCount,
        double AheadDistanceM,
        double HalfFovDeg
    )
    {
        public static AdvancedAnalysisReport Empty { get; } =
            new(
                HasObstacleAhead: false,
                ClearanceLeft: 0.0,
                ClearanceRight: 0.0,
                FrontRiskScore: 0.0,
                LeftScore: 0.0,
                RightScore: 0.0,
                BestHeadingOffsetDeg: 0.0,
                SuggestedSide: "center",
                ClosestSurfaceDistanceM: 0.0,
                TotalObstacleCount: 0,
                ConsideredObstacleCount: 0,
                SectorCount: 0,
                AheadDistanceM: 0.0,
                HalfFovDeg: 0.0
            );

        public override string ToString()
        {
            return
                $"AdvancedAnalysis danger={HasObstacleAhead} " +
                $"clear(L/R)=({ClearanceLeft:F2}/{ClearanceRight:F2}) " +
                $"riskFront={FrontRiskScore:F3} " +
                $"score(L/R)=({LeftScore:F3}/{RightScore:F3}) " +
                $"bestOffset={BestHeadingOffsetDeg:F1}Â° " +
                $"side={SuggestedSide} " +
                $"obs={ConsideredObstacleCount}/{TotalObstacleCount}";
        }
    }
}
