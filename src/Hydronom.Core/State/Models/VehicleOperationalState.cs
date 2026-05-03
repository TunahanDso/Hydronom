using System;
using Hydronom.Core.State.Authority;

namespace Hydronom.Core.State.Models
{
    /// <summary>
    /// Hydronom'un operasyonel authoritative araÃ§ state modeli.
    ///
    /// Decision, Safety, Telemetry ve Ops ana araÃ§ marker'Ä± bu state Ã¼zerinden beslenmelidir.
    ///
    /// Bu model mevcut Domain.VehicleState fizik modeli ile aynÄ± ÅŸey deÄŸildir.
    /// Domain.VehicleState fizik entegrasyonu ve eski runtime uyumluluÄŸu iÃ§in korunur.
    /// VehicleOperationalState ise yeni state authority pipeline'Ä±nÄ±n ana Ã§Ä±ktÄ±sÄ±dÄ±r.
    /// </summary>
    public readonly record struct VehicleOperationalState(
        string VehicleId,
        DateTime TimestampUtc,
        VehiclePose Pose,
        VehicleTwist Twist,
        VehicleAttitude Attitude,
        VehicleStateSourceKind SourceKind,
        StateAuthorityMode AuthorityMode,
        double Confidence,
        string FrameId,
        string QualitySummary,
        string TraceId
    )
    {
        public static VehicleOperationalState CreateInitial(
            string vehicleId,
            StateAuthorityMode mode = StateAuthorityMode.CSharpPrimary
        )
        {
            return new VehicleOperationalState(
                VehicleId: string.IsNullOrWhiteSpace(vehicleId) ? "UNKNOWN" : vehicleId.Trim(),
                TimestampUtc: DateTime.UtcNow,
                Pose: VehiclePose.Zero,
                Twist: VehicleTwist.Zero,
                Attitude: VehicleAttitude.Zero,
                SourceKind: VehicleStateSourceKind.Unknown,
                AuthorityMode: mode,
                Confidence: 0.0,
                FrameId: "map",
                QualitySummary: "INITIAL",
                TraceId: Guid.NewGuid().ToString("N")
            );
        }

        public bool IsFinite =>
            Pose.IsFinite &&
            Twist.IsFinite &&
            Attitude.IsFinite &&
            double.IsFinite(Confidence);

        public VehicleOperationalState Sanitized()
        {
            return new VehicleOperationalState(
                VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Pose: Pose.Sanitized(),
                Twist: Twist.Sanitized(),
                Attitude: Attitude.Sanitized(),
                SourceKind: SourceKind,
                AuthorityMode: AuthorityMode,
                Confidence: Clamp(Confidence, 0.0, 1.0),
                FrameId: string.IsNullOrWhiteSpace(FrameId) ? Pose.FrameId : FrameId.Trim(),
                QualitySummary: string.IsNullOrWhiteSpace(QualitySummary) ? "UNSPECIFIED" : QualitySummary.Trim(),
                TraceId: string.IsNullOrWhiteSpace(TraceId) ? Guid.NewGuid().ToString("N") : TraceId.Trim()
            );
        }

        public double AgeMs(DateTime utcNow)
        {
            var age = utcNow - TimestampUtc;
            return age.TotalMilliseconds < 0.0 ? 0.0 : age.TotalMilliseconds;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
