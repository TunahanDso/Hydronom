// File: Hydronom.Core/MathTools/MatrixMath.cs
//
// Not:
// - Vec3 ve Orientation: Hydronom.Core.Domain altÄ±nda tanÄ±mlÄ±dÄ±r.
// - Bu sÄ±nÄ±f AutoDiscovery dahil tÃ¼m modÃ¼ller tarafÄ±ndan genel math helper olarak kullanÄ±labilir.

using System;
using Hydronom.Core.Domain; // Vec3, Orientation burada tanÄ±mlÄ±

namespace Hydronom.Core.MathTools
{
    /// <summary>
    /// 6-DoF Navigasyon ve Kontrol iÃ§in yÃ¼ksek performanslÄ± matematik kÃ¼tÃ¼phanesi.
    /// Euler aÃ§Ä±larÄ± (tekne) ve quaternion (roket/denizaltÄ±) desteÄŸi iÃ§erir.
    /// Garbage Collection baskÄ±sÄ±nÄ± azaltmak iÃ§in Vec3 ve hafif tipler kullanÄ±lÄ±r.
    /// </summary>
    public static class MatrixMath
    {
        // ---------------------------
        // VektÃ¶r Ä°ÅŸlemleri (Vec3 Entegrasyonu)
        // ---------------------------

        public static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        public static double Magnitude(Vec3 v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }

        public static Vec3 Normalize(Vec3 v)
        {
            double len = Magnitude(v);
            if (len < 1e-9) return Vec3.Zero;
            return new Vec3(v.X / len, v.Y / len, v.Z / len);
        }

        /// <summary>
        /// Ä°ki vektÃ¶r arasÄ±ndaki aÃ§Ä±yÄ± (radyan) hesaplar.
        /// </summary>
        public static double AngleBetween(Vec3 a, Vec3 b)
        {
            double dot = Dot(a, b);
            double len = Magnitude(a) * Magnitude(b);
            if (len < 1e-9) return 0.0;
            return Math.Acos(Math.Clamp(dot / len, -1.0, 1.0));
        }

        // ---------------------------
        // Matris Ä°ÅŸlemleri (Rotation)
        // ---------------------------

        /// <summary>
        /// Euler aÃ§Ä±larÄ±ndan (derece) dÃ¶nÃ¼ÅŸÃ¼m matrisi (Rotation Matrix) oluÅŸturur.
        /// SÄ±ralama: Yaw -> Pitch -> Roll (Intrinsic ZYX).
        /// UYARI: Pitch +/- 90 derecede gimbal lock riski vardÄ±r.
        /// </summary>
        public static double[,] RotationMatrixRPY(double rollDeg, double pitchDeg, double yawDeg)
        {
            double r = Deg2Rad(rollDeg);
            double p = Deg2Rad(pitchDeg);
            double y = Deg2Rad(yawDeg);

            double cr = Math.Cos(r), sr = Math.Sin(r);
            double cp = Math.Cos(p), sp = Math.Sin(p);
            double cy = Math.Cos(y), sy = Math.Sin(y);

            // 3x3 matris (row-major)
            return new double[3, 3]
            {
                { cy * cp,  cy * sp * sr - sy * cr,  cy * sp * cr + sy * sr },
                { sy * cp,  sy * sp * sr + cy * cr,  sy * sp * cr - cy * sr },
                { -sp,      cp * sr,                cp * cr                 }
            };
        }

        /// <summary>
        /// Bir vektÃ¶rÃ¼ (body frame), dÃ¼nya koordinatlarÄ±na (world frame) dÃ¶ndÃ¼rÃ¼r.
        /// v_world = R * v_body
        /// </summary>
        public static Vec3 Transform(double[,] R, Vec3 v)
        {
            return new Vec3(
                R[0, 0] * v.X + R[0, 1] * v.Y + R[0, 2] * v.Z,
                R[1, 0] * v.X + R[1, 1] * v.Y + R[1, 2] * v.Z,
                R[2, 0] * v.X + R[2, 1] * v.Y + R[2, 2] * v.Z
            );
        }

        /// <summary>
        /// Bir vektÃ¶rÃ¼, dÃ¼nya koordinatlarÄ±ndan araÃ§ koordinatlarÄ±na (body frame) dÃ¶ndÃ¼rÃ¼r.
        /// v_body = Ráµ€ * v_world (ortogonal matrislerde inverse = transpose).
        /// </summary>
        public static Vec3 InverseTransform(double[,] R, Vec3 v)
        {
            return new Vec3(
                R[0, 0] * v.X + R[1, 0] * v.Y + R[2, 0] * v.Z,
                R[0, 1] * v.X + R[1, 1] * v.Y + R[2, 1] * v.Z,
                R[0, 2] * v.X + R[1, 2] * v.Y + R[2, 2] * v.Z
            );
        }

        /// <summary>
        /// Manifesto uyumlu helper:
        /// Body-frame bir kuvvet vektÃ¶rÃ¼nÃ¼ (Fx,Fy,Fz), aracÄ±n anlÄ±k
        /// oryantasyonuna gÃ¶re dÃ¼nya eksenine dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
        /// Not: Burada Orientation iÃ§indeki kuaterniyon tabanlÄ± dÃ¶nÃ¼ÅŸÃ¼m kullanÄ±lÄ±r.
        /// </summary>
        public static Vec3 BodyToWorld(Vec3 body, Orientation orientation)
        {
            // Tek gerÃ§ek kaynak: Orientation.BodyToWorld
            return orientation.BodyToWorld(body);
        }

        /// <summary>
        /// Manifesto uyumlu helper:
        /// DÃ¼nya eksenindeki bir kuvveti, aracÄ±n anlÄ±k oryantasyonuna gÃ¶re
        /// body-frameâ€™e projeksiyon eder.
        /// Not: Burada Orientation iÃ§indeki kuaterniyon tabanlÄ± dÃ¶nÃ¼ÅŸÃ¼m kullanÄ±lÄ±r.
        /// </summary>
        public static Vec3 WorldToBody(Vec3 world, Orientation orientation)
        {
            // Tek gerÃ§ek kaynak: Orientation.WorldToBody
            return orientation.WorldToBody(world);
        }

        // ---------------------------
        // Quaternion DesteÄŸi
        // ---------------------------

        /// <summary>
        /// Euler aÃ§Ä±larÄ±nÄ± (derece) quaternion'a Ã§evirir.
        /// Gimbal lock olmadan 3B rotasyon saÄŸlar.
        /// </summary>
        public static Vec4 EulerToQuaternion(double rollDeg, double pitchDeg, double yawDeg)
        {
            double r = Deg2Rad(rollDeg) * 0.5;
            double p = Deg2Rad(pitchDeg) * 0.5;
            double y = Deg2Rad(yawDeg) * 0.5;

            double cr = Math.Cos(r), sr = Math.Sin(r);
            double cp = Math.Cos(p), sp = Math.Sin(p);
            double cy = Math.Cos(y), sy = Math.Sin(y);

            return new Vec4(
                W: cr * cp * cy + sr * sp * sy,
                X: sr * cp * cy - cr * sp * sy,
                Y: cr * sp * cy + sr * cp * sy,
                Z: cr * cp * sy - sr * sp * cy
            );
        }

        /// <summary>
        /// Quaternion kullanarak vektÃ¶rÃ¼ dÃ¶ndÃ¼rÃ¼r.
        /// Genelde matris Ã§arpÄ±mÄ±ndan daha hÄ±zlÄ± ve daha stabildir.
        /// </summary>
        public static Vec3 RotateByQuaternion(Vec3 v, Vec4 q)
        {
            double qx = q.X, qy = q.Y, qz = q.Z, qw = q.W;

            // t = 2 * cross(q.xyz, v)
            double tx = 2.0 * (qy * v.Z - qz * v.Y);
            double ty = 2.0 * (qz * v.X - qx * v.Z);
            double tz = 2.0 * (qx * v.Y - qy * v.X);

            // v' = v + q.w * t + cross(q.xyz, t)
            return new Vec3(
                v.X + qw * tx + (qy * tz - qz * ty),
                v.Y + qw * ty + (qz * tx - qx * tz),
                v.Z + qw * tz + (qx * ty - qy * tx)
            );
        }

        // ---------------------------
        // YardÄ±mcÄ± Fonksiyonlar
        // ---------------------------

        public static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
        public static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;

        public static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        /// <summary>
        /// Lineer interpolasyon (yumuÅŸak geÃ§iÅŸler).
        /// </summary>
        public static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * Clamp(amount, 0, 1);
        }
    }

    /// <summary>
    /// 4-elemanlÄ± vektÃ¶r (Ã¶zellikle quaternion iÃ§in).
    /// </summary>
    public record Vec4(double W, double X, double Y, double Z);
}

