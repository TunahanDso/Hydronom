namespace Hydronom.Core.Sensors.Imu.Models
{
    /// <summary>
    /// IMU/AHRS sensÃ¶r verisi.
    ///
    /// Ä°vme dÃ¼nya veya sensÃ¶r frame yorumuna gÃ¶re frame bilgisiyle birlikte deÄŸerlendirilmelidir.
    /// Gyro deÄŸerleri rad/s olarak tutulur.
    /// Euler aÃ§Ä±larÄ± derece cinsindendir.
    /// </summary>
    public readonly record struct ImuSampleData(
        double Ax,
        double Ay,
        double Az,
        double GxRadSec,
        double GyRadSec,
        double GzRadSec,
        double? Mx,
        double? My,
        double? Mz,
        double? RollDeg,
        double? PitchDeg,
        double? YawDeg,
        double? TemperatureC
    )
    {
        public static ImuSampleData Zero => new(
            Ax: 0.0,
            Ay: 0.0,
            Az: 0.0,
            GxRadSec: 0.0,
            GyRadSec: 0.0,
            GzRadSec: 0.0,
            Mx: null,
            My: null,
            Mz: null,
            RollDeg: null,
            PitchDeg: null,
            YawDeg: null,
            TemperatureC: null
        );

        public bool IsFinite =>
            double.IsFinite(Ax) &&
            double.IsFinite(Ay) &&
            double.IsFinite(Az) &&
            double.IsFinite(GxRadSec) &&
            double.IsFinite(GyRadSec) &&
            double.IsFinite(GzRadSec) &&
            IsNullableFinite(Mx) &&
            IsNullableFinite(My) &&
            IsNullableFinite(Mz) &&
            IsNullableFinite(RollDeg) &&
            IsNullableFinite(PitchDeg) &&
            IsNullableFinite(YawDeg) &&
            IsNullableFinite(TemperatureC);

        public ImuSampleData Sanitized()
        {
            return new ImuSampleData(
                Ax: Safe(Ax),
                Ay: Safe(Ay),
                Az: Safe(Az),
                GxRadSec: Safe(GxRadSec),
                GyRadSec: Safe(GyRadSec),
                GzRadSec: Safe(GzRadSec),
                Mx: SafeNullable(Mx),
                My: SafeNullable(My),
                Mz: SafeNullable(Mz),
                RollDeg: SafeNullable(RollDeg),
                PitchDeg: SafeNullable(PitchDeg),
                YawDeg: SafeNullable(YawDeg),
                TemperatureC: SafeNullable(TemperatureC)
            );
        }

        private static bool IsNullableFinite(double? value)
        {
            return !value.HasValue || double.IsFinite(value.Value);
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double? SafeNullable(double? value)
        {
            if (!value.HasValue)
                return null;

            return double.IsFinite(value.Value) ? value.Value : null;
        }
    }
}


