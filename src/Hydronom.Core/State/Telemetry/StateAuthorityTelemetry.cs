using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Telemetry;

/// <summary>
/// Son state authority kararını ve authoritative state özetini telemetry için taşır.
/// </summary>
public readonly record struct StateAuthorityTelemetry(
    string VehicleId,
    DateTime TimestampUtc,
    bool HasState,
    VehiclePose Pose,
    VehicleTwist Twist,
    VehicleAttitude Attitude,
    StateAuthorityMode AuthorityMode,
    VehicleStateSourceKind SourceKind,
    double Confidence,
    bool LastUpdateAccepted,
    StateUpdateDecision LastDecision,
    string LastReason,
    int AcceptedUpdateCount,
    int RejectedUpdateCount,
    string Summary
)
{
    public StateAuthorityTelemetry Sanitized()
    {
        return new StateAuthorityTelemetry(
            VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            HasState: HasState,
            Pose: Pose.Sanitized(),
            Twist: Twist.Sanitized(),
            Attitude: Attitude.Sanitized(),
            AuthorityMode: AuthorityMode,
            SourceKind: SourceKind,
            Confidence: Clamp01(Confidence),
            LastUpdateAccepted: LastUpdateAccepted,
            LastDecision: LastDecision,
            LastReason: string.IsNullOrWhiteSpace(LastReason) ? "NO_DECISION" : LastReason.Trim(),
            AcceptedUpdateCount: AcceptedUpdateCount < 0 ? 0 : AcceptedUpdateCount,
            RejectedUpdateCount: RejectedUpdateCount < 0 ? 0 : RejectedUpdateCount,
            Summary: string.IsNullOrWhiteSpace(Summary) ? "State authority telemetry." : Summary.Trim()
        );
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}