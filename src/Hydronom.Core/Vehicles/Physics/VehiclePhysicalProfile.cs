using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Physics
{
    /// <summary>
    /// Aracın fiziksel gövde profilidir.
    ///
    /// Bu model:
    /// - Boyut
    /// - Kütle
    /// - Yaklaşık çarpışma hacmi
    /// - Yüzdürme
    /// - Hidrodinamik direnç
    /// bilgilerini bir araya getirir.
    /// </summary>
    public sealed record VehiclePhysicalProfile(
        double LengthM,
        double WidthM,
        double HeightM,
        double CollisionRadiusM,
        VehicleMassProperties Mass,
        VehicleBuoyancyProfile Buoyancy,
        VehicleHydrodynamicProfile Hydrodynamics,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehiclePhysicalProfile Unknown { get; } = new(
            LengthM: 0.0,
            WidthM: 0.0,
            HeightM: 0.0,
            CollisionRadiusM: 0.0,
            Mass: VehicleMassProperties.Unknown,
            Buoyancy: VehicleBuoyancyProfile.Disabled,
            Hydrodynamics: VehicleHydrodynamicProfile.Disabled,
            Tags: new Dictionary<string, string>());

        public Vec3 DimensionsM => new(LengthM, WidthM, HeightM);

        public bool IsValid =>
            LengthM > 0.0 &&
            WidthM > 0.0 &&
            HeightM > 0.0 &&
            Mass is not null &&
            Mass.IsValid;

        public VehiclePhysicalProfile Sanitized()
        {
            return this with
            {
                LengthM = SafePositive(LengthM),
                WidthM = SafePositive(WidthM),
                HeightM = SafePositive(HeightM),
                CollisionRadiusM = SafePositive(CollisionRadiusM),
                Mass = (Mass ?? VehicleMassProperties.Unknown).Sanitized(),
                Buoyancy = (Buoyancy ?? VehicleBuoyancyProfile.Disabled).Sanitized(),
                Hydrodynamics = (Hydrodynamics ?? VehicleHydrodynamicProfile.Disabled).Sanitized(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }
    }
}