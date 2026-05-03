using System;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Diagnostics
{
    /// <summary>
    /// Tek bir sensÃ¶rÃ¼n genel saÄŸlÄ±k snapshot'Ä±.
    ///
    /// Bu tek sample kalitesi deÄŸildir.
    /// SensÃ¶rÃ¼n genel Ã§alÄ±ÅŸma durumu, rate bilgisi ve hata sayÄ±larÄ±dÄ±r.
    /// </summary>
    public readonly record struct SensorHealthSnapshot(
        string SensorId,
        string SourceId,
        SensorDataKind DataKind,
        SensorHealthState State,
        DateTime TimestampUtc,
        DateTime LastSampleUtc,
        DateTime LastGoodSampleUtc,
        double LastSampleAgeMs,
        double EffectiveRateHz,
        double TargetRateHz,
        int ErrorCount,
        int ConsecutiveFailureCount,
        string LastError,
        SensorBackendKind BackendKind,
        string BackendName,
        bool Simulated,
        bool Replay,
        string Summary
    )
    {
        public static SensorHealthSnapshot Unknown(string sensorId)
        {
            return new SensorHealthSnapshot(
                SensorId: string.IsNullOrWhiteSpace(sensorId) ? "sensor" : sensorId.Trim(),
                SourceId: string.IsNullOrWhiteSpace(sensorId) ? "sensor" : sensorId.Trim(),
                DataKind: SensorDataKind.Unknown,
                State: SensorHealthState.Unknown,
                TimestampUtc: DateTime.UtcNow,
                LastSampleUtc: default,
                LastGoodSampleUtc: default,
                LastSampleAgeMs: double.PositiveInfinity,
                EffectiveRateHz: 0.0,
                TargetRateHz: 0.0,
                ErrorCount: 0,
                ConsecutiveFailureCount: 0,
                LastError: "",
                BackendKind: SensorBackendKind.Unknown,
                BackendName: "unknown",
                Simulated: false,
                Replay: false,
                Summary: "Sensor health unknown."
            );
        }

        public SensorHealthSnapshot Sanitized()
        {
            return new SensorHealthSnapshot(
                SensorId: Normalize(SensorId, "sensor"),
                SourceId: Normalize(SourceId, SensorId),
                DataKind: DataKind,
                State: State,
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                LastSampleUtc: LastSampleUtc,
                LastGoodSampleUtc: LastGoodSampleUtc,
                LastSampleAgeMs: SafeNonNegativeOrInfinity(LastSampleAgeMs),
                EffectiveRateHz: SafeNonNegative(EffectiveRateHz),
                TargetRateHz: SafeNonNegative(TargetRateHz),
                ErrorCount: ErrorCount < 0 ? 0 : ErrorCount,
                ConsecutiveFailureCount: ConsecutiveFailureCount < 0 ? 0 : ConsecutiveFailureCount,
                LastError: LastError?.Trim() ?? "",
                BackendKind: BackendKind,
                BackendName: Normalize(BackendName, BackendKind.ToString()),
                Simulated: Simulated,
                Replay: Replay,
                Summary: Normalize(Summary, "Sensor health.")
            );
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

        private static double SafeNonNegativeOrInfinity(double value)
        {
            if (double.IsPositiveInfinity(value))
                return value;

            return SafeNonNegative(value);
        }
    }
}

