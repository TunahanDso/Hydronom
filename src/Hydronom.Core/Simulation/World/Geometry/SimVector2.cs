using System;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ± iÃ§in 2D vektÃ¶r modeli.
    ///
    /// Bu model Ã¶zellikle:
    /// - 2D harita katmanlarÄ±
    /// - no-go zone poligonlarÄ±
    /// - gÃ¶rev alanlarÄ±
    /// - Ops 2D mission control gÃ¶rÃ¼nÃ¼mÃ¼
    /// iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimVector2(
        double X,
        double Y
    )
    {
        public static SimVector2 Zero => new(0.0, 0.0);
        public static SimVector2 UnitX => new(1.0, 0.0);
        public static SimVector2 UnitY => new(0.0, 1.0);

        public bool IsFinite =>
            double.IsFinite(X) &&
            double.IsFinite(Y);

        public double Length
        {
            get
            {
                double s = X * X + Y * Y;
                return s <= 0.0 || !double.IsFinite(s) ? 0.0 : Math.Sqrt(s);
            }
        }

        public double LengthSquared
        {
            get
            {
                double s = X * X + Y * Y;
                return double.IsFinite(s) ? s : 0.0;
            }
        }

        public SimVector2 Sanitized()
        {
            return new SimVector2(
                Sanitize(X),
                Sanitize(Y)
            );
        }

        public SimVector2 Normalized()
        {
            double len = Length;
            if (len < 1e-12)
                return Zero;

            return new SimVector2(X / len, Y / len);
        }

        public double DistanceTo(SimVector2 other)
        {
            return (this - other).Length;
        }

        public double Dot(SimVector2 other)
        {
            double value = X * other.X + Y * other.Y;
            return double.IsFinite(value) ? value : 0.0;
        }

        public static SimVector2 Min(SimVector2 a, SimVector2 b)
        {
            return new SimVector2(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y)
            );
        }

        public static SimVector2 Max(SimVector2 a, SimVector2 b)
        {
            return new SimVector2(
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y)
            );
        }

        public static SimVector2 operator +(SimVector2 a, SimVector2 b)
        {
            return new SimVector2(a.X + b.X, a.Y + b.Y).Sanitized();
        }

        public static SimVector2 operator -(SimVector2 a, SimVector2 b)
        {
            return new SimVector2(a.X - b.X, a.Y - b.Y).Sanitized();
        }

        public static SimVector2 operator -(SimVector2 v)
        {
            return new SimVector2(-v.X, -v.Y).Sanitized();
        }

        public static SimVector2 operator *(SimVector2 v, double scalar)
        {
            return new SimVector2(v.X * scalar, v.Y * scalar).Sanitized();
        }

        public static SimVector2 operator *(double scalar, SimVector2 v)
        {
            return v * scalar;
        }

        public static SimVector2 operator /(SimVector2 v, double scalar)
        {
            if (!double.IsFinite(scalar) || Math.Abs(scalar) < 1e-12)
                return Zero;

            return new SimVector2(v.X / scalar, v.Y / scalar).Sanitized();
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }

        private static double Sanitize(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }
    }
}
