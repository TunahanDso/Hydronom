using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.World;

namespace Hydronom.Core.Physics
{
    /// <summary>
    /// Basit hidrodinamik damping modeli.
    /// Mevcut VehicleHydrodynamicProfile içindeki linear/quadratic drag katsayılarını kullanır.
    /// </summary>
    public static class HydrodynamicModel
    {
        public static Vec3 ComputeDragBody(
            VehiclePhysicalProfile vehicle,
            EnvironmentSample environment,
            Vec3 relativeVelocityBody)
        {
            var hydrodynamics = vehicle.Hydrodynamics;

            if (hydrodynamics is null || !hydrodynamics.Enabled)
                return Vec3.Zero;

            double mediumScale = environment.IsWater
                ? 1.0
                : 0.05;

            var linear = hydrodynamics.LinearDrag;
            var quadratic = hydrodynamics.QuadraticDrag;

            double fx = ComputeAxisDrag(
                linear.X,
                quadratic.X,
                relativeVelocityBody.X,
                mediumScale);

            double fy = ComputeAxisDrag(
                linear.Y,
                quadratic.Y,
                relativeVelocityBody.Y,
                mediumScale);

            double fz = ComputeAxisDrag(
                linear.Z,
                quadratic.Z,
                relativeVelocityBody.Z,
                mediumScale);

            return new Vec3(fx, fy, fz);
        }

        private static double ComputeAxisDrag(
            double linearCoeff,
            double quadraticCoeff,
            double velocity,
            double mediumScale)
        {
            double linear = -linearCoeff * velocity;
            double quadratic = -quadraticCoeff * velocity * Math.Abs(velocity);

            return (linear + quadratic) * mediumScale;
        }
    }
}