using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedAnalysisReport içinden operasyonel risk ve decision advice üretir.
    ///
    /// Bu sınıf Analysis → Decision köprüsüdür.
    /// Obstacle/clearance/sector analizinden karar önerisi çıkarır.
    ///
    /// Yeni prensip:
    /// Güvenlik katmanı aracı öldüren bir duvar değildir.
    /// Öncelik sırası:
    ///
    /// 1) Geçilebilir koridor varsa kontrollü geç.
    /// 2) Yakın engel varsa recovery/avoidance ile canlı kal.
    /// 3) Sadece temas/kaçınılmaz çarpışma gibi son çare durumunda dur.
    /// </summary>
    public static class OperationalRiskAnalyzer
    {
        private const double PassableCorridorMinConfidence = 0.20;

        private const double ContactDistanceM = 0.18;
        private const double CriticalCloseDistanceM = 0.75;
        private const double CloseDistanceM = 1.25;

        public static OperationalAnalysisContext Analyze(
            AdvancedAnalysisReport report)
        {
            double obstacleRisk = ComputeObstacleRisk(report);
            var advice = BuildObstacleAdvice(report, obstacleRisk);

            return OperationalAnalysisContext.FromObstacleReport(
                report,
                advice,
                obstacleRisk);
        }

        private static DecisionAdviceProfile BuildObstacleAdvice(
            AdvancedAnalysisReport report,
            double obstacleRisk)
        {
            /*
             * 1) KORİDOR VARSA PANİK YOK.
             *
             * Engel önde olabilir, duba yakın olabilir, risk yükselmiş olabilir.
             * Ama analiz modülü "geçilebilir koridor var" diyorsa bu bir durma sebebi değil,
             * kontrollü geçiş sebebidir.
             */
            if (report.HasPassableCorridor &&
                report.CorridorConfidence >= PassableCorridorMinConfidence)
            {
                return BuildPassableCorridorAdvice(report, obstacleRisk);
            }

            /*
             * 2) GERÇEK TEMAS / SON ÇARE.
             *
             * Burayı özellikle çok düşük tuttuk.
             * Çünkü "yakın engel" ile "durmak zorundayım" aynı şey değildir.
             * Bu profil sadece gerçekten objeye bindik gibi aşırı durumda devreye girer.
             */
            if (IsLastResortContact(report))
            {
                return new DecisionAdviceProfile(
                    MaxSpeedScale: 0.10,
                    ThrottleScale: 0.08,
                    YawAggressionScale: 1.85,
                    ArrivalCautionScale: 2.75,
                    ObstacleAvoidanceUrgency: 1.0,
                    HoldPreference: 0.95,
                    ForceCoast: true,
                    PreferSafeHeading: true,
                    RequireSlowMode: true,
                    RecommendHold: true,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "IMMINENT_CONTACT_LAST_RESORT",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: Math.Max(0.0, report.ClosestSurfaceDistanceM),
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                ).Sanitized();
            }

            /*
             * 3) YAKIN ENGEL AMA KORİDOR YOK.
             *
             * Eski sistem burada ForceCoast + Hold yapıyordu.
             * Yeni sistem "dur" demiyor; recovery/avoidance davranışını canlı tutuyor.
             * Decision katmanı yaw/heading tarafında kaçınma üretebilsin diye
             * gaz ve hareket tamamen öldürülmez.
             */
            if (IsCriticalClose(report))
            {
                double closeness = ComputeCloseness01(
                    report.ClosestSurfaceDistanceM,
                    CriticalCloseDistanceM);

                double urgency = Math.Clamp(
                    Math.Max(obstacleRisk, 0.70 + closeness * 0.20),
                    0.70,
                    0.92);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(0.48 - urgency * 0.12, 0.34, 0.48),
                    ThrottleScale: Math.Clamp(0.46 - urgency * 0.10, 0.32, 0.46),
                    YawAggressionScale: Math.Clamp(1.25 + urgency * 0.55, 1.45, 1.85),
                    ArrivalCautionScale: Math.Clamp(1.45 + urgency * 0.45, 1.60, 2.10),
                    ObstacleAvoidanceUrgency: urgency,
                    HoldPreference: Math.Clamp(0.20 + urgency * 0.25, 0.30, 0.50),
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: true,
                    RecommendHold: false,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "CRITICAL_CLOSE_OBSTACLE_RECOVERY",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: Math.Max(0.0, report.ClosestSurfaceDistanceM),
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                ).Sanitized();
            }

            /*
             * 4) ÖNDE ENGEL VAR.
             *
             * Bu artık panik/ölüm modu değil.
             * Araç hızını düşürür, yaw kabiliyetini artırır, güvenli heading ister,
             * fakat throttle'ı tamamen öldürmez.
             */
            if (report.HasObstacleAhead || report.FrontRiskScore >= 1.15)
            {
                double urgency = Math.Clamp(
                    obstacleRisk + 0.20,
                    0.30,
                    0.78);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(1.0 - urgency * 0.42, 0.48, 0.88),
                    ThrottleScale: Math.Clamp(1.0 - urgency * 0.46, 0.42, 0.90),
                    YawAggressionScale: Math.Clamp(1.0 + urgency * 0.55, 1.05, 1.55),
                    ArrivalCautionScale: Math.Clamp(1.0 + urgency * 0.70, 1.05, 1.80),
                    ObstacleAvoidanceUrgency: urgency,
                    HoldPreference: Math.Clamp(urgency * 0.22, 0.0, 0.35),
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: urgency >= 0.55,
                    RecommendHold: false,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "OBSTACLE_AHEAD_RECOVERY",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: Math.Max(0.0, report.ClosestSurfaceDistanceM),
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                ).Sanitized();
            }

            /*
             * 5) RİSK YÜKSELMİŞ AMA PANİK YOK.
             */
            if (obstacleRisk >= 0.30)
            {
                double caution = Math.Clamp(
                    obstacleRisk,
                    0.30,
                    0.75);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(1.0 - caution * 0.25, 0.68, 0.94),
                    ThrottleScale: Math.Clamp(1.0 - caution * 0.28, 0.62, 0.96),
                    YawAggressionScale: Math.Clamp(1.0 + caution * 0.22, 1.0, 1.30),
                    ArrivalCautionScale: Math.Clamp(1.0 + caution * 0.45, 1.0, 1.55),
                    ObstacleAvoidanceUrgency: caution,
                    HoldPreference: 0.0,
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: false,
                    RecommendHold: false,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "ELEVATED_OBSTACLE_RISK_CAUTION",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: Math.Max(0.0, report.ClosestSurfaceDistanceM),
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                ).Sanitized();
            }

            return DecisionAdviceProfile.Neutral;
        }

        private static DecisionAdviceProfile BuildPassableCorridorAdvice(
            AdvancedAnalysisReport report,
            double obstacleRisk)
        {
            double confidence = Math.Clamp(report.CorridorConfidence, 0.0, 1.0);

            /*
             * Koridor varsa düşük güven bile tamamen yavaşlatma sebebi değil.
             * Düşük güven: dikkatli ama canlı.
             * Yüksek güven: atik ve koridor merkezli.
             */
            double speedScale = Math.Clamp(
                0.68 + confidence * 0.22 - obstacleRisk * 0.10,
                0.58,
                0.92);

            double throttleScale = Math.Clamp(
                0.62 + confidence * 0.26 - obstacleRisk * 0.08,
                0.52,
                0.92);

            double urgency = Math.Clamp(
                0.18 + obstacleRisk * 0.35 - confidence * 0.15,
                0.12,
                0.42);

            return new DecisionAdviceProfile(
                MaxSpeedScale: speedScale,
                ThrottleScale: throttleScale,
                YawAggressionScale: Math.Clamp(1.02 + confidence * 0.38, 1.05, 1.45),
                ArrivalCautionScale: Math.Clamp(1.05 + (1.0 - confidence) * 0.35, 1.05, 1.42),
                ObstacleAvoidanceUrgency: urgency,
                HoldPreference: 0.0,
                ForceCoast: false,
                PreferSafeHeading: true,
                RequireSlowMode: confidence < 0.35 && obstacleRisk >= 0.55,
                RecommendHold: false,
                RecommendReturnHome: false,
                RecommendMissionAbort: false,
                PrimaryReason: "PASSABLE_CORRIDOR_GUIDANCE",
                HasPassableCorridor: true,
                CorridorCenterOffsetDeg: report.CorridorCenterOffsetDeg,
                CorridorWidthMeters: report.CorridorWidthMeters,
                CorridorClearanceMeters: report.CorridorClearanceMeters,
                CorridorConfidence: report.CorridorConfidence,
                SuppressObstaclePanic: true,
                PreferCorridorHeading: true
            ).Sanitized();
        }

        private static double ComputeObstacleRisk(
            AdvancedAnalysisReport report)
        {
            double frontRisk = Math.Clamp(report.FrontRiskScore / 2.5, 0.0, 1.0);

            double closestRisk = 0.0;
            if (report.ClosestSurfaceDistanceM > 0.0 &&
                double.IsFinite(report.ClosestSurfaceDistanceM))
            {
                closestRisk = 1.0 - Math.Clamp(
                    report.ClosestSurfaceDistanceM / Math.Max(1.0, report.AheadDistanceM),
                    0.0,
                    1.0);
            }

            double clearanceRisk = 0.0;
            double minClearance = Math.Min(report.ClearanceLeft, report.ClearanceRight);

            if (double.IsFinite(minClearance) && minClearance > 0.0)
            {
                clearanceRisk = 1.0 - Math.Clamp(
                    minClearance / Math.Max(1.0, report.AheadDistanceM),
                    0.0,
                    1.0);
            }

            double densityRisk = 0.0;
            if (report.TotalObstacleCount > 0)
            {
                densityRisk = Math.Clamp(
                    report.ConsideredObstacleCount / 16.0,
                    0.0,
                    1.0);
            }

            double risk =
                frontRisk * 0.35 +
                closestRisk * 0.30 +
                clearanceRisk * 0.20 +
                densityRisk * 0.15;

            /*
             * HasObstacleAhead tek başına panik sebebi değil.
             * Sadece minimum dikkat seviyesi verir.
             */
            if (report.HasObstacleAhead)
                risk = Math.Max(risk, 0.32);

            /*
             * Geçilebilir koridor riskin anlamını değiştirir.
             * Risk hâlâ vardır ama "dur" değil "kılavuzlu geç" riskidir.
             */
            if (report.HasPassableCorridor)
            {
                double corridorRelief = Math.Clamp(
                    report.CorridorConfidence * 0.55,
                    0.0,
                    0.55);

                risk = Math.Max(0.08, risk - corridorRelief);
            }

            /*
             * Gerçekten çok yakın temas varsa risk yükselir,
             * ama davranış yine BuildObstacleAdvice içinde son çareye ayrılır.
             */
            if (IsCriticalClose(report))
            {
                double closeness = ComputeCloseness01(
                    report.ClosestSurfaceDistanceM,
                    CriticalCloseDistanceM);

                risk = Math.Max(risk, 0.62 + closeness * 0.25);
            }

            if (IsLastResortContact(report))
                risk = 1.0;

            return Math.Clamp(risk, 0.0, 1.0);
        }

        private static bool IsLastResortContact(
            AdvancedAnalysisReport report)
        {
            return report.ClosestSurfaceDistanceM > 0.0 &&
                   double.IsFinite(report.ClosestSurfaceDistanceM) &&
                   report.ClosestSurfaceDistanceM <= ContactDistanceM &&
                   !report.HasPassableCorridor;
        }

        private static bool IsCriticalClose(
            AdvancedAnalysisReport report)
        {
            return report.ClosestSurfaceDistanceM > 0.0 &&
                   double.IsFinite(report.ClosestSurfaceDistanceM) &&
                   report.ClosestSurfaceDistanceM <= CriticalCloseDistanceM;
        }

        private static double ComputeCloseness01(
            double distanceMeters,
            double referenceMeters)
        {
            if (!double.IsFinite(distanceMeters) ||
                distanceMeters <= 0.0 ||
                referenceMeters <= 0.0)
            {
                return 0.0;
            }

            return 1.0 - Math.Clamp(
                distanceMeters / referenceMeters,
                0.0,
                1.0);
        }
    }
}