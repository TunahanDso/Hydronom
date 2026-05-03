namespace Hydronom.Core.State.Models
{
    /// <summary>
    /// GÃ¶vde yÃ¶nelimi ve aÃ§Ä±sal hÄ±z modeli.
    ///
    /// Pose iÃ§indeki yaw operasyonel heading olarak dÃ¼ÅŸÃ¼nÃ¼lebilir.
    /// Buradaki yaw ise gÃ¶vde attitude bilgisidir.
    /// BaÅŸlangÄ±Ã§ta ikisi aynÄ± olabilir; ileride frame dÃ¶nÃ¼ÅŸÃ¼mleri ile ayrÄ±labilir.
    /// </summary>
    public readonly record struct VehicleAttitude(
        double RollDeg,
        double PitchDeg,
        double YawDeg,
        double RollRateDegSec,
        double PitchRateDegSec,
        double YawRateDegSec
    )
    {
        public static VehicleAttitude Zero => new(
            RollDeg: 0.0,
            PitchDeg: 0.0,
            YawDeg: 0.0,
            RollRateDegSec: 0.0,
            PitchRateDegSec: 0.0,
            YawRateDegSec: 0.0
        );

        public bool IsFinite =>
            double.IsFinite(RollDeg) &&
            double.IsFinite(PitchDeg) &&
            double.IsFinite(YawDeg) &&
            double.IsFinite(RollRateDegSec) &&
            double.IsFinite(PitchRateDegSec) &&
            double.IsFinite(YawRateDegSec);

        public VehicleAttitude Sanitized()
        {
            return new VehicleAttitude(
                NormalizeDeg(Sanitize(RollDeg)),
                Clamp(Sanitize(PitchDeg), -90.0, 90.0),
                NormalizeDeg(Sanitize(YawDeg)),
                Sanitize(RollRateDegSec),
                Sanitize(PitchRateDegSec),
                Sanitize(YawRateDegSec)
            );
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
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

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
