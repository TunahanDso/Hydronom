using System;

namespace Hydronom.Core.Domain
{
    public readonly record struct Vec3(double X, double Y, double Z)
    {
        public static Vec3 Zero => new(0, 0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalize()
        {
            var len = Length;
            return len < 1e-9 ? Zero : new(X / len, Y / len, Z / len);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        // Skaler çarpma (sağdan ve soldan)
        public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
        public static Vec3 operator *(double k, Vec3 a) => new(a.X * k, a.Y * k, a.Z * k);

        // Skaler bölme
        public static Vec3 operator /(Vec3 a, double k) => (Math.Abs(k) < 1e-12) ? Zero : new(a.X / k, a.Y / k, a.Z / k);

        // Noktasal ve vektörel çarpım
        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public double Dot(in Vec3 b) => Dot(this, b);

        public static Vec3 Cross(Vec3 a, Vec3 b)
            => new(a.Y * b.Z - a.Z * b.Y,
                   a.Z * b.X - a.X * b.Z,
                   a.X * b.Y - a.Y * b.X);
    }
}
