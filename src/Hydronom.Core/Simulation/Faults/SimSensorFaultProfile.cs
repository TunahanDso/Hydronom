namespace Hydronom.Core.Simulation.Faults
{
    /// <summary>
    /// Sim sensÃ¶rlere kontrollÃ¼ hata enjekte etmek iÃ§in fault profili.
    ///
    /// Bu profil safety, diagnostics ve degraded-mode testleri iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimSensorFaultProfile(
        double DropProbability,
        double FreezeProbability,
        double TimestampJitterMs,
        double WrongFrameProbability,
        bool ForceStale,
        bool ForceInvalid,
        bool Enabled
    )
    {
        public static SimSensorFaultProfile None => new(
            DropProbability: 0.0,
            FreezeProbability: 0.0,
            TimestampJitterMs: 0.0,
            WrongFrameProbability: 0.0,
            ForceStale: false,
            ForceInvalid: false,
            Enabled: false
        );

        public SimSensorFaultProfile Sanitized()
        {
            return new SimSensorFaultProfile(
                DropProbability: Clamp01(DropProbability),
                FreezeProbability: Clamp01(FreezeProbability),
                TimestampJitterMs: SafeNonNegative(TimestampJitterMs),
                WrongFrameProbability: Clamp01(WrongFrameProbability),
                ForceStale: ForceStale,
                ForceInvalid: ForceInvalid,
                Enabled: Enabled
            );
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
