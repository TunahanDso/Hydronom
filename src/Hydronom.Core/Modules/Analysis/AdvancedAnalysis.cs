using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Gelişmiş çevre analizi.
    ///
    /// IAnalysisModule dışarıya hâlâ Insights döndürür.
    /// İçeride ise obstacle, sector, clearance ve risk raporu üretir.
    /// </summary>
    public sealed partial class AdvancedAnalysis : IAnalysisModule
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

        public AdvancedAnalysisReport LastReport
        {
            get { lock (_lock) return _lastReport; }
        }

        public AdvancedAnalysis(
            double aheadDistanceM = DefaultAheadDistanceM,
            double halfFovDeg = DefaultHalfFovDeg,
            int sectorCount = DefaultSectorCount,
            double safetyMarginM = DefaultSafetyMarginM,
            double dangerDistanceM = DefaultDangerDistanceM,
            double sideWindowDeg = DefaultSideWindowDeg,
            double frontWeight = DefaultFrontWeight,
            double sizeWeight = DefaultSizeWeight,
            double centerBiasWeight = DefaultCenterBiasWeight,
            double frontCriticalRiskThreshold = DefaultFrontCriticalRiskThreshold)
        {
            _aheadDistanceM = ClampAhead(aheadDistanceM);
            _halfFovDeg = ClampFov(halfFovDeg);
            _sectorCount = ClampSectorCount(sectorCount);
            _safetyMarginM = ClampRange(safetyMarginM, 0.0, 10.0, DefaultSafetyMarginM);
            _dangerDistanceM = ClampRange(dangerDistanceM, 0.5, 100.0, DefaultDangerDistanceM);
            _sideWindowDeg = ClampRange(sideWindowDeg, 10.0, 120.0, DefaultSideWindowDeg);
            _frontWeight = ClampRange(frontWeight, 0.1, 5.0, DefaultFrontWeight);
            _sizeWeight = ClampRange(sizeWeight, 0.0, 5.0, DefaultSizeWeight);
            _centerBiasWeight = ClampRange(centerBiasWeight, 0.0, 1.0, DefaultCenterBiasWeight);
            _frontCriticalRiskThreshold = ClampRange(frontCriticalRiskThreshold, 0.1, 10.0, DefaultFrontCriticalRiskThreshold);
        }

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
            AnalysisParameters parameters;

            lock (_lock)
            {
                parameters = new AnalysisParameters(
                    AheadDistanceM: _aheadDistanceM,
                    HalfFovDeg: _halfFovDeg,
                    SectorCount: _sectorCount,
                    SafetyMarginM: _safetyMarginM,
                    DangerDistanceM: _dangerDistanceM,
                    SideWindowDeg: _sideWindowDeg,
                    FrontWeight: _frontWeight,
                    SizeWeight: _sizeWeight,
                    CenterBiasWeight: _centerBiasWeight,
                    FrontCriticalRiskThreshold: _frontCriticalRiskThreshold
                );
            }

            parameters = parameters.Sanitized();

            var report = AnalyzeObstacles(frame, parameters);

            lock (_lock)
                _lastReport = report;

            return new Insights(
                HasObstacleAhead: report.HasObstacleAhead,
                ClearanceLeft: report.ClearanceLeft,
                ClearanceRight: report.ClearanceRight
            );
        }
    }
}