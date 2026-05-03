namespace Hydronom.Runtime.Tuning
{
    public interface ITuningSink
    {
        void SetLimiter(double? throttleRatePerSec, double? rudderRatePerSec);
        void SetAnalysis(double? aheadDistanceM, double? halfFovDeg);
        void SetTick(int? tickMs);

        RuntimeStatus GetStatus();
    }

    public record RuntimeStatus(
        double LimiterThrottleRatePerSec,
        double LimiterRudderRatePerSec,
        double AnalysisAheadDistanceM,
        double AnalysisHalfFovDeg,
        int TickMs,
        bool TaskActive
    );
}

