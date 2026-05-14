using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// Aracın mevcut pozisyonu için çözümlenmiş ortam bilgisi.
    /// Fizik motoru doğrudan bu örneği kullanır.
    /// </summary>
    public sealed record EnvironmentSample
    {
        public EnvironmentMedium Medium { get; init; } = EnvironmentMedium.Unknown;
        public string ZoneId { get; init; } = "none";
        public string ZoneName { get; init; } = "No Environment Zone";

        public double WaterDensityKgM3 { get; init; } = 997.0;
        public double AirDensityKgM3 { get; init; } = 1.225;
        public double GravityMps2 { get; init; } = 9.80665;

        public Vec3 CurrentWorld { get; init; } = Vec3.Zero;
        public Vec3 WindWorld { get; init; } = Vec3.Zero;

        public double SurfaceZ { get; init; } = 0.0;
        public double FloorZ { get; init; } = -2.0;
        public double VisibilityMeters { get; init; } = 10.0;
        public double FrictionCoefficient { get; init; } = 0.7;

        public bool IsWater => Medium == EnvironmentMedium.Water || Medium == EnvironmentMedium.Mixed;
        public bool IsAir => Medium == EnvironmentMedium.Air || Medium == EnvironmentMedium.Mixed;
        public bool IsSolid => Medium == EnvironmentMedium.Solid;

        public static EnvironmentSample DefaultWaterPool(double floorZ = -2.0, double surfaceZ = 0.0) =>
            new()
            {
                Medium = EnvironmentMedium.Water,
                ZoneId = "default_water_pool",
                ZoneName = "Default Water Pool",
                WaterDensityKgM3 = 997.0,
                GravityMps2 = 9.80665,
                SurfaceZ = surfaceZ,
                FloorZ = floorZ,
                VisibilityMeters = 8.0
            };

        public static EnvironmentSample DefaultAir() =>
            new()
            {
                Medium = EnvironmentMedium.Air,
                ZoneId = "default_air",
                ZoneName = "Default Air",
                AirDensityKgM3 = 1.225,
                GravityMps2 = 9.80665
            };

        public string CompactInfo()
        {
            return $"{Medium} zone={ZoneId} surfZ={SurfaceZ:F2} floorZ={FloorZ:F2} cur=({CurrentWorld.X:F2},{CurrentWorld.Y:F2},{CurrentWorld.Z:F2})";
        }
    }
}
