using System;
using Hydronom.Core.State.Models;

namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// Hydronom'un authoritative state gÃ¼venlik kapÄ±sÄ±.
    ///
    /// Her VehicleOperationalState gÃ¼ncellemesi bu yÃ¶neticiden geÃ§melidir.
    /// BÃ¶ylece Python, external pose, replay, sim truth veya hatalÄ± fusion Ã§Ä±ktÄ±sÄ±
    /// ana araÃ§ state'ini izinsiz ezemez.
    /// </summary>
    public sealed class StateAuthorityManager
    {
        private StateAuthorityPolicy _policy;

        public StateAuthorityManager(StateAuthorityPolicy? policy = null)
        {
            _policy = (policy ?? StateAuthorityPolicy.CSharpPrimary).Sanitized();
        }

        public StateAuthorityPolicy Policy => _policy;

        public void UpdatePolicy(StateAuthorityPolicy policy)
        {
            _policy = policy.Sanitized();
        }

        public StateUpdateResult Evaluate(
            VehicleOperationalState currentState,
            StateUpdateCandidate candidate,
            DateTime? utcNow = null
        )
        {
            var now = utcNow ?? DateTime.UtcNow;
            var policy = _policy.Sanitized();
            var current = currentState.Sanitized();
            var safeCandidate = candidate.Sanitized();

            double candidateAgeMs = Math.Max(0.0, (now - safeCandidate.TimestampUtc).TotalMilliseconds);
            double positionDeltaMeters = current.Pose.Distance3DTo(safeCandidate.Pose);

            double dtSeconds = Math.Max(1e-6, (safeCandidate.TimestampUtc - current.TimestampUtc).TotalSeconds);
            double impliedSpeedMps = positionDeltaMeters / dtSeconds;

            double yawDeltaDeg = NormalizeDeg(safeCandidate.Pose.YawDeg - current.Pose.YawDeg);
            double impliedYawRateDegSec = Math.Abs(yawDeltaDeg) / dtSeconds;

            if (!safeCandidate.IsFinite)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedInvalidData,
                    "Aday state sayÄ±sal olarak geÃ§ersiz.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (!string.Equals(current.VehicleId, safeCandidate.VehicleId, StringComparison.OrdinalIgnoreCase))
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedVehicleMismatch,
                    "Aday state farklÄ± bir araÃ§ kimliÄŸine ait.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (!IsSourceAllowed(policy, safeCandidate.SourceKind))
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedSourceNotAuthorized,
                    "Aday state kaynaÄŸÄ± mevcut authority policy tarafÄ±ndan yetkili deÄŸil.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (candidateAgeMs > policy.MaxStateAgeMs)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedStaleTimestamp,
                    "Aday state timestamp deÄŸeri Ã§ok eski.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (policy.RequireFrameMatch &&
                !string.Equals(current.FrameId, safeCandidate.FrameId, StringComparison.OrdinalIgnoreCase))
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedFrameMismatch,
                    "Aday state frame bilgisi mevcut authoritative frame ile uyumsuz.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (safeCandidate.Confidence < policy.MinConfidence)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedLowConfidence,
                    "Aday state gÃ¼ven skoru minimum eÅŸik altÄ±nda.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (positionDeltaMeters > policy.MaxTeleportDistanceMeters)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedTeleportDetected,
                    "Aday state ani konum sÄ±Ã§ramasÄ± oluÅŸturuyor.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (impliedSpeedMps > policy.MaxPlausibleSpeedMps)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedPhysicallyImpossible,
                    "Aday state fiziksel olarak aÅŸÄ±rÄ± yÃ¼ksek hÄ±z gerektiriyor.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            if (impliedYawRateDegSec > policy.MaxPlausibleYawRateDegSec)
            {
                return Reject(
                    current,
                    safeCandidate,
                    StateUpdateDecision.RejectedPhysicallyImpossible,
                    "Aday state fiziksel olarak aÅŸÄ±rÄ± yÃ¼ksek yaw rate gerektiriyor.",
                    candidateAgeMs,
                    positionDeltaMeters,
                    impliedSpeedMps,
                    yawDeltaDeg,
                    impliedYawRateDegSec
                );
            }

            var next = new VehicleOperationalState(
                VehicleId: current.VehicleId,
                TimestampUtc: safeCandidate.TimestampUtc,
                Pose: safeCandidate.Pose,
                Twist: safeCandidate.Twist,
                Attitude: safeCandidate.Attitude,
                SourceKind: safeCandidate.SourceKind,
                AuthorityMode: policy.Mode,
                Confidence: safeCandidate.Confidence,
                FrameId: safeCandidate.FrameId,
                QualitySummary: safeCandidate.Reason,
                TraceId: safeCandidate.TraceId
            ).Sanitized();

            return StateUpdateResult.Accept(
                before: current,
                after: next,
                candidate: safeCandidate,
                reason: "Aday state authority policy tarafÄ±ndan kabul edildi.",
                candidateAgeMs: candidateAgeMs,
                positionDeltaMeters: positionDeltaMeters,
                impliedSpeedMps: impliedSpeedMps,
                yawDeltaDeg: yawDeltaDeg,
                impliedYawRateDegSec: impliedYawRateDegSec
            );
        }

        private static StateUpdateResult Reject(
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
            return StateUpdateResult.Reject(
                current,
                candidate,
                decision,
                reason,
                candidateAgeMs,
                positionDeltaMeters,
                impliedSpeedMps,
                yawDeltaDeg,
                impliedYawRateDegSec
            );
        }

        private static bool IsSourceAllowed(StateAuthorityPolicy policy, VehicleStateSourceKind source)
        {
            if (source == VehicleStateSourceKind.PythonBackup)
                return policy.AllowPythonBackupAuthority && policy.Mode == StateAuthorityMode.PythonBackup;

            if (source == VehicleStateSourceKind.PythonCompareOnly)
                return false;

            if (source == VehicleStateSourceKind.PhysicsTruth)
                return policy.Mode == StateAuthorityMode.Simulation &&
                       policy.AllowPhysicsTruthAsAuthorityInSimulation;

            if (policy.Mode == StateAuthorityMode.Replay &&
                source == VehicleStateSourceKind.ReplayEstimate)
                return true;

            return policy.ExplicitAllowedSources.Contains(source);
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}
