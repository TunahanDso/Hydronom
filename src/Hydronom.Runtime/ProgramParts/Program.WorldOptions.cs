using System;
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
        double GravityMps2
    );

    /// <summary>
    /// VP9A world/environment parametrelerini config üzerinden okur.
    ///
    /// World:SurfaceZ varsayılan 0.0 kabul edilir.
    /// World:FloorZ varsayılan -2.0 sadece güvenli fallback'tir.
    /// Kalıcı gerçek değer sonraki adımda scenario/world config üzerinden gelecektir.
    /// </summary>
    private static WorldOptions ReadWorldOptions(IConfiguration config)
    {
        string worldId = ReadString(config, "World:Id", "default_pool");
        string worldName = ReadString(config, "World:Name", "Default Pool World");

        double surfaceZ = ReadDouble(config, "World:SurfaceZ", 0.0);
        double floorZ = ReadDouble(config, "World:FloorZ", -2.0);
        double gravityMps2 = ReadDouble(config, "World:GravityMps2", 9.80665);

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

        return new WorldOptions(
            Id: string.IsNullOrWhiteSpace(worldId) ? "default_pool" : worldId.Trim(),
            Name: string.IsNullOrWhiteSpace(worldName) ? "Default Pool World" : worldName.Trim(),
            SurfaceZ: surfaceZ,
            FloorZ: floorZ,
            GravityMps2: gravityMps2);
    }

    private static void PrintWorldBootstrapSummary(WorldOptions world)
    {
        Console.WriteLine(
            $"[CFG] World -> Id={world.Id} Name={world.Name} " +
            $"SurfaceZ={world.SurfaceZ:F2} FloorZ={world.FloorZ:F2} " +
            $"Gravity={world.GravityMps2:F5}m/s2");
    }
}