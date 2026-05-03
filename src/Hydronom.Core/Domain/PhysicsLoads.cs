namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Bir fizik adÄ±mÄ±nda uygulanacak dÄ±ÅŸ yÃ¼kler.
    ///
    /// ForceWorld dÃ¼nya frame'de, TorqueBody body frame'de tutulur.
    /// Platforma Ã¶zel modeller toplam yÃ¼kleri hesaplayÄ±p buraya aktarÄ±r.
    /// </summary>
    public readonly record struct PhysicsLoads(
        Vec3 ForceWorld,
        Vec3 TorqueBody
    )
    {
        public static PhysicsLoads Zero => new(Vec3.Zero, Vec3.Zero);

        /// <summary>
        /// GeÃ§ersiz kuvvet veya moment deÄŸerlerini sÄ±fÄ±ra Ã§eker.
        /// </summary>
        public PhysicsLoads Sanitized()
        {
            return new PhysicsLoads(
                SanitizeVec(ForceWorld),
                SanitizeVec(TorqueBody)
            );
        }

        public static PhysicsLoads operator +(PhysicsLoads a, PhysicsLoads b)
        {
            return new PhysicsLoads(
                a.ForceWorld + b.ForceWorld,
                a.TorqueBody + b.TorqueBody
            );
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                double.IsFinite(v.X) ? v.X : 0.0,
                double.IsFinite(v.Y) ? v.Y : 0.0,
                double.IsFinite(v.Z) ? v.Z : 0.0
            );
        }
    }
}
