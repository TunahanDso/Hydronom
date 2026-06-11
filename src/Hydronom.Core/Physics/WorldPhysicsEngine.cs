using Hydronom.Core.Domain;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.World;

namespace Hydronom.Core.Physics
{
    /// <summary>
    /// VP9A world physics engine iskeleti.
    /// Bu sınıf şimdilik runtime davranışına bağlanmaz; sonraki adımda synthetic physics hattı buraya alınacaktır.
    /// </summary>
    public sealed class WorldPhysicsEngine
    {
        public WorldPhysicsReport ComputeForces(
            WorldPhysicsSample sample,
            VehiclePhysicalProfile vehicle,
            Vec3 actuatorForceWorld,
            Vec3 actuatorTorqueBody,
            Vec3 relativeVelocityBody)
        {
            vehicle = (vehicle ?? VehiclePhysicalProfile.Unknown).Sanitized();

            var gravity = HydrostaticModel.ComputeGravityWorld(vehicle, sample.Environment);
            var buoyancy = HydrostaticModel.ComputeBuoyancyWorld(vehicle, sample.Environment);
            var dragBody = HydrodynamicModel.ComputeDragBody(vehicle, sample.Environment, relativeVelocityBody);

            /*
             * VP9A scaffold notu:
             * Drag body-frame olarak raporlanıyor. Runtime entegrasyonunda orientation üzerinden
             * world-frame'e çevrilerek toplam kuvvete eklenecek.
             * Şimdilik davranış bağlamamak için TotalForceWorld sadece world-frame kuvvetleri toplar.
             */
            var totalWorld = new Vec3(
                actuatorForceWorld.X + gravity.X + buoyancy.X,
                actuatorForceWorld.Y + gravity.Y + buoyancy.Y,
                actuatorForceWorld.Z + gravity.Z + buoyancy.Z);

            return new WorldPhysicsReport
            {
                Sample = sample,
                ActuatorForceWorld = actuatorForceWorld,
                GravityForceWorld = gravity,
                BuoyancyForceWorld = buoyancy,
                HydrodynamicDragForceBody = dragBody,
                TotalForceWorld = totalWorld,
                TotalTorqueBody = actuatorTorqueBody,
                HasFloorContact = ContactModel.IsBelowFloor(Vec3.Zero, sample.Environment),
                HasSurfaceContact = false
            };
        }
    }
}