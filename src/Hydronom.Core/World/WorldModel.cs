using System.Collections.Generic;
using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// VP9A dünya modeli.
    ///
    /// Bu model doğrudan fizik motoru, planner ve runtime synthetic physics tarafından
    /// ortak kullanılacak çevre örneğini üretir.
    ///
    /// Not:
    /// - Z ekseni yukarıdır.
    /// - SurfaceZ varsayılan 0 kabul edilir.
    /// - Sualtı konumları genellikle negatif Z değerindedir.
    /// - FloorZ ileride scenario/world config üzerinden beslenecektir.
    /// </summary>
    public sealed record WorldModel
    {
        public string Id { get; init; } = "default_world";
        public string Name { get; init; } = "Default World";

        public double SurfaceZ { get; init; } = 0.0;
        public double FloorZ { get; init; } = -2.0;
        public double GravityMps2 { get; init; } = 9.80665;

        public IReadOnlyList<EnvironmentZone> Zones { get; init; } =
            new List<EnvironmentZone>();

        /*
         * EnvironmentLayer/MediumProperties dosyaları VP9A ileri katmanları için
         * korunuyor. Fakat mevcut kod tabanında EnvironmentZone + EnvironmentResolver
         * zaten surface/floor/current/visibility/floor bilgilerini taşıdığı için
         * ilk gerçek entegrasyon burada resolver üstünden yapılır.
         */
        public static WorldModel DefaultPool(double floorZ = -2.0, double surfaceZ = 0.0)
        {
            return new WorldModel
            {
                Id = "default_pool",
                Name = "Default Pool World",
                SurfaceZ = surfaceZ,
                FloorZ = floorZ,
                GravityMps2 = 9.80665,
                Zones = new List<EnvironmentZone>
                {
                    new()
                    {
                        Id = "default_air",
                        Name = "Default Air",
                        Medium = EnvironmentMedium.Air,
                        Bounds = new EnvironmentBounds(
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            surfaceZ,
                            double.PositiveInfinity),
                        SurfaceZ = surfaceZ,
                        FloorZ = floorZ,
                        GravityMps2 = 9.80665,
                        AirDensityKgM3 = 1.225,
                        Priority = 0
                    },
                    new()
                    {
                        Id = "default_water_pool",
                        Name = "Default Water Pool",
                        Medium = EnvironmentMedium.Water,
                        Bounds = new EnvironmentBounds(
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            floorZ,
                            surfaceZ),
                        SurfaceZ = surfaceZ,
                        FloorZ = floorZ,
                        GravityMps2 = 9.80665,
                        WaterDensityKgM3 = 997.0,
                        VisibilityMeters = 8.0,
                        Priority = 10
                    },
                    new()
                    {
                        Id = "default_floor",
                        Name = "Default Floor / Seabed",
                        Medium = EnvironmentMedium.Solid,
                        Bounds = new EnvironmentBounds(
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            double.NegativeInfinity,
                            double.PositiveInfinity,
                            double.NegativeInfinity,
                            floorZ),
                        SurfaceZ = surfaceZ,
                        FloorZ = floorZ,
                        GravityMps2 = 9.80665,
                        FrictionCoefficient = 0.7,
                        Priority = 20
                    }
                }
            };
        }

        public WorldPhysicsSample SampleAt(Vec3 position)
        {
            var resolver = new EnvironmentResolver(Zones);
            var environment = resolver.Resolve(position) with
            {
                SurfaceZ = SurfaceZ,
                FloorZ = FloorZ,
                GravityMps2 = GravityMps2
            };

            double depth = System.Math.Max(0.0, SurfaceZ - position.Z);
            double distanceToSurface = System.Math.Abs(SurfaceZ - position.Z);
            double distanceToFloor = System.Math.Abs(position.Z - FloorZ);

            return new WorldPhysicsSample
            {
                Environment = environment,
                DepthMeters = depth,
                DistanceToSurfaceMeters = distanceToSurface,
                DistanceToFloorMeters = distanceToFloor,
                FlowVelocityWorld = environment.CurrentWorld,
                VisibilityMeters = environment.VisibilityMeters,
                IsNearSurface = distanceToSurface < 0.20,
                IsNearFloor = distanceToFloor < 0.20
            };
        }
    }
}