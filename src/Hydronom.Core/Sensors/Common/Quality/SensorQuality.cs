using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Quality
{
    /// <summary>
    /// Tek bir SensorSample'ın kalite bilgisi.
    ///
    /// SensorQuality, SensorHealth ile aynı şey değildir.
    /// Quality tek sample'ın geçerliliği ve güvenilirliğidir.
    /// Health ise sensörün genel çalışma sağlığıdır.
    /// </summary>
    public readonly record struct SensorQuality(
        bool Valid,
        double Confidence,
        double AgeMs,
        double LatencyMs,
        double TargetRateHz,
        double EffectiveRateHz,
        bool Simulated,
        SensorBackendKind BackendKind,
        string BackendName,
        string Error,
        IReadOnlyList<string> Hints
    )
    {
        public static SensorQuality Good(
            SensorBackendKind backendKind,
            string backendName,
            bool simulated = false,
            double confidence = 1.0
        )
        {
            return new SensorQuality(
                Valid: true,
                Confidence: confidence,
                AgeMs: 0.0,
                LatencyMs: 0.0,
                TargetRateHz: 0.0,
                EffectiveRateHz: 0.0,
                Simulated: simulated,
                BackendKind: backendKind,
                BackendName: Normalize(backendName, backendKind.ToString()),
                Error: "",
                Hints: Array.Empty<string>()
            ).Sanitized();
        }

        public static SensorQuality Invalid(string error)
        {
            return new SensorQuality(
                Valid: false,
                Confidence: 0.0,
                AgeMs: 0.0,
                LatencyMs: 0.0,
                TargetRateHz: 0.0,
                EffectiveRateHz: 0.0,
                Simulated: false,
                BackendKind: SensorBackendKind.Unknown,
                BackendName: "unknown",
                Error: Normalize(error, "Invalid sample."),
                Hints: Array.Empty<string>()
            );
        }

        public SensorQuality Sanitized()
        {
            return new SensorQuality(
                Valid: Valid,
                Confidence: Clamp01(Confidence),
                AgeMs: SafeNonNegative(AgeMs),
                LatencyMs: SafeNonNegative(LatencyMs),
                TargetRateHz: SafeNonNegative(TargetRateHz),
                EffectiveRateHz: SafeNonNegative(EffectiveRateHz),
                Simulated: Simulated,
                BackendKind: BackendKind,
                BackendName: Normalize(BackendName, BackendKind.ToString()),
                Error: Error?.Trim() ?? "",
                Hints: NormalizeList(Hints)
            );
        }

        public SensorQuality WithTiming(double ageMs, double latencyMs, double targetRateHz, double effectiveRateHz)
        {
            return this with
            {
                AgeMs = SafeNonNegative(ageMs),
                LatencyMs = SafeNonNegative(latencyMs),
                TargetRateHz = SafeNonNegative(targetRateHz),
                EffectiveRateHz = SafeNonNegative(effectiveRateHz)
            };
        }

        public SensorQuality WithError(string error)
        {
            return this with
            {
                Valid = false,
                Confidence = 0.0,
                Error = Normalize(error, "Invalid sample.")
            };
        }

        private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
                return Array.Empty<string>();

            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
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
    }
}