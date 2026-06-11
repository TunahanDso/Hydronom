using System;

namespace Hydronom.Core.Vehicles.Safety
{
    /// <summary>
    /// Aracın safety/failsafe profilidir.
    ///
    /// Bu model araç özelinde:
    /// - E-stop davranışı
    /// - Leak sensor gereksinimi
    /// - Haberleşme kaybı davranışı
    /// - Maksimum operasyon limitleri
    /// - İnsan/araç güvenliği sınırları
    /// bilgisini taşır.
    /// </summary>
    public sealed record VehicleSafetyProfile(
        bool Enabled,
        bool RequiresEmergencyStop,
        bool RequiresLeakSensor,
        bool RequiresDepthSensor,
        bool RequiresBatteryMonitor,
        bool StopThrustersOnCommsLoss,
        bool SurfaceOnCommsLoss,
        bool HoldPositionOnCommsLoss,
        double CommsLossTimeoutSeconds,
        VehicleOperationalLimits OperationalLimits,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleSafetyProfile Conservative { get; } = new(
            Enabled: true,
            RequiresEmergencyStop: true,
            RequiresLeakSensor: false,
            RequiresDepthSensor: false,
            RequiresBatteryMonitor: true,
            StopThrustersOnCommsLoss: true,
            SurfaceOnCommsLoss: false,
            HoldPositionOnCommsLoss: false,
            CommsLossTimeoutSeconds: 2.0,
            OperationalLimits: VehicleOperationalLimits.Conservative,
            Tags: new Dictionary<string, string>());

        public VehicleSafetyProfile Sanitized()
        {
            return this with
            {
                CommsLossTimeoutSeconds = Clamp(CommsLossTimeoutSeconds, 0.1, 300.0, 2.0),
                OperationalLimits = (OperationalLimits ?? VehicleOperationalLimits.Conservative).Sanitized(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public bool ShouldFailSafeOnCommsLoss =>
            StopThrustersOnCommsLoss ||
            SurfaceOnCommsLoss ||
            HoldPositionOnCommsLoss;

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }
    }
}