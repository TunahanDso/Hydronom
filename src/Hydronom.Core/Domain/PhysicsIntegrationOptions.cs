namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Fizik entegrasyonu iÃ§in gÃ¼venlik ve yÃ¶ntem ayarlarÄ±.
    /// </summary>
    public readonly record struct PhysicsIntegrationOptions(
        PhysicsIntegrationMode IntegrationMode,
        bool EnableGyroscopicTerm,
        double MaxTimeStep,
        double MaxForceMagnitude,
        double MaxTorqueMagnitude
    )
    {
        public static PhysicsIntegrationOptions Default => new(
            IntegrationMode: PhysicsIntegrationMode.SemiImplicitEuler,
            EnableGyroscopicTerm: true,
            MaxTimeStep: 0.05,
            MaxForceMagnitude: 1_000_000.0,
            MaxTorqueMagnitude: 1_000_000.0
        );

        /// <summary>
        /// Entegrasyon ayarlarÄ±nÄ± gÃ¼venli aralÄ±klara Ã§eker.
        /// </summary>
        public PhysicsIntegrationOptions Sanitized()
        {
            return new PhysicsIntegrationOptions(
                IntegrationMode,
                EnableGyroscopicTerm,
                MaxTimeStep: SafePositive(MaxTimeStep, 0.05),
                MaxForceMagnitude: SafePositive(MaxForceMagnitude, 1_000_000.0),
                MaxTorqueMagnitude: SafePositive(MaxTorqueMagnitude, 1_000_000.0)
            );
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }
    }
}
