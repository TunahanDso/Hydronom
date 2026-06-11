using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Physics
{
    /// <summary>
    /// Su altı ve su üstü araçlar için yüzdürme profilidir.
    ///
    /// Z-up koordinat sisteminde:
    /// - Su yüzeyi genelde Z = 0
    /// - Su altı negatif Z
    /// - Derinlik pozitif bir operasyonel büyüklük olarak ayrıca yorumlanabilir.
    /// </summary>
    public sealed record VehicleBuoyancyProfile(
        bool Enabled,
        double DisplacedVolumeM3,
        Vec3 CenterOfBuoyancyM,
        double FluidDensityKgM3,
        double NeutralBuoyancyErrorKg,
        bool IsApproximatelyNeutrallyBuoyant)
    {
        public static VehicleBuoyancyProfile Disabled { get; } = new(
            Enabled: false,
            DisplacedVolumeM3: 0.0,
            CenterOfBuoyancyM: Vec3.Zero,
            FluidDensityKgM3: 997.0,
            NeutralBuoyancyErrorKg: 0.0,
            IsApproximatelyNeutrallyBuoyant: false);

        public VehicleBuoyancyProfile Sanitized()
        {
            return this with
            {
                DisplacedVolumeM3 = SafePositive(DisplacedVolumeM3),
                CenterOfBuoyancyM = SanitizeVec(CenterOfBuoyancyM),
                FluidDensityKgM3 = SafePositiveOrDefault(FluidDensityKgM3, 997.0),
                NeutralBuoyancyErrorKg = double.IsFinite(NeutralBuoyancyErrorKg)
                    ? NeutralBuoyancyErrorKg
                    : 0.0
            };
        }

        public double BuoyantMassEquivalentKg =>
            Enabled
                ? DisplacedVolumeM3 * FluidDensityKgM3
                : 0.0;

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }

        private static double SafePositiveOrDefault(double value, double fallback)
        {
            return double.IsFinite(value) && value > 0.0
                ? value
                : fallback;
        }

        private static Vec3 SanitizeVec(Vec3 value)
        {
            return new Vec3(
                double.IsFinite(value.X) ? value.X : 0.0,
                double.IsFinite(value.Y) ? value.Y : 0.0,
                double.IsFinite(value.Z) ? value.Z : 0.0);
        }
    }
}