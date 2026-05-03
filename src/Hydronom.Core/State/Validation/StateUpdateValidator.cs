using System;
using System.Collections.Generic;
using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Validation
{
    /// <summary>
    /// State update adaylarÄ± iÃ§in genel doÄŸrulama yardÄ±mcÄ±sÄ±.
    ///
    /// Bu sÄ±nÄ±f doÄŸrudan authority kararÄ±nÄ± vermez.
    /// Authority kararÄ±nÄ± StateAuthorityManager verir.
    ///
    /// Bu validator ise diagnostics, telemetry, debug ve Ã¶n analiz iÃ§in aday state'in
    /// genel yapÄ±sal saÄŸlÄ±ÄŸÄ±nÄ± kontrol eder.
    /// </summary>
    public sealed class StateUpdateValidator
    {
        public IReadOnlyList<StateValidationIssue> ValidateCandidate(
            VehicleOperationalState current,
            StateUpdateCandidate candidate,
            DateTime? utcNow = null
        )
        {
            var now = utcNow ?? DateTime.UtcNow;
            var issues = new List<StateValidationIssue>();

            var safeCurrent = current.Sanitized();
            var safeCandidate = candidate.Sanitized();

            if (!safeCandidate.IsFinite)
            {
                issues.Add(StateValidationIssue.Critical(
                    "STATE_CANDIDATE_NON_FINITE",
                    "State update adayÄ± NaN veya Infinity deÄŸer iÃ§eriyor.",
                    nameof(StateUpdateValidator)
                ));
            }

            if (string.Equals(safeCandidate.VehicleId, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(StateValidationIssue.Error(
                    "STATE_CANDIDATE_UNKNOWN_VEHICLE",
                    "State update adayÄ±nÄ±n araÃ§ kimliÄŸi bilinmiyor.",
                    nameof(StateUpdateValidator)
                ));
            }

            if (!string.Equals(safeCurrent.VehicleId, safeCandidate.VehicleId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(StateValidationIssue.Error(
                    "STATE_CANDIDATE_VEHICLE_MISMATCH",
                    "State update adayÄ± mevcut authoritative state ile farklÄ± araÃ§ kimliÄŸine sahip.",
                    nameof(StateUpdateValidator)
                ));
            }

            if (safeCandidate.TimestampUtc == default)
            {
                issues.Add(StateValidationIssue.Error(
                    "STATE_CANDIDATE_DEFAULT_TIMESTAMP",
                    "State update adayÄ±nÄ±n timestamp deÄŸeri default durumda.",
                    nameof(StateUpdateValidator)
                ));
            }

            var ageMs = Math.Max(0.0, (now - safeCandidate.TimestampUtc).TotalMilliseconds);

            if (ageMs > 1_000.0)
            {
                issues.Add(StateValidationIssue.Warning(
                    "STATE_CANDIDATE_OLD_TIMESTAMP",
                    $"State update adayÄ± eski gÃ¶rÃ¼nÃ¼yor. AgeMs={ageMs:F1}",
                    nameof(StateUpdateValidator)
                ));
            }

            if (safeCandidate.Confidence < 0.10)
            {
                issues.Add(StateValidationIssue.Warning(
                    "STATE_CANDIDATE_LOW_CONFIDENCE",
                    $"State update adayÄ±nÄ±n confidence deÄŸeri dÃ¼ÅŸÃ¼k. Confidence={safeCandidate.Confidence:F3}",
                    nameof(StateUpdateValidator)
                ));
            }

            if (string.IsNullOrWhiteSpace(safeCandidate.FrameId))
            {
                issues.Add(StateValidationIssue.Error(
                    "STATE_CANDIDATE_EMPTY_FRAME",
                    "State update adayÄ±nÄ±n FrameId bilgisi boÅŸ.",
                    nameof(StateUpdateValidator)
                ));
            }

            if (!string.Equals(safeCurrent.FrameId, safeCandidate.FrameId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(StateValidationIssue.Warning(
                    "STATE_CANDIDATE_FRAME_MISMATCH",
                    $"State update adayÄ±nÄ±n frame bilgisi mevcut state ile farklÄ±. Current={safeCurrent.FrameId}, Candidate={safeCandidate.FrameId}",
                    nameof(StateUpdateValidator)
                ));
            }

            if (safeCandidate.SourceKind == VehicleStateSourceKind.Unknown)
            {
                issues.Add(StateValidationIssue.Warning(
                    "STATE_CANDIDATE_UNKNOWN_SOURCE",
                    "State update adayÄ±nÄ±n kaynak tÃ¼rÃ¼ bilinmiyor.",
                    nameof(StateUpdateValidator)
                ));
            }

            var distance = safeCurrent.Pose.Distance3DTo(safeCandidate.Pose);
            if (distance > 25.0)
            {
                issues.Add(StateValidationIssue.Warning(
                    "STATE_CANDIDATE_LARGE_POSITION_DELTA",
                    $"State update adayÄ± bÃ¼yÃ¼k konum farkÄ± oluÅŸturuyor. Distance={distance:F2} m",
                    nameof(StateUpdateValidator)
                ));
            }

            return issues;
        }

        public bool HasBlockingIssue(IReadOnlyList<StateValidationIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (issue.Severity == StateValidationSeverity.Error ||
                    issue.Severity == StateValidationSeverity.Critical)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
