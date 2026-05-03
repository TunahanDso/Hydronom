namespace Hydronom.Core.State.Models
{
    /// <summary>
    /// Operasyonel hız modeli.
    ///
    /// Vx/Vy/Vz dünya frame'de lineer hızları temsil eder.
    /// YawRateDegSec operasyonel heading değişim hızıdır.
    /// </summary>
    public readonly record struct VehicleTwist(
        double Vx,
        double Vy,
        double Vz,
        double YawRateDegSec
    )
    {
        public static VehicleTwist Zero => new(
            Vx: 0.0,
            Vy: 0.0,
            Vz: 0.0,
            YawRateDegSec: 0.0
        );

        public double SpeedMps
        {
            get
            {
                double s = Vx * Vx + Vy * Vy + Vz * Vz;
                return s <= 0.0 || !double.IsFinite(s) ? 0.0 : System.Math.Sqrt(s);
            }
        }

        public bool IsFinite =>
            double.IsFinite(Vx) &&
            double.IsFinite(Vy) &&
            double.IsFinite(Vz) &&
            double.IsFinite(YawRateDegSec);

        public VehicleTwist Sanitized()
        {
            return new VehicleTwist(
                Sanitize(Vx),
                Sanitize(Vy),
                Sanitize(Vz),
                Sanitize(YawRateDegSec)
            );
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}