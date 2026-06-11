using Hydronom.Core.Domain;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.World;

namespace Hydronom.Core.Physics
{
    /// <summary>
    /// Gravity + buoyancy hesapları.
    /// Z ekseni yukarıdır; gravity negatif Z, buoyancy pozitif Z yönündedir.
    /// </summary>
    public static class HydrostaticModel
    {
        public static Vec3 ComputeGravityWorld(
            VehiclePhysicalProfile vehicle,
            EnvironmentSample environment)
        {
            double massKg = vehicle.Mass?.MassKg ?? 0.0;

            if (massKg <= 0.0)
                return Vec3.Zero;

            return new Vec3(
                0.0,
                0.0,
                -massKg * environment.GravityMps2);
        }

        public static Vec3 ComputeBuoyancyWorld(
            VehiclePhysicalProfile vehicle,
            EnvironmentSample environment)
        {
            if (!environment.IsWater)
                return Vec3.Zero;

            var buoyancy = vehicle.Buoyancy;
            if (buoyancy is null || !buoyancy.Enabled)
                return Vec3.Zero;

            double displacedVolumeM3 = buoyancy.DisplacedVolumeM3;
            if (displacedVolumeM3 <= 0.0)
                return Vec3.Zero;

            double fluidDensityKgM3 = environment.WaterDensityKgM3 > 0.0
                ? environment.WaterDensityKgM3
                : buoyancy.FluidDensityKgM3;

            double forceZ = fluidDensityKgM3 * displacedVolumeM3 * environment.GravityMps2;

            return new Vec3(
                0.0,
                0.0,
                forceZ);
        }
    }
}