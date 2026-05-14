using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// WorldModel içinde fizik ve sensör davranışını etkileyen ortam bölgesi.
    /// Örnek: havuz su hacmi, havuz üstü hava, taban, kara alanı, akıntılı bölge.
    /// </summary>
    public sealed record EnvironmentZone
    {
        public string Id { get; init; } = "environment_zone";
        public string Name { get; init; } = "Environment Zone";
        public EnvironmentMedium Medium { get; init; } = EnvironmentMedium.Unknown;
        public EnvironmentBounds Bounds { get; init; } = EnvironmentBounds.Infinite;

        /// <summary>[kg/m³] Su yoğunluğu. Tatlı su için yaklaşık 997, deniz suyu için yaklaşık 1025.</summary>
        public double WaterDensityKgM3 { get; init; } = 997.0;

        /// <summary>[kg/m³] Hava yoğunluğu. Deniz seviyesinde yaklaşık 1.225.</summary>
        public double AirDensityKgM3 { get; init; } = 1.225;

        /// <summary>[m/s²] Yerçekimi ivmesi.</summary>
        public double GravityMps2 { get; init; } = 9.80665;

        /// <summary>[m/s] Dünya frame içinde su akıntısı.</summary>
        public Vec3 CurrentWorld { get; init; } = Vec3.Zero;

        /// <summary>[m/s] Dünya frame içinde rüzgar.</summary>
        public Vec3 WindWorld { get; init; } = Vec3.Zero;

        /// <summary>[m] Su yüzeyi yüksekliği. Z yukarı kabulünde genelde 0.</summary>
        public double SurfaceZ { get; init; } = 0.0;

        /// <summary>[m] Taban / zemin yüksekliği. Havuz için örnek: -2.0.</summary>
        public double FloorZ { get; init; } = -2.0;

        /// <summary>[m] Görüş mesafesi. Kamera/sensör simülasyonu için kullanılabilir.</summary>
        public double VisibilityMeters { get; init; } = 10.0;

        /// <summary>Zemin sürtünmesi. Solid ortam ve temas modeli için kullanılır.</summary>
        public double FrictionCoefficient { get; init; } = 0.7;

        /// <summary>
        /// Aynı noktayı birden fazla zone kapsarsa yüksek öncelikli zone seçilir.
        /// Örnek: boru içi dar alan, genel su hacminden daha yüksek öncelikli olabilir.
        /// </summary>
        public int Priority { get; init; } = 0;

        public bool Contains(Vec3 position) => Bounds.Sanitized().Contains(position);

        public EnvironmentZone Sanitized()
        {
            return this with
            {
                Bounds = Bounds.Sanitized(),
                WaterDensityKgM3 = PositiveOr(WaterDensityKgM3, 997.0),
                AirDensityKgM3 = PositiveOr(AirDensityKgM3, 1.225),
                GravityMps2 = PositiveOr(GravityMps2, 9.80665),
                CurrentWorld = SanitizeVec(CurrentWorld),
                WindWorld = SanitizeVec(WindWorld),
                SurfaceZ = FiniteOr(SurfaceZ, 0.0),
                FloorZ = FiniteOr(FloorZ, -2.0),
                VisibilityMeters = PositiveOr(VisibilityMeters, 10.0),
                FrictionCoefficient = Clamp(FiniteOr(FrictionCoefficient, 0.7), 0.0, 5.0)
            };
        }

        private static Vec3 SanitizeVec(Vec3 v) =>
            new(FiniteOr(v.X, 0.0), FiniteOr(v.Y, 0.0), FiniteOr(v.Z, 0.0));

        private static double PositiveOr(double value, double fallback) =>
            double.IsFinite(value) && value > 0.0 ? value : fallback;

        private static double FiniteOr(double value, double fallback) =>
            double.IsFinite(value) ? value : fallback;

        private static double Clamp(double value, double min, double max) =>
            value < min ? min : value > max ? max : value;
    }
}
