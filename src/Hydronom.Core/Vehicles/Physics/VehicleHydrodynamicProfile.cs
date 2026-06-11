using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Physics
{
    /// <summary>
    /// Su ortamındaki direnç/sürükleme ve sönümleme katsayılarını taşır.
    ///
    /// Bu ilk sürüm bilinçli olarak basit tutuldu:
    /// - Linear damping düşük hızlarda kullanılır.
    /// - Quadratic damping hız arttıkça baskın olur.
    ///
    /// İleride CFD, lookup table veya eksen bazlı gelişmiş model eklenebilir.
    /// </summary>
    public sealed record VehicleHydrodynamicProfile(
        bool Enabled,
        Vec3 LinearDrag,
        Vec3 QuadraticDrag,
        Vec3 AngularLinearDrag,
        Vec3 AngularQuadraticDrag,
        double AddedMassSurgeKg,
        double AddedMassSwayKg,
        double AddedMassHeaveKg)
    {
        public static VehicleHydrodynamicProfile Disabled { get; } = new(
            Enabled: false,
            LinearDrag: Vec3.Zero,
            QuadraticDrag: Vec3.Zero,
            AngularLinearDrag: Vec3.Zero,
            AngularQuadraticDrag: Vec3.Zero,
            AddedMassSurgeKg: 0.0,
            AddedMassSwayKg: 0.0,
            AddedMassHeaveKg: 0.0);

        public VehicleHydrodynamicProfile Sanitized()
        {
            return this with
            {
                LinearDrag = SanitizePositiveVec(LinearDrag),
                QuadraticDrag = SanitizePositiveVec(QuadraticDrag),
                AngularLinearDrag = SanitizePositiveVec(AngularLinearDrag),
                AngularQuadraticDrag = SanitizePositiveVec(AngularQuadraticDrag),
                AddedMassSurgeKg = SafePositive(AddedMassSurgeKg),
                AddedMassSwayKg = SafePositive(AddedMassSwayKg),
                AddedMassHeaveKg = SafePositive(AddedMassHeaveKg)
            };
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }

        private static Vec3 SanitizePositiveVec(Vec3 value)
        {
            return new Vec3(
                SafePositive(value.X),
                SafePositive(value.Y),
                SafePositive(value.Z));
        }
    }
}