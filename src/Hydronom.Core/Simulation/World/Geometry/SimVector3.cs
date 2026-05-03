using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ± iÃ§in 3D vektÃ¶r modeli.
    ///
    /// Bu model:
    /// - 3D engeller
    /// - fiziksel dÃ¼nya nesneleri
    /// - Ops 3D tactical view
    /// - sensÃ¶r gÃ¶rÃ¼ÅŸ hacimleri
    /// - sonar/lidar/kamera algÄ± uzayÄ±
    /// iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimVector3(
        double X,
        double Y,
        double Z
    )
    {
        public static SimVector3 Zero => new(0.0, 0.0, 0.0);
        public static SimVector3 UnitX => new(1.0, 0.0, 0.0);
        public static SimVector3 UnitY => new(0.0, 1.0, 0.0);
        public static SimVector3 UnitZ => new(0.0, 0.0, 1.0);

        public bool IsFinite =>
            double.IsFinite(X) &&
            double.IsFinite(Y) &&
            double.IsFinite(Z);

        public double Length
        {
            get
            {
                double s = X * X + Y * Y + Z * Z;
                return s <= 0.0 || !double.IsFinite(s) ? 0.0 : Math.Sqrt(s);
            }
        }

        public double LengthSquared
        {
            get
            {
                double s = X * X + Y * Y + Z * Z;
                return double.IsFinite(s) ? s : 0.0;
            }
        }

        public SimVector2 Xy => new(X, Y);

        public SimVector3 Sanitized()
        {
            return new SimVector3(
                Sanitize(X),
                Sanitize(Y),
                Sanitize(Z)
            );
        }

        public SimVector3 Normalized()
        {
            double len = Length;
            if (len < 1e-12)
                return Zero;

            return new SimVector3(X / len, Y / len, Z / len);
        }

        public double DistanceTo(SimVector3 other)
        {
            return (this - other).Length;
        }

        public double Dot(SimVector3 other)
        {
            double value = X * other.X + Y * other.Y + Z * other.Z;
            return double.IsFinite(value) ? value : 0.0;
        }

        public SimVector3 Cross(SimVector3 other)
        {
            return new SimVector3(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X
            ).Sanitized();
        }

        public Vec3 ToDomainVec3()
        {
            return new Vec3(X, Y, Z);
        }

        public static SimVector3 FromDomainVec3(Vec3 value)
        {
            return new SimVector3(value.X, value.Y, value.Z).Sanitized();
        }

        public static SimVector3 Min(SimVector3 a, SimVector3 b)
        {
            return new SimVector3(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Min(a.Z, b.Z)
            );
        }

        public static SimVector3 Max(SimVector3 a, SimVector3 b)
        {
            return new SimVector3(
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y),
                Math.Max(a.Z, b.Z)
            );
        }

        public static SimVector3 operator +(SimVector3 a, SimVector3 b)
        {
            return new SimVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z).Sanitized();
        }

        public static SimVector3 operator -(SimVector3 a, SimVector3 b)
        {
            return new SimVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z).Sanitized();
        }

        public static SimVector3 operator -(SimVector3 v)
        {
            return new SimVector3(-v.X, -v.Y, -v.Z).Sanitized();
        }

        public static SimVector3 operator *(SimVector3 v, double scalar)
        {
            return new SimVector3(v.X * scalar, v.Y * scalar, v.Z * scalar).Sanitized();
        }

        public static SimVector3 operator *(double scalar, SimVector3 v)
        {
            return v * scalar;
        }

        public static SimVector3 operator /(SimVector3 v, double scalar)
        {
            if (!double.IsFinite(scalar) || Math.Abs(scalar) < 1e-12)
                return Zero;

            return new SimVector3(v.X / scalar, v.Y / scalar, v.Z / scalar).Sanitized();
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Z:F2})";
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}
