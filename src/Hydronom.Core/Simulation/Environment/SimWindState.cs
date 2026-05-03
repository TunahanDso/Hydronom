using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// RÃ¼zgar ortamÄ± durum modeli.
    ///
    /// Yelkenli, hava aracÄ± ve aÃ§Ä±k alan simÃ¼lasyonlarÄ±nda karar, physics ve sensor confidence
    /// katmanlarÄ±nÄ± etkileyebilir.
    /// </summary>
    public readonly record struct SimWindState(
        bool Enabled,
        SimVector3 VelocityWorld,
        double SpeedMps,
        double DirectionDeg,
        double GustSpeedMps,
        double GustProbability,
        double Turbulence
    )
    {
        public static SimWindState None => new(
            Enabled: false,
            VelocityWorld: SimVector3.Zero,
            SpeedMps: 0.0,
            DirectionDeg: 0.0,
            GustSpeedMps: 0.0,
            GustProbability: 0.0,
            Turbulence: 0.0
        );

        public static SimWindState Calm => None with
        {
            Enabled = true
        };

        public SimWindState Sanitized()
        {
            var velocity = VelocityWorld.Sanitized();
            var speed = SafeNonNegative(SpeedMps);

            if (speed <= 0.0 && velocity.Length > 0.0)
                speed = velocity.Length;

            return new SimWindState(
                Enabled: Enabled,
                VelocityWorld: velocity,
                SpeedMps: speed,
                DirectionDeg: NormalizeDeg(DirectionDeg),
                GustSpeedMps: SafeNonNegative(GustSpeedMps),
                GustProbability: Clamp01(GustProbability),
                Turbulence: Clamp01(Turbulence)
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
