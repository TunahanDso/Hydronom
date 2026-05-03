namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// Genel hava durumu modeli.
    ///
    /// Bu model kamera kalitesi, gÃ¶rÃ¼ÅŸ, gÃ¶rev riski, yelken/hava aracÄ± davranÄ±ÅŸÄ±
    /// ve Ops ortam gÃ¶sterimi iÃ§in kullanÄ±labilir.
    /// </summary>
    public readonly record struct SimWeatherState(
        bool Enabled,
        double AirTemperatureC,
        double PressureHPa,
        double HumidityPercent,
        double RainIntensity,
        double FogDensity,
        double Cloudiness,
        double StormRisk
    )
    {
        public static SimWeatherState Clear => new(
            Enabled: true,
            AirTemperatureC: 20.0,
            PressureHPa: 1013.25,
            HumidityPercent: 50.0,
            RainIntensity: 0.0,
            FogDensity: 0.0,
            Cloudiness: 0.1,
            StormRisk: 0.0
        );

        public SimWeatherState Sanitized()
        {
            return new SimWeatherState(
                Enabled: Enabled,
                AirTemperatureC: Safe(AirTemperatureC, 20.0),
                PressureHPa: SafePositive(PressureHPa, 1013.25),
                HumidityPercent: Clamp(HumidityPercent, 0.0, 100.0),
                RainIntensity: Clamp01(RainIntensity),
                FogDensity: Clamp01(FogDensity),
                Cloudiness: Clamp01(Cloudiness),
                StormRisk: Clamp01(StormRisk)
            );
        }

        private static double Safe(double value, double fallback)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
