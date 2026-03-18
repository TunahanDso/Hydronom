// File: Hydronom.Core/MathTools/MatrixMath.cs
//
// Not:
// - Vec3 ve Orientation: Hydronom.Core.Domain altında tanımlıdır.
// - Bu sınıf AutoDiscovery dahil tüm modüller tarafından genel math helper olarak kullanılabilir.

using System;
using Hydronom.Core.Domain; // Vec3, Orientation burada tanımlı

namespace Hydronom.Core.MathTools
{
    /// <summary>
    /// 6-DoF Navigasyon ve Kontrol için yüksek performanslı matematik kütüphanesi.
    /// Euler açıları (tekne) ve quaternion (roket/denizaltı) desteği içerir.
    /// Garbage Collection baskısını azaltmak için Vec3 ve hafif tipler kullanılır.
    /// </summary>
    public static class MatrixMath
    {
        // ---------------------------
        // Vektör İşlemleri (Vec3 Entegrasyonu)
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
        /// İki vektör arasındaki açıyı (radyan) hesaplar.
        /// </summary>
        public static double AngleBetween(Vec3 a, Vec3 b)
        {
            double dot = Dot(a, b);
            double len = Magnitude(a) * Magnitude(b);
            if (len < 1e-9) return 0.0;
            return Math.Acos(Math.Clamp(dot / len, -1.0, 1.0));
        }

        // ---------------------------
        // Matris İşlemleri (Rotation)
        // ---------------------------

        /// <summary>
        /// Euler açılarından (derece) dönüşüm matrisi (Rotation Matrix) oluşturur.
        /// Sıralama: Yaw -> Pitch -> Roll (Intrinsic ZYX).
        /// UYARI: Pitch +/- 90 derecede gimbal lock riski vardır.
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
        /// Bir vektörü (body frame), dünya koordinatlarına (world frame) döndürür.
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
        /// Bir vektörü, dünya koordinatlarından araç koordinatlarına (body frame) döndürür.
        /// v_body = Rᵀ * v_world (ortogonal matrislerde inverse = transpose).
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
        /// Body-frame bir kuvvet vektörünü (Fx,Fy,Fz), aracın anlık
        /// oryantasyonuna göre dünya eksenine dönüştürür.
        /// Not: Burada Orientation içindeki kuaterniyon tabanlı dönüşüm kullanılır.
        /// </summary>
        public static Vec3 BodyToWorld(Vec3 body, Orientation orientation)
        {
            // Tek gerçek kaynak: Orientation.BodyToWorld
            return orientation.BodyToWorld(body);
        }

        /// <summary>
        /// Manifesto uyumlu helper:
        /// Dünya eksenindeki bir kuvveti, aracın anlık oryantasyonuna göre
        /// body-frame’e projeksiyon eder.
        /// Not: Burada Orientation içindeki kuaterniyon tabanlı dönüşüm kullanılır.
        /// </summary>
        public static Vec3 WorldToBody(Vec3 world, Orientation orientation)
        {
            // Tek gerçek kaynak: Orientation.WorldToBody
            return orientation.WorldToBody(world);
        }

        // ---------------------------
        // Quaternion Desteği
        // ---------------------------

        /// <summary>
        /// Euler açılarını (derece) quaternion'a çevirir.
        /// Gimbal lock olmadan 3B rotasyon sağlar.
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
        /// Quaternion kullanarak vektörü döndürür.
        /// Genelde matris çarpımından daha hızlı ve daha stabildir.
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
        // Yardımcı Fonksiyonlar
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
        /// Lineer interpolasyon (yumuşak geçişler).
        /// </summary>
        public static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * Clamp(amount, 0, 1);
        }
    }

    /// <summary>
    /// 4-elemanlı vektör (özellikle quaternion için).
    /// </summary>
    public record Vec4(double W, double X, double Y, double Z);
}
