using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;

namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// Hydronom C# primary sensör hattının ana veri sözleşmesi.
    ///
    /// Sim, real, replay ve fallback fark etmeksizin tüm sensör backend'leri üst katmana
    /// SensorSample üretmelidir.
    ///
    /// FusionEngine backend detayına değil, bu ortak modele bakar.
    /// </summary>
    public readonly record struct SensorSample(
        string SampleId,
        SensorIdentity Sensor,
        SensorSourceInfo Source,
        long Sequence,
        DateTime TimestampUtc,
        SensorTiming Timing,
        SensorQuality Quality,
        SensorDataKind DataKind,
        object? Data,
        string CalibrationId,
        string TraceId,
        IReadOnlyList<string> Tags
    )
    {
        public static SensorSample Create(
            SensorIdentity sensor,
            SensorSourceInfo source,
            long sequence,
            SensorDataKind dataKind,
            object? data,
            SensorQuality quality,
            SensorTiming timing,
            string calibrationId = "",
            string traceId = ""
        )
        {
            var safeTiming = timing.Sanitized();
            var sampleTimestamp = safeTiming.CaptureUtc == default
                ? DateTime.UtcNow
                : safeTiming.CaptureUtc;

            return new SensorSample(
                SampleId: Guid.NewGuid().ToString("N"),
                Sensor: sensor.Sanitized(),
                Source: source.Sanitized(),
                Sequence: sequence < 0 ? 0 : sequence,
                TimestampUtc: sampleTimestamp,
                Timing: safeTiming,
                Quality: quality.Sanitized(),
                DataKind: dataKind,
                Data: data,
                CalibrationId: string.IsNullOrWhiteSpace(calibrationId) ? "uncalibrated" : calibrationId.Trim(),
                TraceId: string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId.Trim(),
                Tags: Array.Empty<string>()
            ).Sanitized();
        }

        public bool IsValid => Quality.Valid && Data is not null;

        public bool IsStale(double staleAfterMs, DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            var ageMs = Math.Max(0.0, (now - TimestampUtc).TotalMilliseconds);
            return ageMs > staleAfterMs;
        }

        public T? GetDataAs<T>() where T : struct
        {
            if (Data is T typed)
                return typed;

            return null;
        }

        public SensorSample Sanitized()
        {
            return new SensorSample(
                SampleId: Normalize(SampleId, Guid.NewGuid().ToString("N")),
                Sensor: Sensor.Sanitized(),
                Source: Source.Sanitized(),
                Sequence: Sequence < 0 ? 0 : Sequence,
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Timing: Timing.Sanitized(),
                Quality: Quality.Sanitized(),
                DataKind: DataKind,
                Data: Data,
                CalibrationId: Normalize(CalibrationId, "uncalibrated"),
                TraceId: Normalize(TraceId, Guid.NewGuid().ToString("N")),
                Tags: NormalizeTags(Tags)
            );
        }

        public SensorSample WithTags(params string[] tags)
        {
            return this with
            {
                Tags = NormalizeTags(tags)
            };
        }

        private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
        {
            if (tags is null)
                return Array.Empty<string>();

            return tags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}