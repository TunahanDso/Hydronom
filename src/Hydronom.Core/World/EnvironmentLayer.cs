using Hydronom.Core.Domain;

namespace Hydronom.Core.World
{
    /// <summary>
    /// Dünya modelindeki Z aralıklı ortam katmanı.
    /// Örn: air, water column, seabed/floor.
    /// </summary>
    public sealed record EnvironmentLayer
    {
        public string Id { get; init; } = "layer";
        public string Name { get; init; } = "Environment Layer";

        public double MinZ { get; init; } = double.NegativeInfinity;
        public double MaxZ { get; init; } = double.PositiveInfinity;

        public int Priority { get; init; } = 0;

        public MediumProperties Properties { get; init; } = new();

        public bool Contains(Vec3 position)
        {
            return position.Z >= MinZ && position.Z <= MaxZ;
        }
    }
}