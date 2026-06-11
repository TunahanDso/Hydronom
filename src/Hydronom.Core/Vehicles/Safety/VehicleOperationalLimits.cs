using System;

namespace Hydronom.Core.Vehicles.Safety
{
    /// <summary>
    /// Aracın operasyonel limitlerini tanımlar.
    ///
    /// Bu limitler:
    /// - Simülasyon sınırlandırması
    /// - Safety gate kontrolü
    /// - Ground Station uyarıları
    /// - Görev uyumluluğu
    /// için kullanılır.
    /// </summary>
    public sealed record VehicleOperationalLimits(
        double MaxForwardSpeedMps,
        double MaxReverseSpeedMps,
        double MaxLateralSpeedMps,
        double MaxVerticalSpeedMps,
        double MaxYawRateDegps,
        double MaxRollDeg,
        double MaxPitchDeg,
        double MaxDepthM,
        double MinDepthM,
        double MaxOperatingTimeSeconds,
        double MinBatteryPercent)
    {
        public static VehicleOperationalLimits Conservative { get; } = new(
            MaxForwardSpeedMps: 1.0,
            MaxReverseSpeedMps: 0.25,
            MaxLateralSpeedMps: 0.25,
            MaxVerticalSpeedMps: 0.25,
            MaxYawRateDegps: 90.0,
            MaxRollDeg: 25.0,
            MaxPitchDeg: 25.0,
            MaxDepthM: 2.0,
            MinDepthM: 0.0,
            MaxOperatingTimeSeconds: 300.0,
            MinBatteryPercent: 20.0);

        public VehicleOperationalLimits Sanitized()
        {
            var minDepth = SafePositive(MinDepthM);
            var maxDepth = SafePositive(MaxDepthM);

            if (maxDepth < minDepth)
                maxDepth = minDepth;

            return this with
            {
                MaxForwardSpeedMps = Clamp(MaxForwardSpeedMps, 0.0, 50.0, 1.0),
                MaxReverseSpeedMps = Clamp(MaxReverseSpeedMps, 0.0, 50.0, 0.25),
                MaxLateralSpeedMps = Clamp(MaxLateralSpeedMps, 0.0, 50.0, 0.25),
                MaxVerticalSpeedMps = Clamp(MaxVerticalSpeedMps, 0.0, 20.0, 0.25),
                MaxYawRateDegps = Clamp(MaxYawRateDegps, 0.0, 720.0, 90.0),
                MaxRollDeg = Clamp(MaxRollDeg, 0.0, 180.0, 25.0),
                MaxPitchDeg = Clamp(MaxPitchDeg, 0.0, 180.0, 25.0),
                MinDepthM = minDepth,
                MaxDepthM = maxDepth,
                MaxOperatingTimeSeconds = Clamp(MaxOperatingTimeSeconds, 1.0, 86400.0, 300.0),
                MinBatteryPercent = Clamp(MinBatteryPercent, 0.0, 100.0, 20.0)
            };
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }
    }
}