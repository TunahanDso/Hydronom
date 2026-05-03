using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Sensors.Common.Capabilities
{
    /// <summary>
    /// SensÃ¶r runtime'Ä±n veya tek bir sensÃ¶rÃ¼n saÄŸladÄ±ÄŸÄ± capability listesi.
    /// </summary>
    public readonly record struct SensorCapabilitySet(
        IReadOnlyList<SensorCapability> Capabilities
    )
    {
        public static SensorCapabilitySet Empty => new(Array.Empty<SensorCapability>());

        public SensorCapabilitySet Sanitized()
        {
            if (Capabilities is null || Capabilities.Count == 0)
                return Empty;

            return new SensorCapabilitySet(
                Capabilities
                    .Select(x => x.Sanitized())
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.Confidence).First())
                    .ToArray()
            );
        }

        public bool Has(string capabilityName, double minConfidence = 0.0)
        {
            if (string.IsNullOrWhiteSpace(capabilityName))
                return false;

            var safe = Sanitized();

            return safe.Capabilities.Any(c =>
                string.Equals(c.Name, capabilityName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                c.Confidence >= minConfidence
            );
        }

        public double GetConfidence(string capabilityName)
        {
            if (string.IsNullOrWhiteSpace(capabilityName))
                return 0.0;

            var safe = Sanitized();

            var match = safe.Capabilities
                .Where(c => string.Equals(c.Name, capabilityName.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Confidence)
                .FirstOrDefault();

            return match.Name is null ? 0.0 : match.Confidence;
        }

        public SensorCapabilitySet AddOrUpdate(SensorCapability capability)
        {
            var safe = Sanitized();
            var cap = capability.Sanitized();

            var list = safe.Capabilities
                .Where(x => !string.Equals(x.Name, cap.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            list.Add(cap);

            return new SensorCapabilitySet(list.ToArray()).Sanitized();
        }
    }
}

