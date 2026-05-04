using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Analysis katmanının ürettiği geniş operasyonel bağlam.
    ///
    /// Şimdilik obstacle/sector riskinden türetilir.
    /// İleride batarya, rüzgâr, akıntı, sensör güveni, görev süresi,
    /// no-progress ve return-home değerlendirmeleri de buraya bağlanacak.
    /// </summary>
    public readonly record struct OperationalAnalysisContext(
        DateTime TimestampUtc,
        AdvancedAnalysisReport ObstacleReport,
        DecisionAdviceProfile Advice,
        double OverallRiskScore,
        double ObstacleRiskScore,
        double EnvironmentRiskScore,
        double EnergyRiskScore,
        double SensorRiskScore,
        bool HasCriticalRisk,
        bool HasWarning,
        string Summary
    )
    {
        public static OperationalAnalysisContext Empty { get; } = new(
            TimestampUtc: DateTime.MinValue,
            ObstacleReport: AdvancedAnalysisReport.Empty,
            Advice: DecisionAdviceProfile.Neutral,
            OverallRiskScore: 0.0,
            ObstacleRiskScore: 0.0,
            EnvironmentRiskScore: 0.0,
            EnergyRiskScore: 0.0,
            SensorRiskScore: 0.0,
            HasCriticalRisk: false,
            HasWarning: false,
            Summary: "No operational analysis computed."
        );

        public static OperationalAnalysisContext FromObstacleReport(
            AdvancedAnalysisReport report,
            DecisionAdviceProfile advice,
            double obstacleRiskScore)
        {
            obstacleRiskScore = Clamp01(obstacleRiskScore);

            bool critical =
                advice.RecommendMissionAbort ||
                report.FrontRiskScore >= 2.0 ||
                report.ClosestSurfaceDistanceM <= 0.75;

            bool warning =
                critical ||
                advice.RequireSlowMode ||
                advice.PreferSafeHeading ||
                report.HasObstacleAhead ||
                obstacleRiskScore >= 0.35;

            string summary = BuildSummary(report, advice, obstacleRiskScore, critical, warning);

            return new OperationalAnalysisContext(
                TimestampUtc: DateTime.UtcNow,
                ObstacleReport: report,
                Advice: advice.Sanitized(),
                OverallRiskScore: obstacleRiskScore,
                ObstacleRiskScore: obstacleRiskScore,
                EnvironmentRiskScore: 0.0,
                EnergyRiskScore: 0.0,
                SensorRiskScore: 0.0,
                HasCriticalRisk: critical,
                HasWarning: warning,
                Summary: summary
            );
        }

        public override string ToString()
        {
            return
                $"OperationalAnalysis risk={OverallRiskScore:F2} " +
                $"critical={HasCriticalRisk} warning={HasWarning} " +
                $"advice=({Advice.PrimaryReason}) summary={Summary}";
        }

        private static string BuildSummary(
            AdvancedAnalysisReport report,
            DecisionAdviceProfile advice,
            double obstacleRiskScore,
            bool critical,
            bool warning)
        {
            if (critical)
            {
                return
                    $"Critical operational risk: obstacleRisk={obstacleRiskScore:F2}, " +
                    $"frontRisk={report.FrontRiskScore:F2}, closest={report.ClosestSurfaceDistanceM:F2}m, " +
                    $"advice={advice.PrimaryReason}.";
            }

            if (warning)
            {
                return
                    $"Operational warning: obstacleRisk={obstacleRiskScore:F2}, " +
                    $"suggestedSide={report.SuggestedSide}, bestOffset={report.BestHeadingOffsetDeg:F1}deg, " +
                    $"advice={advice.PrimaryReason}.";
            }

            return
                $"Operational status nominal: obstacleRisk={obstacleRiskScore:F2}, " +
                $"clearanceL={report.ClearanceLeft:F2}m, clearanceR={report.ClearanceRight:F2}m.";
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Gelecekte batarya / enerji yönetimi için doldurulacak bağlam.
    /// Şimdilik nötr varsayılan modeldir.
    /// </summary>
    public readonly record struct EnergyAnalysisContext(
        double BatteryPercent,
        bool IsLowBattery,
        bool IsCriticalBattery,
        bool ReturnHomeRecommended,
        double SuggestedPowerScale
    )
    {
        public static EnergyAnalysisContext Unknown { get; } = new(
            BatteryPercent: double.NaN,
            IsLowBattery: false,
            IsCriticalBattery: false,
            ReturnHomeRecommended: false,
            SuggestedPowerScale: 1.0
        );
    }

    /// <summary>
    /// Rüzgâr, akıntı, drift, dalga gibi çevresel faktörler için gelecek bağlam.
    /// </summary>
    public readonly record struct EnvironmentAnalysisContext(
        double WindRiskScore,
        double CurrentRiskScore,
        double DriftRiskScore,
        double SuggestedHeadingOffsetDeg,
        bool RequiresCompensation
    )
    {
        public static EnvironmentAnalysisContext Neutral { get; } = new(
            WindRiskScore: 0.0,
            CurrentRiskScore: 0.0,
            DriftRiskScore: 0.0,
            SuggestedHeadingOffsetDeg: 0.0,
            RequiresCompensation: false
        );
    }

    /// <summary>
    /// GPS/IMU/LiDAR/DVL/kamera vb. sensör güveni için gelecek bağlam.
    /// </summary>
    public readonly record struct SensorConfidenceContext(
        double OverallConfidence,
        bool HasStalePose,
        bool HasLowNavigationConfidence,
        bool HasCriticalSensorLoss,
        string Reason
    )
    {
        public static SensorConfidenceContext Unknown { get; } = new(
            OverallConfidence: 1.0,
            HasStalePose: false,
            HasLowNavigationConfidence: false,
            HasCriticalSensorLoss: false,
            Reason: "UNKNOWN"
        );
    }
}