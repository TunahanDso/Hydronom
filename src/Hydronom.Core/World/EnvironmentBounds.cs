using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// Axis-aligned basit 3B ortam sınırı.
    /// İlk v1 için yeterli; ileride poligon, mesh veya SDF tabanlı alanlara genişletilebilir.
    /// </summary>
    public readonly record struct EnvironmentBounds(
        double XMin,
        double XMax,
        double YMin,
        double YMax,
        double ZMin,
        double ZMax
    )
    {
        public static EnvironmentBounds Infinite => new(
            double.NegativeInfinity,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.PositiveInfinity
        );

        public bool Contains(Vec3 position)
        {
            return
                position.X >= XMin && position.X <= XMax &&
                position.Y >= YMin && position.Y <= YMax &&
                position.Z >= ZMin && position.Z <= ZMax;
        }

        public EnvironmentBounds Sanitized()
        {
            return new EnvironmentBounds(
                MinOrFallback(XMin, double.NegativeInfinity),
                MaxOrFallback(XMax, double.PositiveInfinity),
                MinOrFallback(YMin, double.NegativeInfinity),
                MaxOrFallback(YMax, double.PositiveInfinity),
                MinOrFallback(ZMin, double.NegativeInfinity),
                MaxOrFallback(ZMax, double.PositiveInfinity)
            );
        }

        private static double MinOrFallback(double value, double fallback)
        {
            return double.IsFinite(value) || double.IsInfinity(value) ? value : fallback;
        }

        private static double MaxOrFallback(double value, double fallback)
        {
            return double.IsFinite(value) || double.IsInfinity(value) ? value : fallback;
        }
    }
}
