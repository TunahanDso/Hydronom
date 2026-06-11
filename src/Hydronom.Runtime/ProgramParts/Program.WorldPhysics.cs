using Hydronom.Core.Physics;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.World;

partial class Program
{
    /*
     * VP9A World Physics entegrasyon noktasÄ±.
     *
     * Bu dosya ÅŸimdilik runtime davranÄ±ÅŸÄ±nÄ± tek baÅŸÄ±na deÄŸiÅŸtirmez.
     * Ama synthetic physics / planner / control tarafÄ±nÄ±n aynÄ± world-model
     * Ã¶rneÄŸinden beslenebilmesi iÃ§in merkezi giriÅŸ noktasÄ± saÄŸlar.
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
            surfaceZ: world.SurfaceZ) with
        {
            Id = world.Id,
            Name = world.Name,
            GravityMps2 = world.GravityMps2
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