using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// WorldModel tarafından fizik motoru için çözümlenmiş tekil örnek.
    /// </summary>
    public sealed record WorldPhysicsSample
    {
        public EnvironmentSample Environment { get; init; } = EnvironmentSample.DefaultWaterPool();

        public double DepthMeters { get; init; } = 0.0;
        public double DistanceToSurfaceMeters { get; init; } = 0.0;
        public double DistanceToFloorMeters { get; init; } = 0.0;

        public Vec3 FlowVelocityWorld { get; init; } = Vec3.Zero;
        public double VisibilityMeters { get; init; } = 10.0;

        public bool IsInWater => Environment.IsWater;
        public bool IsNearSurface { get; init; }
        public bool IsNearFloor { get; init; }
    }
}