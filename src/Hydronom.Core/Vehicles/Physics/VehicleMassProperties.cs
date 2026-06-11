using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Physics
{
    /// <summary>
    /// Aracın temel kütle ve atalet özellikleridir.
    ///
    /// Bu model simülasyon, kontrol, safety ve raporlama tarafında kullanılır.
    /// </summary>
    public sealed record VehicleMassProperties(
        double MassKg,
        Vec3 CenterOfMassM,
        Vec3 InertiaKgM2)
    {
        public static VehicleMassProperties Unknown { get; } = new(
            MassKg: 0.0,
            CenterOfMassM: Vec3.Zero,
            InertiaKgM2: Vec3.Zero);

        public bool IsValid =>
            double.IsFinite(MassKg) &&
            MassKg > 0.0;

        public VehicleMassProperties Sanitized()
        {
            return this with
            {
                MassKg = SafePositive(MassKg),
                CenterOfMassM = SanitizeVec(CenterOfMassM),
                InertiaKgM2 = SanitizeVec(InertiaKgM2)
            };
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
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