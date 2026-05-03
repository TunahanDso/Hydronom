using System;

namespace Hydronom.Core.Sensors.Common.Timing
{
    /// <summary>
    /// Tek bir sensor sample'ın zamanlama bilgisi.
    ///
    /// CaptureUtc: sensör verisinin fiziksel/gerçek yakalanma zamanı.
    /// ReceiveUtc: runtime'ın sample'ı aldığı zaman.
    /// PublishUtc: sample'ın üst katmana yayınlandığı zaman.
    /// </summary>
    public readonly record struct SensorTiming(
        DateTime CaptureUtc,
        DateTime ReceiveUtc,
        DateTime PublishUtc,
        double CaptureAgeMs,
        double ReceiveToPublishMs,
        double TargetRateHz,
        double EffectiveRateHz
    )
    {
        public static SensorTiming Now(double targetRateHz = 0.0, double effectiveRateHz = 0.0)
        {
            var now = DateTime.UtcNow;

            return new SensorTiming(
                CaptureUtc: now,
                ReceiveUtc: now,
                PublishUtc: now,
                CaptureAgeMs: 0.0,
                ReceiveToPublishMs: 0.0,
                TargetRateHz: targetRateHz,
                EffectiveRateHz: effectiveRateHz
            );
        }

        public static SensorTiming FromCapture(
            DateTime captureUtc,
            DateTime? receiveUtc = null,
            DateTime? publishUtc = null,
            double targetRateHz = 0.0,
            double effectiveRateHz = 0.0
        )
        {
            var receive = receiveUtc ?? DateTime.UtcNow;
            var publish = publishUtc ?? receive;
            var capture = captureUtc == default ? receive : captureUtc;

            return new SensorTiming(
                CaptureUtc: capture,
                ReceiveUtc: receive,
                PublishUtc: publish,
                CaptureAgeMs: Math.Max(0.0, (publish - capture).TotalMilliseconds),
                ReceiveToPublishMs: Math.Max(0.0, (publish - receive).TotalMilliseconds),
                TargetRateHz: targetRateHz,
                EffectiveRateHz: effectiveRateHz
            ).Sanitized();
        }

        public SensorTiming Sanitized()
        {
            var receive = ReceiveUtc == default ? DateTime.UtcNow : ReceiveUtc;
            var capture = CaptureUtc == default ? receive : CaptureUtc;
            var publish = PublishUtc == default ? receive : PublishUtc;

            return new SensorTiming(
                CaptureUtc: capture,
                ReceiveUtc: receive,
                PublishUtc: publish,
                CaptureAgeMs: SafeNonNegative(CaptureAgeMs),
                ReceiveToPublishMs: SafeNonNegative(ReceiveToPublishMs),
                TargetRateHz: SafeNonNegative(TargetRateHz),
                EffectiveRateHz: SafeNonNegative(EffectiveRateHz)
            );
        }

        public double AgeMs(DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            return Math.Max(0.0, (now - CaptureUtc).TotalMilliseconds);
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}