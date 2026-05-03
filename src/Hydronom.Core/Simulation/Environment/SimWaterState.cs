using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// Su ortamÄ± durum modeli.
    ///
    /// Deniz/su Ã¼stÃ¼/su altÄ± araÃ§larÄ±nda physics, buoyancy, drag, sensor quality
    /// ve gÃ¶rev gÃ¼venliÄŸi iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimWaterState(
        bool Enabled,
        double WaterLevelZ,
        double DensityKgM3,
        double TemperatureC,
        double SalinityPsu,
        double TurbidityNtu,
        double WaveHeightMeters,
        double WaveDirectionDeg,
        double SurfaceRoughness,
        SimVector3 CurrentVelocityWorld
    )
    {
        public static SimWaterState None => new(
            Enabled: false,
            WaterLevelZ: 0.0,
            DensityKgM3: 997.0,
            TemperatureC: 20.0,
            SalinityPsu: 0.0,
            TurbidityNtu: 0.0,
            WaveHeightMeters: 0.0,
            WaveDirectionDeg: 0.0,
            SurfaceRoughness: 0.0,
            CurrentVelocityWorld: SimVector3.Zero
        );

        public static SimWaterState DefaultSea => new(
            Enabled: true,
            WaterLevelZ: 0.0,
            DensityKgM3: 1025.0,
            TemperatureC: 15.0,
            SalinityPsu: 35.0,
            TurbidityNtu: 2.0,
            WaveHeightMeters: 0.15,
            WaveDirectionDeg: 0.0,
            SurfaceRoughness: 0.25,
            CurrentVelocityWorld: SimVector3.Zero
        );

        public SimWaterState Sanitized()
        {
            return new SimWaterState(
                Enabled: Enabled,
                WaterLevelZ: Safe(WaterLevelZ),
                DensityKgM3: SafePositive(DensityKgM3, 997.0),
                TemperatureC: Safe(TemperatureC, 20.0),
                SalinityPsu: SafeNonNegative(SalinityPsu),
                TurbidityNtu: SafeNonNegative(TurbidityNtu),
                WaveHeightMeters: SafeNonNegative(WaveHeightMeters),
                WaveDirectionDeg: NormalizeDeg(WaveDirectionDeg),
                SurfaceRoughness: Clamp01(SurfaceRoughness),
                CurrentVelocityWorld: CurrentVelocityWorld.Sanitized()
            );
        }

        public bool IsPointUnderwater(SimVector3 point)
        {
            if (!Enabled)
                return false;

            return point.Sanitized().Z < WaterLevelZ;
        }

        private static double Safe(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
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

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}
