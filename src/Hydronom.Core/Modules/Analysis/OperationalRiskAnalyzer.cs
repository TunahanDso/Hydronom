using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedAnalysisReport içinden operasyonel risk ve decision advice üretir.
    ///
    /// Bu sınıf Analysis → Decision köprüsünün ilk adımıdır.
    /// Obstacle/clearance/sector analizinden karar önerisi çıkarır.
    /// Ayrıca geçilebilir koridor tespit edildiğinde panik kaçınma yerine kontrollü geçiş tavsiyesi üretir.
    /// </summary>
    public static class OperationalRiskAnalyzer
    {
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
            if (report.HasPassableCorridor && report.CorridorConfidence >= 0.30)
            {
                double confidence = Math.Clamp(report.CorridorConfidence, 0.0, 1.0);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(0.55 + confidence * 0.20, 0.45, 0.75),
                    ThrottleScale: Math.Clamp(0.45 + confidence * 0.25, 0.40, 0.70),
                    YawAggressionScale: Math.Clamp(0.90 + confidence * 0.35, 0.90, 1.25),
                    ArrivalCautionScale: Math.Clamp(1.10 + (1.0 - confidence) * 0.50, 1.10, 1.60),
                    ObstacleAvoidanceUrgency: Math.Clamp(0.15 + (1.0 - confidence) * 0.20, 0.15, 0.35),
                    HoldPreference: 0.0,
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: true,
                    RecommendHold: false,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "PASSABLE_CORRIDOR",
                    HasPassableCorridor: true,
                    CorridorCenterOffsetDeg: report.CorridorCenterOffsetDeg,
                    CorridorWidthMeters: report.CorridorWidthMeters,
                    CorridorClearanceMeters: report.CorridorClearanceMeters,
                    CorridorConfidence: report.CorridorConfidence,
                    SuppressObstaclePanic: true,
                    PreferCorridorHeading: true
                ).Sanitized();
            }

            if (report.ClosestSurfaceDistanceM <= 0.75)
            {
                return new DecisionAdviceProfile(
                    MaxSpeedScale: 0.15,
                    ThrottleScale: 0.10,
                    YawAggressionScale: 1.65,
                    ArrivalCautionScale: 2.50,
                    ObstacleAvoidanceUrgency: 1.0,
                    HoldPreference: 0.70,
                    ForceCoast: true,
                    PreferSafeHeading: true,
                    RequireSlowMode: true,
                    RecommendHold: true,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "CRITICAL_CLOSE_OBSTACLE",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: 0.0,
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                );
            }

            if (report.HasObstacleAhead || report.FrontRiskScore >= 1.15)
            {
                double urgency = Math.Clamp(obstacleRisk + 0.35, 0.35, 1.0);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(1.0 - urgency * 0.65, 0.25, 0.85),
                    ThrottleScale: Math.Clamp(1.0 - urgency * 0.75, 0.20, 0.85),
                    YawAggressionScale: Math.Clamp(1.0 + urgency * 0.65, 1.0, 1.75),
                    ArrivalCautionScale: Math.Clamp(1.0 + urgency * 1.10, 1.0, 2.50),
                    ObstacleAvoidanceUrgency: urgency,
                    HoldPreference: Math.Clamp(urgency * 0.45, 0.0, 0.75),
                    ForceCoast: urgency >= 0.80,
                    PreferSafeHeading: true,
                    RequireSlowMode: urgency >= 0.45,
                    RecommendHold: urgency >= 0.90,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "OBSTACLE_AHEAD",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: 0.0,
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                );
            }

            if (obstacleRisk >= 0.35)
            {
                double caution = Math.Clamp(obstacleRisk, 0.35, 0.80);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Clamp(1.0 - caution * 0.35, 0.55, 0.90),
                    ThrottleScale: Math.Clamp(1.0 - caution * 0.40, 0.50, 0.95),
                    YawAggressionScale: Math.Clamp(1.0 + caution * 0.25, 1.0, 1.40),
                    ArrivalCautionScale: Math.Clamp(1.0 + caution * 0.65, 1.0, 1.80),
                    ObstacleAvoidanceUrgency: caution,
                    HoldPreference: 0.0,
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: false,
                    RecommendHold: false,
                    RecommendReturnHome: false,
                    RecommendMissionAbort: false,
                    PrimaryReason: "ELEVATED_OBSTACLE_RISK",
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: 0.0,
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                );
            }

            return DecisionAdviceProfile.Neutral;
        }

        private static double ComputeObstacleRisk(
            AdvancedAnalysisReport report)
        {
            double frontRisk = Math.Clamp(report.FrontRiskScore / 2.0, 0.0, 1.0);

            double closestRisk = 0.0;
            if (report.ClosestSurfaceDistanceM > 0.0 && double.IsFinite(report.ClosestSurfaceDistanceM))
            {
                closestRisk = 1.0 - Math.Clamp(report.ClosestSurfaceDistanceM / Math.Max(1.0, report.AheadDistanceM), 0.0, 1.0);
            }

            double clearanceRisk = 0.0;
            double minClearance = Math.Min(report.ClearanceLeft, report.ClearanceRight);

            if (double.IsFinite(minClearance) && minClearance > 0.0)
            {
                clearanceRisk = 1.0 - Math.Clamp(minClearance / Math.Max(1.0, report.AheadDistanceM), 0.0, 1.0);
            }

            double densityRisk = 0.0;
            if (report.TotalObstacleCount > 0)
            {
                densityRisk = Math.Clamp(report.ConsideredObstacleCount / 12.0, 0.0, 1.0);
            }

            double risk =
                frontRisk * 0.40 +
                closestRisk * 0.30 +
                clearanceRisk * 0.20 +
                densityRisk * 0.10;

            if (report.HasObstacleAhead)
                risk = Math.Max(risk, 0.45);

            if (report.HasPassableCorridor)
            {
                double corridorRelief = Math.Clamp(report.CorridorConfidence * 0.45, 0.0, 0.45);
                risk = Math.Max(0.10, risk - corridorRelief);
            }

            return Math.Clamp(risk, 0.0, 1.0);
        }
    }
}