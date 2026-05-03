namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// GÃ¶rÃ¼ÅŸ/algÄ±lanabilirlik durum modeli.
    ///
    /// Kamera, lidar, sonar, radar ve insan gÃ¶zetimi gibi sistemlerde
    /// algÄ± confidence deÄŸerlerini etkileyebilir.
    /// </summary>
    public readonly record struct SimVisibilityState(
        bool Enabled,
        double VisibilityMeters,
        double LightLevel,
        double UnderwaterVisibilityMeters,
        double OpticalClarity,
        double SensorOcclusionRisk
    )
    {
        public static SimVisibilityState Clear => new(
            Enabled: true,
            VisibilityMeters: 10_000.0,
            LightLevel: 1.0,
            UnderwaterVisibilityMeters: 20.0,
            OpticalClarity: 1.0,
            SensorOcclusionRisk: 0.0
        );

        public SimVisibilityState Sanitized()
        {
            return new SimVisibilityState(
                Enabled: Enabled,
                VisibilityMeters: SafeNonNegative(VisibilityMeters),
                LightLevel: Clamp01(LightLevel),
                UnderwaterVisibilityMeters: SafeNonNegative(UnderwaterVisibilityMeters),
                OpticalClarity: Clamp01(OpticalClarity),
                SensorOcclusionRisk: Clamp01(SensorOcclusionRisk)
            );
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}
