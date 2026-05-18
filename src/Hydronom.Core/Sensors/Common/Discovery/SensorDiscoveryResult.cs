using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Sensors.Common.Discovery
{
    /// <summary>
    /// Sensör keşif işleminin sonucu.
    ///
    /// Hydronom burada sensörü çalıştırmış olmak zorunda değildir.
    /// Bu sonuç sadece "hangi adaylar bulundu, hangileri bağlanabilir görünüyor?"
    /// sorusunu cevaplar.
    /// </summary>
    public sealed record SensorDiscoveryResult
    {
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

        public IReadOnlyList<SensorDiscoveryCandidate> Candidates { get; init; } =
            Array.Empty<SensorDiscoveryCandidate>();

        public IReadOnlyList<string> Warnings { get; init; } =
            Array.Empty<string>();

        public IReadOnlyList<string> Errors { get; init; } =
            Array.Empty<string>();

        public bool HasCandidates => Candidates.Count > 0;

        public bool HasUsableCandidates => Candidates.Any(x => x.Sanitized().IsUsable);

        public bool HasErrors => Errors.Count > 0;

        public static SensorDiscoveryResult Empty { get; } = new();

        public static SensorDiscoveryResult FromCandidates(
            IEnumerable<SensorDiscoveryCandidate> candidates,
            IEnumerable<string>? warnings = null,
            IEnumerable<string>? errors = null)
        {
            return new SensorDiscoveryResult
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Candidates = SanitizeCandidates(candidates),
                Warnings = SanitizeMessages(warnings),
                Errors = SanitizeMessages(errors)
            };
        }

        public SensorDiscoveryResult Sanitized()
        {
            return this with
            {
                Candidates = SanitizeCandidates(Candidates),
                Warnings = SanitizeMessages(Warnings),
                Errors = SanitizeMessages(Errors)
            };
        }

        public SensorDiscoveryResult Merge(SensorDiscoveryResult? other)
        {
            if (other is null)
                return Sanitized();

            var left = Sanitized();
            var right = other.Sanitized();

            return new SensorDiscoveryResult
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Candidates = left.Candidates
                    .Concat(right.Candidates)
                    .GroupBy(x => x.CandidateId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.Confidence).First())
                    .OrderByDescending(x => x.Confidence)
                    .ToArray(),
                Warnings = left.Warnings.Concat(right.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Errors = left.Errors.Concat(right.Errors).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        public IReadOnlyList<SensorDiscoveryCandidate> BestCandidatesPerBackend()
        {
            return Candidates
                .Select(x => x.Sanitized())
                .Where(x => x.IsUsable)
                .GroupBy(x => x.BackendKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Confidence).First())
                .OrderByDescending(x => x.Confidence)
                .ToArray();
        }

        private static IReadOnlyList<SensorDiscoveryCandidate> SanitizeCandidates(
            IEnumerable<SensorDiscoveryCandidate>? candidates)
        {
            if (candidates is null)
                return Array.Empty<SensorDiscoveryCandidate>();

            return candidates
                .Select(x => x.Sanitized())
                .Where(x => !string.IsNullOrWhiteSpace(x.CandidateId))
                .OrderByDescending(x => x.Confidence)
                .ToArray();
        }

        private static IReadOnlyList<string> SanitizeMessages(IEnumerable<string>? messages)
        {
            if (messages is null)
                return Array.Empty<string>();

            return messages
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}