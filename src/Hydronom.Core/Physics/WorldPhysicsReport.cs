using Hydronom.Core.Domain;
using Hydronom.Core.World;

namespace Hydronom.Core.Physics
{
    /// <summary>
    /// WorldPhysicsEngine hesap çıktısı.
    /// VP9A iskeleti: runtime telemetry/log entegrasyonu sonraki adımda yapılacaktır.
    /// </summary>
    public sealed record WorldPhysicsReport
    {
        public WorldPhysicsSample Sample { get; init; } = new();

        public Vec3 ActuatorForceWorld { get; init; } = Vec3.Zero;
        public Vec3 GravityForceWorld { get; init; } = Vec3.Zero;
        public Vec3 BuoyancyForceWorld { get; init; } = Vec3.Zero;
        public Vec3 HydrodynamicDragForceBody { get; init; } = Vec3.Zero;

        public Vec3 TotalForceWorld { get; init; } = Vec3.Zero;
        public Vec3 TotalTorqueBody { get; init; } = Vec3.Zero;

        public bool HasFloorContact { get; init; }
        public bool HasSurfaceContact { get; init; }
    }
}