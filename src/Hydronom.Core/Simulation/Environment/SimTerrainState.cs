namespace Hydronom.Core.Simulation.Environment
{
    /// <summary>
    /// Zemin/terrain durum modeli.
    ///
    /// Kara aracÄ±, paletli araÃ§, AGV, tarÄ±m aracÄ± ve karma ortam simÃ¼lasyonlarÄ±nda kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimTerrainState(
        bool Enabled,
        string TerrainId,
        string TerrainKind,
        double BaseElevationZ,
        double Roughness,
        double SlopeDeg,
        double Friction,
        double Softness,
        double ObstacleDensity
    )
    {
        public static SimTerrainState FlatGround => new(
            Enabled: true,
            TerrainId: "flat_ground",
            TerrainKind: "flat",
            BaseElevationZ: 0.0,
            Roughness: 0.05,
            SlopeDeg: 0.0,
            Friction: 0.8,
            Softness: 0.1,
            ObstacleDensity: 0.0
        );

        public SimTerrainState Sanitized()
        {
            return new SimTerrainState(
                Enabled: Enabled,
                TerrainId: Normalize(TerrainId, "terrain"),
                TerrainKind: Normalize(TerrainKind, "generic"),
                BaseElevationZ: Safe(BaseElevationZ),
                Roughness: Clamp01(Roughness),
                SlopeDeg: Clamp(SlopeDeg, -90.0, 90.0),
                Friction: Clamp01(Friction),
                Softness: Clamp01(Softness),
                ObstacleDensity: Clamp01(ObstacleDensity)
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
