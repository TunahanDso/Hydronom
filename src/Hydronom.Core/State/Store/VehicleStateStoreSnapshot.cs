using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Store;

/// <summary>
/// Authoritative VehicleState deposunun diagnostic snapshot modeli.
/// </summary>
public readonly record struct VehicleStateStoreSnapshot(
    string VehicleId,
    DateTime TimestampUtc,
    VehicleOperationalState CurrentState,
    bool HasState,
    int AcceptedUpdateCount,
    int RejectedUpdateCount,
    StateUpdateResult? LastResult,
    IReadOnlyList<StateUpdateResult> RecentResults,
    string Summary
)
{
    public VehicleStateStoreSnapshot Sanitized()
    {
        var recent = RecentResults ?? Array.Empty<StateUpdateResult>();

        return new VehicleStateStoreSnapshot(
            VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            CurrentState: CurrentState.Sanitized(),
            HasState: HasState,
            AcceptedUpdateCount: AcceptedUpdateCount < 0 ? 0 : AcceptedUpdateCount,
            RejectedUpdateCount: RejectedUpdateCount < 0 ? 0 : RejectedUpdateCount,
            LastResult: LastResult,
            RecentResults: recent.ToArray(),
            Summary: string.IsNullOrWhiteSpace(Summary) ? "Vehicle state store snapshot." : Summary.Trim()
        );
    }
}