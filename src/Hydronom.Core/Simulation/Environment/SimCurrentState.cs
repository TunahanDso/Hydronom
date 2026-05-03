using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// Su akÄ±ntÄ±sÄ± durum modeli.
    ///
    /// Bu model yÃ¼zey aracÄ±, su altÄ± aracÄ±, yelkenli ve sonar/DVL simÃ¼lasyonu iÃ§in Ã¶nemlidir.
    /// </summary>
    public readonly record struct SimCurrentState(
        bool Enabled,
        SimVector3 VelocityWorld,
        double SpeedMps,
        double DirectionDeg,
        double DepthDependency,
        double Turbulence
    )
    {
        public static SimCurrentState None => new(
            Enabled: false,
            VelocityWorld: SimVector3.Zero,
            SpeedMps: 0.0,
            DirectionDeg: 0.0,
            DepthDependency: 0.0,
            Turbulence: 0.0
        );

        public SimCurrentState Sanitized()
        {
            var velocity = VelocityWorld.Sanitized();
            var speed = SafeNonNegative(SpeedMps);

            if (speed <= 0.0 && velocity.Length > 0.0)
                speed = velocity.Length;

            return new SimCurrentState(
                Enabled: Enabled,
                VelocityWorld: velocity,
                SpeedMps: speed,
                DirectionDeg: NormalizeDeg(DirectionDeg),
                DepthDependency: Clamp01(DepthDependency),
                Turbulence: Clamp01(Turbulence)
            );
        }

        public SimVector3 GetVelocityAtDepth(double z)
        {
            var safe = Sanitized();

            if (!safe.Enabled)
                return SimVector3.Zero;

            // Ä°lk sÃ¼rÃ¼mde basit derinlik etkisi:
            // Z azaldÄ±kÃ§a akÄ±ntÄ± etkisi hafifÃ§e Ã¶lÃ§eklenebilir.
            var depthFactor = 1.0 + Clamp01(System.Math.Abs(z) / 100.0) * safe.DepthDependency;

            return safe.VelocityWorld * depthFactor;
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
