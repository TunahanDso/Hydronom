using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Gelişmiş çevre analizi
    /// 
    /// Özellikler:
    /// - Ön tehlike tespiti (bool)
    /// - Sol / sağ açıklık analizi
    /// - Açısal sektör tabanlı risk haritası
    /// - Koridor mantığı ile daha güvenli yön sezgisi
    /// - Canlı parametre güncelleme (thread-safe)
    /// 
    /// Not:
    /// Bu sürüm mevcut IAnalysisModule arayüzünü bozmadan daha akıllı hesap yapar.
    /// İçeride daha zengin metrikler üretir; dışarıya yine Insights verir.
    /// </summary>
    public class AdvancedAnalysis : IAnalysisModule
    {
        private readonly object _lock = new();

        // Temel parametreler
        private double _aheadDistanceM;
        private double _halfFovDeg;

        // Gelişmiş analiz parametreleri
        private int _sectorCount;
        private double _safetyMarginM;
        private double _dangerDistanceM;
        private double _sideWindowDeg;
        private double _frontWeight;
        private double _sizeWeight;

        // Son analiz çıktıları (debug / telemetry için faydalı)
        public double AheadDistanceM { get { lock (_lock) return _aheadDistanceM; } }
        public double HalfFovDeg { get { lock (_lock) return _halfFovDeg; } }
        public int SectorCount { get { lock (_lock) return _sectorCount; } }

        public double LastFrontRiskScore { get; private set; }
        public double LastLeftScore { get; private set; }
        public double LastRightScore { get; private set; }
        public double LastBestHeadingOffsetDeg { get; private set; }
        public string LastSuggestedSide { get; private set; } = "center";

        public AdvancedAnalysis(
            double aheadDistanceM = 12.0,
            double halfFovDeg = 60.0,
            int sectorCount = 31,
            double safetyMarginM = 0.80,
            double dangerDistanceM = 4.0,
            double sideWindowDeg = 70.0,
            double frontWeight = 1.35,
            double sizeWeight = 0.90)
        {
            _aheadDistanceM = ClampAhead(aheadDistanceM);
            _halfFovDeg = ClampFov(halfFovDeg);
            _sectorCount = ClampSectorCount(sectorCount);
            _safetyMarginM = Math.Clamp(safetyMarginM, 0.0, 10.0);
            _dangerDistanceM = Math.Clamp(dangerDistanceM, 0.5, 100.0);
            _sideWindowDeg = Math.Clamp(sideWindowDeg, 10.0, 120.0);
            _frontWeight = Math.Clamp(frontWeight, 0.1, 5.0);
            _sizeWeight = Math.Clamp(sizeWeight, 0.0, 5.0);
        }

        /// <summary>
        /// Canlı parametre güncelleme.
        /// Null olmayan alanlar güncellenir.
        /// </summary>
        public void SetParameters(
            double? aheadDistanceM = null,
            double? halfFovDeg = null,
            int? sectorCount = null,
            double? safetyMarginM = null,
            double? dangerDistanceM = null,
            double? sideWindowDeg = null,
            double? frontWeight = null,
            double? sizeWeight = null)
        {
            lock (_lock)
            {
                if (aheadDistanceM.HasValue) _aheadDistanceM = ClampAhead(aheadDistanceM.Value);
                if (halfFovDeg.HasValue) _halfFovDeg = ClampFov(halfFovDeg.Value);
                if (sectorCount.HasValue) _sectorCount = ClampSectorCount(sectorCount.Value);
                if (safetyMarginM.HasValue) _safetyMarginM = Math.Clamp(safetyMarginM.Value, 0.0, 10.0);
                if (dangerDistanceM.HasValue) _dangerDistanceM = Math.Clamp(dangerDistanceM.Value, 0.5, 100.0);
                if (sideWindowDeg.HasValue) _sideWindowDeg = Math.Clamp(sideWindowDeg.Value, 10.0, 120.0);
                if (frontWeight.HasValue) _frontWeight = Math.Clamp(frontWeight.Value, 0.1, 5.0);
                if (sizeWeight.HasValue) _sizeWeight = Math.Clamp(sizeWeight.Value, 0.0, 5.0);
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
            }

            if (sectorCount % 2 == 0)
                sectorCount++;

            double headingRad = DegToRad(frame.HeadingDeg);

            // Sektörler: -halfFov ... +halfFov
            double[] sectorRisk = new double[sectorCount];
            double[] sectorClearance = new double[sectorCount];

            for (int i = 0; i < sectorCount; i++)
                sectorClearance[i] = aheadDistanceM;

            bool hasObstacleAhead = false;
            double leftClear = aheadDistanceM;
            double rightClear = aheadDistanceM;

            foreach (var o in frame.Obstacles)
            {
                double dx = o.Position.X - frame.Position.X;
                double dy = o.Position.Y - frame.Position.Y;

                double centerDist = Math.Sqrt(dx * dx + dy * dy);
                if (centerDist < 1e-6)
                    centerDist = 1e-6;

                double surfaceDist = Math.Max(0.0, centerDist - o.RadiusM - safetyMarginM);

                // Dünya koordinatındaki hedef açıyı teknenin heading’ine göre relatif açıya çevir
                double absAngle = Math.Atan2(dy, dx);
                double relAngleDeg = NormalizeDeg(RadToDeg(absAngle - headingRad));

                // Obstacle’ın açısal kapsama alanı
                double angularRadiusDeg = 0.0;
                if (centerDist > o.RadiusM)
                    angularRadiusDeg = RadToDeg(Math.Asin(Math.Clamp((o.RadiusM + safetyMarginM) / centerDist, 0.0, 1.0)));
                else
                    angularRadiusDeg = 90.0;

                // Ön bölgede mi?
                bool inFrontCone = Math.Abs(relAngleDeg) <= halfFovDeg;
                bool inRange = centerDist <= aheadDistanceM + o.RadiusM + safetyMarginM;

                if (inFrontCone && inRange)
                    hasObstacleAhead = true;

                // Sol / sağ clearance için yalnız tek nokta değil, yan pencere içinde bak
                if (relAngleDeg < 0 && Math.Abs(relAngleDeg) <= sideWindowDeg)
                    leftClear = Math.Min(leftClear, surfaceDist);

                if (relAngleDeg >= 0 && Math.Abs(relAngleDeg) <= sideWindowDeg)
                    rightClear = Math.Min(rightClear, surfaceDist);

                // Sektör bazlı risk işleme
                for (int i = 0; i < sectorCount; i++)
                {
                    double sectorAngleDeg = Lerp(-halfFovDeg, halfFovDeg, i / (double)(sectorCount - 1));
                    double angleDiff = Math.Abs(NormalizeDeg(relAngleDeg - sectorAngleDeg));

                    if (angleDiff > angularRadiusDeg)
                        continue;

                    // Mesafe riski: yaklaştıkça artsın
                    double distanceRisk = 1.0 - Math.Clamp(surfaceDist / aheadDistanceM, 0.0, 1.0);

                    // Ön taraftaki sektörler daha önemli
                    double frontFactor = 1.0 - Math.Min(Math.Abs(sectorAngleDeg) / halfFovDeg, 1.0);
                    frontFactor = 1.0 + frontFactor * (frontWeight - 1.0);

                    // Büyük obstacle biraz daha tehlikeli sayılsın
                    double sizeFactor = 1.0 + Math.Min(o.RadiusM / Math.Max(0.1, aheadDistanceM), 1.0) * sizeWeight;

                    // Sektör merkezine yakınsa katkısı daha fazla olsun
                    double angularFactor = 1.0 - Math.Clamp(angleDiff / Math.Max(1e-6, angularRadiusDeg), 0.0, 1.0);

                    double riskContribution = distanceRisk * frontFactor * sizeFactor * (0.35 + 0.65 * angularFactor);

                    sectorRisk[i] += riskContribution;
                    sectorClearance[i] = Math.Min(sectorClearance[i], surfaceDist);
                }
            }

            // Sektör skorları: düşük risk + yüksek clearance = iyi yön
            double[] sectorScore = new double[sectorCount];
            for (int i = 0; i < sectorCount; i++)
            {
                double clearanceNorm = Math.Clamp(sectorClearance[i] / aheadDistanceM, 0.0, 1.0);
                double riskNorm = 1.0 / (1.0 + sectorRisk[i]);
                sectorScore[i] = 0.65 * clearanceNorm + 0.35 * riskNorm;
            }

            // Gürültü bastırmak için hafif smoothing
            SmoothInPlace(sectorScore, passes: 2);

            int centerIndex = sectorCount / 2;
            int bestIndex = centerIndex;
            double bestScore = double.MinValue;

            for (int i = 0; i < sectorCount; i++)
            {
                // Çok sert zig-zag olmasın diye merkeze hafif sadakat
                double sectorAngleDeg = Lerp(-halfFovDeg, halfFovDeg, i / (double)(sectorCount - 1));
                double centerBiasPenalty = Math.Abs(sectorAngleDeg) / halfFovDeg * 0.10;

                double finalScore = sectorScore[i] - centerBiasPenalty;
                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestIndex = i;
                }
            }

            double bestHeadingOffsetDeg = Lerp(-halfFovDeg, halfFovDeg, bestIndex / (double)(sectorCount - 1));

            // Ön risk skoru: merkeze yakın sektörler daha kritik
            double frontRiskScore = 0.0;
            int frontWindow = Math.Max(1, sectorCount / 6);
            for (int i = centerIndex - frontWindow; i <= centerIndex + frontWindow; i++)
            {
                if (i < 0 || i >= sectorCount) continue;
                frontRiskScore += sectorRisk[i];
            }
            frontRiskScore /= (2 * frontWindow + 1);

            // Sol / sağ bölgesel skorlar
            double leftScore = 0.0;
            double rightScore = 0.0;
            int leftCount = 0;
            int rightCount = 0;

            for (int i = 0; i < sectorCount; i++)
            {
                double sectorAngleDeg = Lerp(-halfFovDeg, halfFovDeg, i / (double)(sectorCount - 1));

                if (sectorAngleDeg < -5)
                {
                    leftScore += sectorScore[i];
                    leftCount++;
                }
                else if (sectorAngleDeg > 5)
                {
                    rightScore += sectorScore[i];
                    rightCount++;
                }
            }

            if (leftCount > 0) leftScore /= leftCount;
            if (rightCount > 0) rightScore /= rightCount;

            string suggestedSide = "center";
            if (bestHeadingOffsetDeg < -8) suggestedSide = "left";
            else if (bestHeadingOffsetDeg > 8) suggestedSide = "right";

            // Çok yakın kritik engel varsa daha saldırgan uyarı
            if (Math.Min(leftClear, rightClear) < dangerDistanceM || frontRiskScore > 1.15)
                hasObstacleAhead = true;

            LastFrontRiskScore = frontRiskScore;
            LastLeftScore = leftScore;
            LastRightScore = rightScore;
            LastBestHeadingOffsetDeg = bestHeadingOffsetDeg;
            LastSuggestedSide = suggestedSide;

            return new Insights(
                HasObstacleAhead: hasObstacleAhead,
                ClearanceLeft: leftClear,
                ClearanceRight: rightClear
            );
        }

        private static void SmoothInPlace(double[] values, int passes)
        {
            if (values.Length < 3 || passes <= 0)
                return;

            var temp = new double[values.Length];

            for (int p = 0; p < passes; p++)
            {
                temp[0] = (values[0] * 0.7) + (values[1] * 0.3);
                for (int i = 1; i < values.Length - 1; i++)
                    temp[i] = values[i - 1] * 0.25 + values[i] * 0.5 + values[i + 1] * 0.25;
                temp[^1] = (values[^2] * 0.3) + (values[^1] * 0.7);

                Array.Copy(temp, values, values.Length);
            }
        }

        private static double ClampAhead(double v) => Math.Clamp(v, 1.0, 1000.0);
        private static double ClampFov(double v) => Math.Clamp(v, 5.0, 120.0);
        private static int ClampSectorCount(int v) => Math.Clamp(v, 9, 121);

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;
        private static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

        private static double NormalizeDeg(double deg)
        {
            while (deg > 180.0) deg -= 360.0;
            while (deg < -180.0) deg += 360.0;
            return deg;
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}