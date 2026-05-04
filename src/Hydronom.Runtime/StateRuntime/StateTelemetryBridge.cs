using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Store;
using Hydronom.Core.State.Telemetry;

namespace Hydronom.Runtime.StateRuntime;

/// <summary>
/// Authoritative VehicleState ve StateAuthority kararlarını telemetry modeline çevirir.
/// </summary>
public sealed class StateTelemetryBridge
{
    public StateAuthorityTelemetry Project(VehicleStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        return Project(store.GetSnapshot());
    }

    public StateAuthorityTelemetry Project(VehicleStateStoreSnapshot snapshot)
    {
        var safe = snapshot.Sanitized();
        var state = safe.CurrentState.Sanitized();
        var last = safe.LastResult;

        return new StateAuthorityTelemetry(
            VehicleId: safe.VehicleId,
            TimestampUtc: DateTime.UtcNow,
            HasState: safe.HasState,
            Pose: state.Pose,
            Twist: state.Twist,
            Attitude: state.Attitude,
            AuthorityMode: state.AuthorityMode,
            SourceKind: state.SourceKind,
            Confidence: state.Confidence,
            LastUpdateAccepted: last?.Accepted ?? false,
            LastDecision: last?.Decision ?? StateUpdateDecision.Unknown,
            LastReason: last?.Reason ?? "NO_DECISION",
            AcceptedUpdateCount: safe.AcceptedUpdateCount,
            RejectedUpdateCount: safe.RejectedUpdateCount,
            Summary: safe.Summary
        ).Sanitized();
    }
}