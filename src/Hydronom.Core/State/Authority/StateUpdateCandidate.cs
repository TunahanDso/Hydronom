using System;
using System.Collections.Generic;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// VehicleOperationalState'i gÃ¼ncellemek isteyen aday state.
    ///
    /// Fusion, estimator, replay, physics truth veya external kaynaklar doÄŸrudan state'i yazmaz.
    /// Ã–nce bu adayÄ± Ã¼retir, sonra StateAuthorityManager karar verir.
    /// </summary>
    public readonly record struct StateUpdateCandidate(
        string CandidateId,
        string VehicleId,
        DateTime TimestampUtc,
        VehiclePose Pose,
        VehicleTwist Twist,
        VehicleAttitude Attitude,
        VehicleStateSourceKind SourceKind,
        double Confidence,
        string FrameId,
        string Reason,
        IReadOnlyList<string> InputSampleIds,
        string TraceId
    )
    {
        public static StateUpdateCandidate FromOperationalState(
            VehicleOperationalState state,
            string reason
        )
        {
            var safe = state.Sanitized();

            return new StateUpdateCandidate(
                CandidateId: Guid.NewGuid().ToString("N"),
                VehicleId: safe.VehicleId,
                TimestampUtc: safe.TimestampUtc,
                Pose: safe.Pose,
                Twist: safe.Twist,
                Attitude: safe.Attitude,
                SourceKind: safe.SourceKind,
                Confidence: safe.Confidence,
                FrameId: safe.FrameId,
                Reason: string.IsNullOrWhiteSpace(reason) ? "STATE_CANDIDATE" : reason.Trim(),
                InputSampleIds: Array.Empty<string>(),
                TraceId: safe.TraceId
            );
        }

        public bool IsFinite =>
            Pose.IsFinite &&
            Twist.IsFinite &&
            Attitude.IsFinite &&
            double.IsFinite(Confidence);

        public StateUpdateCandidate Sanitized()
        {
            return new StateUpdateCandidate(
                CandidateId: string.IsNullOrWhiteSpace(CandidateId) ? Guid.NewGuid().ToString("N") : CandidateId.Trim(),
                VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Pose: Pose.Sanitized(),
                Twist: Twist.Sanitized(),
                Attitude: Attitude.Sanitized(),
                SourceKind: SourceKind,
                Confidence: Clamp(Confidence, 0.0, 1.0),
                FrameId: string.IsNullOrWhiteSpace(FrameId) ? Pose.FrameId : FrameId.Trim(),
                Reason: string.IsNullOrWhiteSpace(Reason) ? "UNSPECIFIED" : Reason.Trim(),
                InputSampleIds: InputSampleIds ?? Array.Empty<string>(),
                TraceId: string.IsNullOrWhiteSpace(TraceId) ? Guid.NewGuid().ToString("N") : TraceId.Trim()
            );
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value))
                return min;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
