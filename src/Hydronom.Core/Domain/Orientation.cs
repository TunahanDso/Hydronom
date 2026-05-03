using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// 6-DoF yÃ¶nelim modeli.
    ///
    /// DÄ±ÅŸarÄ±ya Euler aÃ§Ä±larÄ± derece cinsinden verilir.
    /// Ä°Ã§eride quaternion tek gerÃ§ek yÃ¶nelim kaynaÄŸÄ± olarak tutulur.
    /// </summary>
    public readonly record struct Orientation
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        public double RollDeg { get; init; }
        public double PitchDeg { get; init; }
        public double YawDeg { get; init; }

        public double Qw { get; init; }
        public double Qx { get; init; }
        public double Qy { get; init; }
        public double Qz { get; init; }

        public static Orientation Zero => new(0.0, 0.0, 0.0);

        public bool IsFinite =>
            double.IsFinite(RollDeg) &&
            double.IsFinite(PitchDeg) &&
            double.IsFinite(YawDeg) &&
            double.IsFinite(Qw) &&
            double.IsFinite(Qx) &&
            double.IsFinite(Qy) &&
            double.IsFinite(Qz);

        /// <summary>
        /// Euler aÃ§Ä±larÄ±yla yÃ¶nelim oluÅŸturur.
        /// </summary>
        public Orientation(double rollDeg, double pitchDeg, double yawDeg)
        {
            rollDeg = SanitizeScalar(rollDeg);
            pitchDeg = SanitizeScalar(pitchDeg);
            yawDeg = SanitizeScalar(yawDeg);

            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);

            (Qw, Qx, Qy, Qz) = FromEuler(RollDeg, PitchDeg, YawDeg);
        }

        /// <summary>
        /// Quaternion ile yÃ¶nelim oluÅŸturur.
        /// </summary>
        public Orientation(double qw, double qx, double qy, double qz, bool fromQuaternion)
        {
            qw = SanitizeScalar(qw, 1.0);
            qx = SanitizeScalar(qx);
            qy = SanitizeScalar(qy);
            qz = SanitizeScalar(qz);

            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            Qw = qw;
            Qx = qx;
            Qy = qy;
            Qz = qz;

            (var rollDeg, var pitchDeg, var yawDeg) = ToEuler(qw, qx, qy, qz);

            RollDeg = NormalizeDeg(rollDeg);
            PitchDeg = Clamp(pitchDeg, -90.0, 90.0);
            YawDeg = NormalizeDeg(yawDeg);
        }

        /// <summary>
        /// SayÄ±sal olarak bozulmuÅŸ orientation deÄŸerlerini temizler.
        /// </summary>
        public Orientation Sanitized()
        {
            if (!IsFinite)
                return Zero;

            return new Orientation(Qw, Qx, Qy, Qz, fromQuaternion: true);
        }

        /// <summary>
        /// VektÃ¶rÃ¼ dÃ¼nya frame'den body frame'e dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public Vec3 WorldToBody(Vec3 worldVector)
        {
            var safe = Sanitized();
            return RotateByQuaternion(safe.Qw, -safe.Qx, -safe.Qy, -safe.Qz, SanitizeVec(worldVector));
        }

        /// <summary>
        /// VektÃ¶rÃ¼ body frame'den dÃ¼nya frame'e dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public Vec3 BodyToWorld(Vec3 bodyVector)
        {
            var safe = Sanitized();
            return RotateByQuaternion(safe.Qw, safe.Qx, safe.Qy, safe.Qz, SanitizeVec(bodyVector));
        }

        /// <summary>
        /// Body frame aÃ§Ä±sal hÄ±z ile quaternion yÃ¶nelimi entegre eder.
        /// angularVelocityRad body frame'de [rad/s] kabul edilir.
        /// </summary>
        public Orientation IntegrateBodyAngularVelocityRad(Vec3 angularVelocityRad, double dt)
        {
            if (dt <= 0.0 || !double.IsFinite(dt))
                return this;

            var safe = Sanitized();
            var omega = SanitizeVec(angularVelocityRad);

            double omegaMag = Math.Sqrt(
                omega.X * omega.X +
                omega.Y * omega.Y +
                omega.Z * omega.Z
            );

            if (omegaMag < 1e-12)
                return safe;

            double angle = omegaMag * dt;
            double half = angle * 0.5;

            double sinHalf = Math.Sin(half);
            double cosHalf = Math.Cos(half);

            double invOmega = 1.0 / omegaMag;

            var delta = new Orientation(
                cosHalf,
                omega.X * invOmega * sinHalf,
                omega.Y * invOmega * sinHalf,
                omega.Z * invOmega * sinHalf,
                fromQuaternion: true
            );

            return safe.Combine(delta);
        }

        /// <summary>
        /// Ä°ki orientation deÄŸerini birleÅŸtirir.
        /// SonuÃ§: this âŠ— other
        /// </summary>
        public Orientation Combine(Orientation other)
        {
            var a = Sanitized();
            var b = other.Sanitized();

            double qw = a.Qw * b.Qw - a.Qx * b.Qx - a.Qy * b.Qy - a.Qz * b.Qz;
            double qx = a.Qw * b.Qx + a.Qx * b.Qw + a.Qy * b.Qz - a.Qz * b.Qy;
            double qy = a.Qw * b.Qy - a.Qx * b.Qz + a.Qy * b.Qw + a.Qz * b.Qx;
            double qz = a.Qw * b.Qz + a.Qx * b.Qy - a.Qy * b.Qx + a.Qz * b.Qw;

            return new Orientation(qw, qx, qy, qz, fromQuaternion: true);
        }

        /// <summary>
        /// Body frame ileri ekseninin dÃ¼nya frame karÅŸÄ±lÄ±ÄŸÄ±.
        /// </summary>
        public Vec3 ForwardWorld => BodyToWorld(new Vec3(1.0, 0.0, 0.0));

        /// <summary>
        /// Body frame saÄŸ ekseninin dÃ¼nya frame karÅŸÄ±lÄ±ÄŸÄ±.
        /// </summary>
        public Vec3 RightWorld => BodyToWorld(new Vec3(0.0, 1.0, 0.0));

        /// <summary>
        /// Body frame yukarÄ± ekseninin dÃ¼nya frame karÅŸÄ±lÄ±ÄŸÄ±.
        /// </summary>
        public Vec3 UpWorld => BodyToWorld(new Vec3(0.0, 0.0, 1.0));

        public override string ToString()
        {
            return $"R={RollDeg:F1}Â°, P={PitchDeg:F1}Â°, Y={YawDeg:F1}Â°";
        }

        public static (double qw, double qx, double qy, double qz) FromEuler(
            double rollDeg,
            double pitchDeg,
            double yawDeg
        )
        {
            double r = SanitizeScalar(rollDeg) * DegToRad;
            double p = SanitizeScalar(pitchDeg) * DegToRad;
            double y = SanitizeScalar(yawDeg) * DegToRad;

            double cy = Math.Cos(y * 0.5);
            double sy = Math.Sin(y * 0.5);
            double cp = Math.Cos(p * 0.5);
            double sp = Math.Sin(p * 0.5);
            double cr = Math.Cos(r * 0.5);
            double sr = Math.Sin(r * 0.5);

            double qw = cr * cp * cy + sr * sp * sy;
            double qx = sr * cp * cy - cr * sp * sy;
            double qy = cr * sp * cy + sr * cp * sy;
            double qz = cr * cp * sy - sr * sp * cy;

            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);
            return (qw, qx, qy, qz);
        }

        public static (double rollDeg, double pitchDeg, double yawDeg) ToEuler(
            double qw,
            double qx,
            double qy,
            double qz
        )
        {
            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            double sinrCosp = 2.0 * (qw * qx + qy * qz);
            double cosrCosp = 1.0 - 2.0 * (qx * qx + qy * qy);
            double roll = Math.Atan2(sinrCosp, cosrCosp);

            double sinp = 2.0 * (qw * qy - qz * qx);
            double pitch = Math.Abs(sinp) >= 1.0
                ? Math.CopySign(Math.PI / 2.0, sinp)
                : Math.Asin(sinp);

            double sinyCosp = 2.0 * (qw * qz + qx * qy);
            double cosyCosp = 1.0 - 2.0 * (qy * qy + qz * qz);
            double yaw = Math.Atan2(sinyCosp, cosyCosp);

            return (
                roll * RadToDeg,
                pitch * RadToDeg,
                yaw * RadToDeg
            );
        }

        private static Vec3 RotateByQuaternion(double qw, double qx, double qy, double qz, Vec3 v)
        {
            NormalizeQuaternion(ref qw, ref qx, ref qy, ref qz);

            double vx = v.X;
            double vy = v.Y;
            double vz = v.Z;

            double tx = 2.0 * (qy * vz - qz * vy);
            double ty = 2.0 * (qz * vx - qx * vz);
            double tz = 2.0 * (qx * vy - qy * vx);

            double cx = qy * tz - qz * ty;
            double cy = qz * tx - qx * tz;
            double cz = qx * ty - qy * tx;

            return new Vec3(
                vx + qw * tx + cx,
                vy + qw * ty + cy,
                vz + qw * tz + cz
            );
        }

        private static void NormalizeQuaternion(ref double qw, ref double qx, ref double qy, ref double qz)
        {
            double norm = Math.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);

            if (!double.IsFinite(norm) || norm < 1e-12)
            {
                qw = 1.0;
                qx = 0.0;
                qy = 0.0;
                qz = 0.0;
                return;
            }

            qw /= norm;
            qx /= norm;
            qy /= norm;
            qz /= norm;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                SanitizeScalar(v.X),
                SanitizeScalar(v.Y),
                SanitizeScalar(v.Z)
            );
        }

        private static double SanitizeScalar(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
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
