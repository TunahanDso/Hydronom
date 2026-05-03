using System;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// State update karar sonucu.
    /// </summary>
    public readonly record struct StateUpdateResult(
        StateUpdateDecision Decision,
        bool Accepted,
        string Reason,
        DateTime EvaluatedUtc,
        VehicleOperationalState StateBefore,
        VehicleOperationalState StateAfter,
        StateUpdateCandidate Candidate,
        double CandidateAgeMs,
        double PositionDeltaMeters,
        double ImpliedSpeedMps,
        double YawDeltaDeg,
        double ImpliedYawRateDegSec
    )
    {
        public static StateUpdateResult Accept(
            VehicleOperationalState before,
            VehicleOperationalState after,
            StateUpdateCandidate candidate,
            string reason,
            double candidateAgeMs,
            double positionDeltaMeters,
            double impliedSpeedMps,
            double yawDeltaDeg,
            double impliedYawRateDegSec
        )
        {
            return new StateUpdateResult(
                Decision: StateUpdateDecision.Accepted,
                Accepted: true,
                Reason: reason,
                EvaluatedUtc: DateTime.UtcNow,
                StateBefore: before,
                StateAfter: after,
                Candidate: candidate,
                CandidateAgeMs: candidateAgeMs,
                PositionDeltaMeters: positionDeltaMeters,
                ImpliedSpeedMps: impliedSpeedMps,
                YawDeltaDeg: yawDeltaDeg,
                ImpliedYawRateDegSec: impliedYawRateDegSec
            );
        }

        public static StateUpdateResult Reject(
            VehicleOperationalState current,
            StateUpdateCandidate candidate,
            StateUpdateDecision decision,
            string reason,
            double candidateAgeMs,
            double positionDeltaMeters,
            double impliedSpeedMps,
            double yawDeltaDeg,
            double impliedYawRateDegSec
        )
        {
            return new StateUpdateResult(
                Decision: decision,
                Accepted: false,
                Reason: reason,
                EvaluatedUtc: DateTime.UtcNow,
                StateBefore: current,
                StateAfter: current,
                Candidate: candidate,
                CandidateAgeMs: candidateAgeMs,
                PositionDeltaMeters: positionDeltaMeters,
                ImpliedSpeedMps: impliedSpeedMps,
                YawDeltaDeg: yawDeltaDeg,
                ImpliedYawRateDegSec: impliedYawRateDegSec
            );
        }
    }
}
