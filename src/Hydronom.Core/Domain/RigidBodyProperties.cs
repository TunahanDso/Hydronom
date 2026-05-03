using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Rijit cisim fizik parametreleri.
    ///
    /// Bu model platform baÄŸÄ±msÄ±zdÄ±r:
    /// - Tekne
    /// - DenizaltÄ±
    /// - Paletli araÃ§
    /// - Hava aracÄ±
    /// - AGV
    /// - EndÃ¼striyel makine
    ///
    /// AynÄ± fizik sÃ¶zleÅŸmesini kullanabilir.
    /// </summary>
    public readonly record struct RigidBodyProperties(
        double MassKg,
        Vec3 InertiaBody,
        double MaxLinearSpeed = 100.0,
        double MaxAngularSpeedDeg = 720.0
    )
    {
        public static RigidBodyProperties Default => new(
            MassKg: 1.0,
            InertiaBody: new Vec3(1.0, 1.0, 1.0),
            MaxLinearSpeed: 100.0,
            MaxAngularSpeedDeg: 720.0
        );

        /// <summary>
        /// Fizik parametrelerini gÃ¼venli aralÄ±ÄŸa Ã§eker.
        /// KÃ¼tle, atalet ve limitler sÄ±fÄ±r veya geÃ§ersiz olamaz.
        /// </summary>
        public RigidBodyProperties Sanitized()
        {
            return new RigidBodyProperties(
                MassKg: SafePositive(MassKg, 1.0),
                InertiaBody: new Vec3(
                    SafePositive(InertiaBody.X, 1.0),
                    SafePositive(InertiaBody.Y, 1.0),
                    SafePositive(InertiaBody.Z, 1.0)
                ),
                MaxLinearSpeed: SafePositive(MaxLinearSpeed, 100.0),
                MaxAngularSpeedDeg: SafePositive(MaxAngularSpeedDeg, 720.0)
            );
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Abs(value) < 1e-12 ? fallback : Math.Abs(value);
        }
    }
}
