using System;
using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Diagnostics
{
    /// <summary>
    /// StateAuthorityManager durumunun telemetry/diagnostics iÃ§in Ã¶zet gÃ¶rÃ¼ntÃ¼sÃ¼.
    ///
    /// Ops ve Ground Station bu snapshot Ã¼zerinden state pipeline'Ä±n son kararÄ±nÄ± gÃ¶rebilir.
    /// </summary>
    public readonly record struct StateAuthoritySnapshot(
        DateTime TimestampUtc,
        StateAuthorityMode Mode,
        VehicleStateSourceKind CurrentSource,
        double CurrentConfidence,
        string CurrentFrameId,
        DateTime CurrentStateUtc,
        StateUpdateDecision LastDecision,
        bool LastAccepted,
        string LastReason,
        VehicleStateSourceKind LastCandidateSource,
        double LastCandidateAgeMs,
        double LastPositionDeltaMeters,
        double LastImpliedSpeedMps,
        double LastYawDeltaDeg,
        double LastImpliedYawRateDegSec,
        string Summary
    )
    {
        public static StateAuthoritySnapshot FromResult(
            StateAuthorityPolicy policy,
            VehicleOperationalState current,
            StateUpdateResult? lastResult
        )
        {
            var safeCurrent = current.Sanitized();

            if (lastResult is null)
            {
                return new StateAuthoritySnapshot(
                    TimestampUtc: DateTime.UtcNow,
                    Mode: policy.Mode,
                    CurrentSource: safeCurrent.SourceKind,
                    CurrentConfidence: safeCurrent.Confidence,
                    CurrentFrameId: safeCurrent.FrameId,
                    CurrentStateUtc: safeCurrent.TimestampUtc,
                    LastDecision: StateUpdateDecision.Unknown,
                    LastAccepted: false,
                    LastReason: "HenÃ¼z state update kararÄ± yok.",
                    LastCandidateSource: VehicleStateSourceKind.Unknown,
                    LastCandidateAgeMs: 0.0,
                    LastPositionDeltaMeters: 0.0,
                    LastImpliedSpeedMps: 0.0,
                    LastYawDeltaDeg: 0.0,
                    LastImpliedYawRateDegSec: 0.0,
                    Summary: "State authority bekleme durumunda."
                );
            }

            var result = lastResult.Value;

            return new StateAuthoritySnapshot(
                TimestampUtc: DateTime.UtcNow,
                Mode: policy.Mode,
                CurrentSource: safeCurrent.SourceKind,
                CurrentConfidence: safeCurrent.Confidence,
                CurrentFrameId: safeCurrent.FrameId,
                CurrentStateUtc: safeCurrent.TimestampUtc,
                LastDecision: result.Decision,
                LastAccepted: result.Accepted,
                LastReason: result.Reason,
                LastCandidateSource: result.Candidate.SourceKind,
                LastCandidateAgeMs: result.CandidateAgeMs,
                LastPositionDeltaMeters: result.PositionDeltaMeters,
                LastImpliedSpeedMps: result.ImpliedSpeedMps,
                LastYawDeltaDeg: result.YawDeltaDeg,
                LastImpliedYawRateDegSec: result.ImpliedYawRateDegSec,
                Summary: BuildSummary(policy.Mode, result)
            );
        }

        private static string BuildSummary(StateAuthorityMode mode, StateUpdateResult result)
        {
            var decision = result.Accepted ? "accepted" : "rejected";

            return $"State authority mode={mode}, last={decision}, decision={result.Decision}, reason={result.Reason}";
        }
    }
}
