using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ± iÃ§in quaternion yÃ¶nelim modeli.
    ///
    /// Ops 3D gÃ¶rÃ¼nÃ¼mÃ¼, 3D engel yÃ¶nelimi, kamera/lidar gÃ¶rÃ¼ÅŸ hacimleri ve
    /// dÃ¼nya nesnelerinin dÃ¶nÃ¼ÅŸleri iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimQuaternion(
        double W,
        double X,
        double Y,
        double Z
    )
    {
        public static SimQuaternion Identity => new(1.0, 0.0, 0.0, 0.0);

        public bool IsFinite =>
            double.IsFinite(W) &&
            double.IsFinite(X) &&
            double.IsFinite(Y) &&
            double.IsFinite(Z);

        public SimQuaternion Sanitized()
        {
            if (!IsFinite)
                return Identity;

            double w = W;
            double x = X;
            double y = Y;
            double z = Z;

            Normalize(ref w, ref x, ref y, ref z);

            return new SimQuaternion(w, x, y, z);
        }

        public static SimQuaternion FromEulerDeg(double rollDeg, double pitchDeg, double yawDeg)
        {
            var q = Orientation.FromEuler(rollDeg, pitchDeg, yawDeg);
            return new SimQuaternion(q.qw, q.qx, q.qy, q.qz).Sanitized();
        }

        public Orientation ToOrientation()
        {
            var safe = Sanitized();
            return new Orientation(safe.W, safe.X, safe.Y, safe.Z, fromQuaternion: true);
        }

        public SimVector3 Rotate(SimVector3 v)
        {
            var safe = Sanitized();

            double vx = v.X;
            double vy = v.Y;
            double vz = v.Z;

            double tx = 2.0 * (safe.Y * vz - safe.Z * vy);
            double ty = 2.0 * (safe.Z * vx - safe.X * vz);
            double tz = 2.0 * (safe.X * vy - safe.Y * vx);

            double cx = safe.Y * tz - safe.Z * ty;
            double cy = safe.Z * tx - safe.X * tz;
            double cz = safe.X * ty - safe.Y * tx;

            return new SimVector3(
                vx + safe.W * tx + cx,
                vy + safe.W * ty + cy,
                vz + safe.W * tz + cz
            ).Sanitized();
        }

        public SimQuaternion Combine(SimQuaternion other)
        {
            var a = Sanitized();
            var b = other.Sanitized();

            return new SimQuaternion(
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W
            ).Sanitized();
        }

        private static void Normalize(ref double w, ref double x, ref double y, ref double z)
        {
            double norm = Math.Sqrt(w * w + x * x + y * y + z * z);

            if (!double.IsFinite(norm) || norm < 1e-12)
            {
                w = 1.0;
                x = 0.0;
                y = 0.0;
                z = 0.0;
                return;
            }

            w /= norm;
            x /= norm;
            y /= norm;
            z /= norm;
        }
    }
}
