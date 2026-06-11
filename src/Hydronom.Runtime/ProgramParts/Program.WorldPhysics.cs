using System;
using Hydronom.Core.Physics;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.World;

partial class Program
{
    /*
     * VP9A World Physics entegrasyon noktası.
     *
     * Synthetic physics / planner / control tarafının aynı world-model
     * örneğinden beslenebilmesi için merkezi giriş noktası sağlar.
     */
    private static readonly Lazy<WorldModel> DefaultWorldPhysicsModel = new(
        () => WorldModel.DefaultPool());

    private static WorldModel CreateDefaultWorldPhysicsModel()
    {
        return DefaultWorldPhysicsModel.Value;
    }

    private static WorldModel CreateWorldPhysicsModel(WorldOptions world)
    {
        return WorldModel.DefaultPool(
            floorZ: world.FloorZ,
            surfaceZ: world.SurfaceZ,
            gravityMps2: world.GravityMps2,
            currentWorld: world.CurrentWorld,
            visibilityMeters: world.VisibilityMeters,
            waterDensityKgM3: world.WaterDensityKgM3,
            airDensityKgM3: world.AirDensityKgM3) with
        {
            Id = world.Id,
            Name = world.Name
        };
    }

    private static VehiclePhysicalProfile CreateDefaultVehiclePhysicalProfile()
    {
        return VehiclePhysicalProfile.Unknown;
    }

    private static WorldPhysicsEngine CreateWorldPhysicsEngine()
    {
        return new WorldPhysicsEngine();
    }
}