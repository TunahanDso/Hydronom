using System;
using System.Collections.Generic;
using Hydronom.Core.Sensors.Common.Connections;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Core.Sensors.Common.Discovery
{
    /// <summary>
    /// Keşif sırasında bulunan potansiyel sensör adayı.
    ///
    /// Bu aday kesin sensör demek değildir.
    /// Bir probe tarafından "bu bağlantıda şu sensör olabilir" şeklinde raporlanır.
    /// </summary>
    public sealed record SensorDiscoveryCandidate
    {
        public string CandidateId { get; init; } = string.Empty;

        public SensorDataKind DataKind { get; init; } = SensorDataKind.Unknown;

        public string BackendKey { get; init; } = string.Empty;

        public string ProbeId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public SensorConnectionDescriptor Connection { get; init; } =
            SensorConnectionDescriptor.Auto();

        /// <summary>
        /// 0.0 - 1.0 arası güven skoru.
        /// Örnek:
        /// - USB VID/PID eşleşti ama handshake yok: 0.35
        /// - Frame header görüldü: 0.65
        /// - Gerçek handshake başarılı: 0.90+
        /// </summary>
        public double Confidence { get; init; }

        public bool IsDefinitive { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsUsable =>
            DataKind != SensorDataKind.Unknown &&
            !string.IsNullOrWhiteSpace(BackendKey) &&
            Connection.HasUsableEndpoint &&
            Confidence > 0.0;

        public static SensorDiscoveryCandidate Empty { get; } = new();

        public SensorDiscoveryCandidate Sanitized()
        {
            var candidateId = string.IsNullOrWhiteSpace(CandidateId)
                ? BuildCandidateId(DataKind, BackendKey, Connection)
                : CandidateId.Trim();

            return this with
            {
                CandidateId = candidateId,
                BackendKey = Normalize(BackendKey),
                ProbeId = Normalize(ProbeId),
                DisplayName = string.IsNullOrWhiteSpace(DisplayName)
                    ? candidateId
                    : DisplayName.Trim(),
                Connection = (Connection ?? SensorConnectionDescriptor.Auto()).Sanitized(),
                Confidence = Clamp01(Confidence),
                Metadata = SanitizeMetadata(Metadata)
            };
        }

        public override string ToString()
        {
            var clean = Sanitized();
            return $"{clean.DataKind}:{clean.BackendKey}:{clean.Connection}:confidence={clean.Confidence:0.00}";
        }

        private static string BuildCandidateId(
            SensorDataKind kind,
            string backendKey,
            SensorConnectionDescriptor connection)
        {
            var backend = string.IsNullOrWhiteSpace(backendKey)
                ? "unknown_backend"
                : backendKey.Trim();

            var endpoint = connection?.ToString() ?? "unknown_endpoint";

            return $"{kind}_{backend}_{endpoint}"
                .Replace(":", "_", StringComparison.Ordinal)
                .Replace("@", "_", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .Replace("\\", "_", StringComparison.Ordinal)
                .Replace(" ", "_", StringComparison.Ordinal)
                .ToLowerInvariant();
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }

        private static IReadOnlyDictionary<string, string> SanitizeMetadata(
            IReadOnlyDictionary<string, string>? metadata)
        {
            if (metadata is null || metadata.Count == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                result[pair.Key.Trim()] = pair.Value?.Trim() ?? string.Empty;
            }

            return result;
        }
    }
}