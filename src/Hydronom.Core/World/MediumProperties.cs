using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// Bir ortam katmanının fiziksel özellikleri.
    /// VP9A iskeleti: davranışa henüz bağlanmamıştır.
    /// </summary>
    public sealed record MediumProperties
    {
        public EnvironmentMedium Medium { get; init; } = EnvironmentMedium.Unknown;

        public double DensityKgM3 { get; init; } = 0.0;
        public double DynamicViscosityPaS { get; init; } = 0.0;

        public double LinearDragMultiplier { get; init; } = 1.0;
        public double QuadraticDragMultiplier { get; init; } = 1.0;

        public Vec3 FlowVelocityWorld { get; init; } = Vec3.Zero;

        public double ReferencePressurePa { get; init; } = 101_325.0;
        public double PressureReferenceZ { get; init; } = 0.0;

        public static MediumProperties Air() => new()
        {
            Medium = EnvironmentMedium.Air,
            DensityKgM3 = 1.225,
            DynamicViscosityPaS = 0.0000181,
            LinearDragMultiplier = 0.15,
            QuadraticDragMultiplier = 0.20
        };

        public static MediumProperties Water() => new()
        {
            Medium = EnvironmentMedium.Water,
            DensityKgM3 = 997.0,
            DynamicViscosityPaS = 0.00089,
            LinearDragMultiplier = 1.0,
            QuadraticDragMultiplier = 1.0
        };

        public static MediumProperties Seabed() => new()
        {
            Medium = EnvironmentMedium.Solid,
            DensityKgM3 = 1_800.0,
            LinearDragMultiplier = 4.0,
            QuadraticDragMultiplier = 4.0
        };
    }
}