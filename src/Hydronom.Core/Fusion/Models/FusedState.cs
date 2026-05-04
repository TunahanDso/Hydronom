using Hydronom.Core.Fusion.Quality;
using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.Fusion.Models;

/// <summary>
/// FusionEngine tarafından üretilen tahmini state modeli.
/// Bu model authoritative state değildir; StateUpdateCandidate'a çevrilir.
/// </summary>
public readonly record struct FusedState(
    string VehicleId,
    DateTime TimestampUtc,
    VehiclePose Pose,
    VehicleTwist Twist,
    VehicleAttitude Attitude,
    FusionQuality Quality,
    string FrameId,
    IReadOnlyList<string> InputSampleIds,
    string TraceId
)
{
    public bool IsValid =>
        Pose.IsFinite &&
        Twist.IsFinite &&
        Attitude.IsFinite &&
        Quality.Valid;

    public StateUpdateCandidate ToCandidate(
        VehicleStateSourceKind sourceKind,
        string reason)
    {
        var safe = Sanitized();

        return new StateUpdateCandidate(
            CandidateId: Guid.NewGuid().ToString("N"),
            VehicleId: safe.VehicleId,
            TimestampUtc: safe.TimestampUtc,
            Pose: safe.Pose,
            Twist: safe.Twist,
            Attitude: safe.Attitude,
            SourceKind: sourceKind,
            Confidence: safe.Quality.Confidence,
            FrameId: safe.FrameId,
            Reason: string.IsNullOrWhiteSpace(reason) ? safe.Quality.Summary : reason.Trim(),
            InputSampleIds: safe.InputSampleIds,
            TraceId: safe.TraceId
        ).Sanitized();
    }

    public FusedState Sanitized()
    {
        var quality = Quality.Sanitized();

        return new FusedState(
            VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            Pose: Pose.Sanitized(),
            Twist: Twist.Sanitized(),
            Attitude: Attitude.Sanitized(),
            Quality: quality,
            FrameId: string.IsNullOrWhiteSpace(FrameId) ? Pose.FrameId : FrameId.Trim(),
            InputSampleIds: NormalizeIds(InputSampleIds),
            TraceId: string.IsNullOrWhiteSpace(TraceId) ? Guid.NewGuid().ToString("N") : TraceId.Trim()
        );
    }

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<string>();
        }

        return ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}