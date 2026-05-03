using System;

namespace Hydronom.Core.State.Models
{
    /// <summary>
    /// Operasyonel konum ve heading modeli.
    ///
    /// Pose, aracÄ±n harita/dÃ¼nya dÃ¼zlemindeki operasyonel yerini temsil eder.
    /// Roll ve pitch burada tutulmaz; bunlar VehicleAttitude iÃ§inde tutulur.
    /// </summary>
    public readonly record struct VehiclePose(
        double X,
        double Y,
        double Z,
        double YawDeg,
        string FrameId
    )
    {
        public static VehiclePose Zero => new(
            X: 0.0,
            Y: 0.0,
            Z: 0.0,
            YawDeg: 0.0,
            FrameId: "map"
        );

        public bool IsFinite =>
            double.IsFinite(X) &&
            double.IsFinite(Y) &&
            double.IsFinite(Z) &&
            double.IsFinite(YawDeg);

        public VehiclePose Sanitized()
        {
            return new VehiclePose(
                Sanitize(X),
                Sanitize(Y),
                Sanitize(Z),
                NormalizeDeg(Sanitize(YawDeg)),
                string.IsNullOrWhiteSpace(FrameId) ? "map" : FrameId.Trim()
            );
        }

        public double Distance2DTo(VehiclePose other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double s = dx * dx + dy * dy;

            return s <= 0.0 || !double.IsFinite(s) ? 0.0 : Math.Sqrt(s);
        }

        public double Distance3DTo(VehiclePose other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            double s = dx * dx + dy * dy + dz * dz;

            return s <= 0.0 || !double.IsFinite(s) ? 0.0 : Math.Sqrt(s);
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
    }
}
