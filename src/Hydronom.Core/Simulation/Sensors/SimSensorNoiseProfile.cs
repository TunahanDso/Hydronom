namespace Hydronom.Core.Simulation.Sensors
{
    /// <summary>
    /// Sim sensÃ¶rlerin gerÃ§ekÃ§i Ã¶lÃ§Ã¼m Ã¼retmesi iÃ§in noise profili.
    /// </summary>
    public readonly record struct SimSensorNoiseProfile(
        double PositionNoiseMeters,
        double VelocityNoiseMetersPerSec,
        double AccelerationNoiseMetersPerSec2,
        double GyroNoiseDegPerSec,
        double AngleNoiseDeg,
        double DepthNoiseMeters,
        double RangeNoiseMeters,
        double Bias,
        bool Enabled
    )
    {
        public static SimSensorNoiseProfile None => new(
            PositionNoiseMeters: 0.0,
            VelocityNoiseMetersPerSec: 0.0,
            AccelerationNoiseMetersPerSec2: 0.0,
            GyroNoiseDegPerSec: 0.0,
            AngleNoiseDeg: 0.0,
            DepthNoiseMeters: 0.0,
            RangeNoiseMeters: 0.0,
            Bias: 0.0,
            Enabled: false
        );

        public static SimSensorNoiseProfile Mild => new(
            PositionNoiseMeters: 0.05,
            VelocityNoiseMetersPerSec: 0.02,
            AccelerationNoiseMetersPerSec2: 0.05,
            GyroNoiseDegPerSec: 0.10,
            AngleNoiseDeg: 0.25,
            DepthNoiseMeters: 0.03,
            RangeNoiseMeters: 0.05,
            Bias: 0.0,
            Enabled: true
        );

        public SimSensorNoiseProfile Sanitized()
        {
            return new SimSensorNoiseProfile(
                PositionNoiseMeters: SafeNonNegative(PositionNoiseMeters),
                VelocityNoiseMetersPerSec: SafeNonNegative(VelocityNoiseMetersPerSec),
                AccelerationNoiseMetersPerSec2: SafeNonNegative(AccelerationNoiseMetersPerSec2),
                GyroNoiseDegPerSec: SafeNonNegative(GyroNoiseDegPerSec),
                AngleNoiseDeg: SafeNonNegative(AngleNoiseDeg),
                DepthNoiseMeters: SafeNonNegative(DepthNoiseMeters),
                RangeNoiseMeters: SafeNonNegative(RangeNoiseMeters),
                Bias: double.IsFinite(Bias) ? Bias : 0.0,
                Enabled: Enabled
            );
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
