using System;

namespace Hydronom.Core.Simulation.Truth
{
    /// <summary>
    /// PhysicsTruthProvider iÃ§in telemetry/diagnostics snapshot modeli.
    ///
    /// Bu model simÃ¼lasyon truth katmanÄ±nÄ±n kullanÄ±labilirliÄŸini, yaÅŸÄ±nÄ± ve Ã¶zet durumunu verir.
    /// </summary>
    public readonly record struct PhysicsTruthSnapshot(
        DateTime TimestampUtc,
        string ProviderName,
        bool IsAvailable,
        DateTime LastTruthUtc,
        double LastTruthAgeMs,
        PhysicsTruthState LatestTruth,
        string Summary
    )
    {
        public static PhysicsTruthSnapshot FromProvider(
            IPhysicsTruthProvider provider,
            DateTime? utcNow = null
        )
        {
            var now = utcNow ?? DateTime.UtcNow;
            var truth = provider.GetLatestTruth().Sanitized();

            var ageMs = provider.LastTruthUtc == default
                ? double.PositiveInfinity
                : Math.Max(0.0, (now - provider.LastTruthUtc).TotalMilliseconds);

            return new PhysicsTruthSnapshot(
                TimestampUtc: now,
                ProviderName: string.IsNullOrWhiteSpace(provider.ProviderName) ? "UnknownTruthProvider" : provider.ProviderName,
                IsAvailable: provider.IsAvailable,
                LastTruthUtc: provider.LastTruthUtc,
                LastTruthAgeMs: ageMs,
                LatestTruth: truth,
                Summary: BuildSummary(provider.ProviderName, provider.IsAvailable, ageMs)
            );
        }

        private static string BuildSummary(string providerName, bool available, double ageMs)
        {
            var name = string.IsNullOrWhiteSpace(providerName) ? "UnknownTruthProvider" : providerName;

            if (!available)
                return $"{name} unavailable.";

            if (double.IsPositiveInfinity(ageMs))
                return $"{name} available but no truth frame has been published yet.";

            return $"{name} available. Last truth age={ageMs:F1} ms.";
        }
    }
}
