namespace Hydronom.Core.Simulation.Sensors
{
    /// <summary>
    /// Sim sensÃ¶rlerin zamanlama profili.
    ///
    /// Her sensÃ¶r farklÄ± rate, jitter ve stale eÅŸiÄŸine sahip olabilir.
    /// </summary>
    public readonly record struct SimSensorTimingProfile(
        double TargetRateHz,
        double JitterMs,
        double LatencyMs,
        double StaleAfterMs
    )
    {
        public static SimSensorTimingProfile ImuDefault => new(
            TargetRateHz: 100.0,
            JitterMs: 1.0,
            LatencyMs: 2.0,
            StaleAfterMs: 100.0
        );

        public static SimSensorTimingProfile GpsDefault => new(
            TargetRateHz: 5.0,
            JitterMs: 10.0,
            LatencyMs: 80.0,
            StaleAfterMs: 1_000.0
        );

        public static SimSensorTimingProfile LidarDefault => new(
            TargetRateHz: 10.0,
            JitterMs: 5.0,
            LatencyMs: 30.0,
            StaleAfterMs: 500.0
        );

        public static SimSensorTimingProfile CameraDefault => new(
            TargetRateHz: 15.0,
            JitterMs: 10.0,
            LatencyMs: 60.0,
            StaleAfterMs: 500.0
        );

        public SimSensorTimingProfile Sanitized()
        {
            return new SimSensorTimingProfile(
                TargetRateHz: SafePositive(TargetRateHz, 10.0),
                JitterMs: SafeNonNegative(JitterMs),
                LatencyMs: SafeNonNegative(LatencyMs),
                StaleAfterMs: SafePositive(StaleAfterMs, 1_000.0)
            );
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
