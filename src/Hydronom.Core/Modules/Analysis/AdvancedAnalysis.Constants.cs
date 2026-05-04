namespace Hydronom.Core.Modules
{
    public sealed partial class AdvancedAnalysis
    {
        private const double DefaultAheadDistanceM = 12.0;
        private const double DefaultHalfFovDeg = 60.0;
        private const int DefaultSectorCount = 31;

        private const double DefaultSafetyMarginM = 0.80;
        private const double DefaultDangerDistanceM = 4.0;
        private const double DefaultSideWindowDeg = 70.0;

        private const double DefaultFrontWeight = 1.35;
        private const double DefaultSizeWeight = 0.90;
        private const double DefaultCenterBiasWeight = 0.10;
        private const double DefaultFrontCriticalRiskThreshold = 1.15;
    }
}