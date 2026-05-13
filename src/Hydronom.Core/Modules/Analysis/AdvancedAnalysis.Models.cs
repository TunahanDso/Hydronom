namespace Hydronom.Core.Modules
{
    internal readonly record struct AnalysisParameters(
        double AheadDistanceM,
        double HalfFovDeg,
        int SectorCount,
        double SafetyMarginM,
        double DangerDistanceM,
        double SideWindowDeg,
        double FrontWeight,
        double SizeWeight,
        double CenterBiasWeight,
        double FrontCriticalRiskThreshold)
    {
        public AnalysisParameters Sanitized()
        {
            var sectors = AdvancedAnalysis.ClampSectorCount(SectorCount);
            if (sectors % 2 == 0)
                sectors++;

            return this with
            {
                AheadDistanceM = AdvancedAnalysis.ClampAhead(AheadDistanceM),
                HalfFovDeg = AdvancedAnalysis.ClampFov(HalfFovDeg),
                SectorCount = sectors,
                SafetyMarginM = AdvancedAnalysis.ClampRange(SafetyMarginM, 0.0, 10.0, 0.80),
                DangerDistanceM = AdvancedAnalysis.ClampRange(DangerDistanceM, 0.5, 100.0, 4.0),
                SideWindowDeg = AdvancedAnalysis.ClampRange(SideWindowDeg, 10.0, 120.0, 70.0),
                FrontWeight = AdvancedAnalysis.ClampRange(FrontWeight, 0.1, 5.0, 1.35),
                SizeWeight = AdvancedAnalysis.ClampRange(SizeWeight, 0.0, 5.0, 0.90),
                CenterBiasWeight = AdvancedAnalysis.ClampRange(CenterBiasWeight, 0.0, 1.0, 0.10),
                FrontCriticalRiskThreshold = AdvancedAnalysis.ClampRange(FrontCriticalRiskThreshold, 0.1, 10.0, 1.15)
            };
        }
    }

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
        double HalfFovDeg,
        bool HasPassableCorridor,
        double CorridorCenterOffsetDeg,
        double CorridorWidthMeters,
        double CorridorClearanceMeters,
        double CorridorConfidence
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
                HalfFovDeg: 0.0,
                HasPassableCorridor: false,
                CorridorCenterOffsetDeg: 0.0,
                CorridorWidthMeters: 0.0,
                CorridorClearanceMeters: 0.0,
                CorridorConfidence: 0.0
            );

        public override string ToString()
        {
            return
                $"AdvancedAnalysis danger={HasObstacleAhead} " +
                $"clear(L/R)=({ClearanceLeft:F2}/{ClearanceRight:F2}) " +
                $"riskFront={FrontRiskScore:F3} " +
                $"score(L/R)=({LeftScore:F3}/{RightScore:F3}) " +
                $"bestOffset={BestHeadingOffsetDeg:F1}° " +
                $"side={SuggestedSide} " +
                $"obs={ConsideredObstacleCount}/{TotalObstacleCount} " +
                $"corridor={HasPassableCorridor} " +
                $"corridorOffset={CorridorCenterOffsetDeg:F1}° " +
                $"corridorWidth={CorridorWidthMeters:F2}m " +
                $"corridorClear={CorridorClearanceMeters:F2}m " +
                $"corridorConf={CorridorConfidence:F2}";
        }
    }

    public sealed partial class AdvancedAnalysis
    {
        private readonly record struct ObstacleSample(
            double CenterDistanceM,
            double SurfaceDistanceM,
            double RelativeAngleDeg,
            double AngularRadiusDeg,
            double RadiusM
        );

        private readonly record struct PassableCorridorCandidate(
            bool IsValid,
            double CenterOffsetDeg,
            double WidthMeters,
            double ClearanceMeters,
            double Confidence
        )
        {
            public static PassableCorridorCandidate None { get; } =
                new(
                    IsValid: false,
                    CenterOffsetDeg: 0.0,
                    WidthMeters: 0.0,
                    ClearanceMeters: 0.0,
                    Confidence: 0.0
                );
        }
    }
}