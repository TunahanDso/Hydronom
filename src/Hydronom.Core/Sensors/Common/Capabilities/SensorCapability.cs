using System;

namespace Hydronom.Core.Sensors.Common.Capabilities
{
    /// <summary>
    /// Bir sensÃ¶rÃ¼n veya sensÃ¶r grubunun saÄŸladÄ±ÄŸÄ± yetenek.
    ///
    /// Hydronom artÄ±k sensÃ¶r adÄ±na deÄŸil capability'ye gÃ¶re karar vermelidir.
    /// Ã–rnek:
    /// - global_position
    /// - heading_estimation
    /// - obstacle_detection
    /// - underwater_localization
    /// - power_monitoring
    /// </summary>
    public readonly record struct SensorCapability(
        string Name,
        double Confidence,
        double TargetRateHz,
        string FrameId,
        string Provider,
        bool RequiredCalibration,
        bool CalibrationValid
    )
    {
        public static SensorCapability Create(
            string name,
            double confidence,
            string provider,
            string frameId = "base_link",
            double targetRateHz = 0.0
        )
        {
            return new SensorCapability(
                Name: Normalize(name, "capability"),
                Confidence: confidence,
                TargetRateHz: targetRateHz,
                FrameId: Normalize(frameId, "base_link"),
                Provider: Normalize(provider, "sensor"),
                RequiredCalibration: false,
                CalibrationValid: true
            ).Sanitized();
        }

        public SensorCapability Sanitized()
        {
            return new SensorCapability(
                Name: Normalize(Name, "capability"),
                Confidence: Clamp01(Confidence),
                TargetRateHz: SafeNonNegative(TargetRateHz),
                FrameId: Normalize(FrameId, "base_link"),
                Provider: Normalize(Provider, "sensor"),
                RequiredCalibration: RequiredCalibration,
                CalibrationValid: CalibrationValid
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

