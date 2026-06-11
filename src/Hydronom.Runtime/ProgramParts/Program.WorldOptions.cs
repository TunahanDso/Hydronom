using System;
using Hydronom.Core.Domain;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// VP9A world/environment ayarları.
    /// Z ekseni yukarıdır; sualtı genellikle negatif Z bölgesidir.
    /// </summary>
    private readonly record struct WorldOptions(
        string Id,
        string Name,
        double SurfaceZ,
        double FloorZ,
        double GravityMps2,
        Vec3 CurrentWorld,
        double VisibilityMeters,
        double WaterDensityKgM3,
        double AirDensityKgM3
    );

    /// <summary>
    /// VP9A world/environment parametrelerini config üzerinden okur.
    ///
    /// World:SurfaceZ varsayılan 0.0 kabul edilir.
    /// World:FloorZ varsayılan -2.0 sadece güvenli fallback'tir.
    /// Kalıcı gerçek değer scenario/world metadata üzerinden gelmelidir.
    /// </summary>
    private static WorldOptions ReadWorldOptions(IConfiguration config)
    {
        string worldId = ReadString(config, "World:Id", "default_pool");
        string worldName = ReadString(config, "World:Name", "Default Pool World");

        double surfaceZ = ReadDouble(config, "World:SurfaceZ", 0.0);
        double floorZ = ReadDouble(config, "World:FloorZ", -2.0);
        double gravityMps2 = ReadDouble(config, "World:GravityMps2", 9.80665);

        var currentWorld = new Vec3(
            ReadDouble(config, "World:Current:X", ReadDouble(config, "World:CurrentWorld:X", 0.0)),
            ReadDouble(config, "World:Current:Y", ReadDouble(config, "World:CurrentWorld:Y", 0.0)),
            ReadDouble(config, "World:Current:Z", ReadDouble(config, "World:CurrentWorld:Z", 0.0))
        );

        double visibilityMeters = ReadDouble(config, "World:VisibilityMeters", 8.0);
        double waterDensityKgM3 = ReadDouble(config, "World:WaterDensityKgM3", 997.0);
        double airDensityKgM3 = ReadDouble(config, "World:AirDensityKgM3", 1.225);

        if (!double.IsFinite(surfaceZ))
            surfaceZ = 0.0;

        if (!double.IsFinite(floorZ))
            floorZ = -2.0;

        if (floorZ > surfaceZ)
        {
            Console.WriteLine(
                $"[CFG] World warning -> FloorZ={floorZ:F2} SurfaceZ={surfaceZ:F2}; " +
                "FloorZ SurfaceZ üstünde olamaz, fallback FloorZ=-2.00 uygulanıyor.");

            floorZ = -2.0;
        }

        if (!double.IsFinite(gravityMps2) || gravityMps2 <= 0.0)
            gravityMps2 = 9.80665;

        if (!double.IsFinite(currentWorld.X) ||
            !double.IsFinite(currentWorld.Y) ||
            !double.IsFinite(currentWorld.Z))
        {
            currentWorld = Vec3.Zero;
        }

        if (!double.IsFinite(visibilityMeters) || visibilityMeters <= 0.0)
            visibilityMeters = 8.0;

        if (!double.IsFinite(waterDensityKgM3) || waterDensityKgM3 <= 0.0)
            waterDensityKgM3 = 997.0;

        if (!double.IsFinite(airDensityKgM3) || airDensityKgM3 <= 0.0)
            airDensityKgM3 = 1.225;

        return new WorldOptions(
            Id: string.IsNullOrWhiteSpace(worldId) ? "default_pool" : worldId.Trim(),
            Name: string.IsNullOrWhiteSpace(worldName) ? "Default Pool World" : worldName.Trim(),
            SurfaceZ: surfaceZ,
            FloorZ: floorZ,
            GravityMps2: gravityMps2,
            CurrentWorld: currentWorld,
            VisibilityMeters: visibilityMeters,
            WaterDensityKgM3: waterDensityKgM3,
            AirDensityKgM3: airDensityKgM3);
    }

    private static void PrintWorldBootstrapSummary(WorldOptions world)
    {
        Console.WriteLine(
            $"[CFG] World -> Id={world.Id} Name={world.Name} " +
            $"SurfaceZ={world.SurfaceZ:F2} FloorZ={world.FloorZ:F2} " +
            $"Gravity={world.GravityMps2:F5}m/s2 " +
            $"Current=({world.CurrentWorld.X:F2},{world.CurrentWorld.Y:F2},{world.CurrentWorld.Z:F2})m/s " +
            $"Visibility={world.VisibilityMeters:F1}m " +
            $"RhoWater={world.WaterDensityKgM3:F1}kg/m3 RhoAir={world.AirDensityKgM3:F3}kg/m3");
    }
}