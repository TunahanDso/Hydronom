using System;
using System.Linq;
using Hydronom.Core.Control;

namespace Hydronom.Runtime.Actuators
{
    public sealed partial class ActuatorManager
    {
        public VehicleCapabilityProfile CapabilityProfile
        {
            get
            {
                lock (_stateLock)
                {
                    return BuildVehicleCapabilityProfile_NoLock().Sanitized();
                }
            }
        }

        public string CapabilitySummary => CapabilityProfile.Summary;

        public void LogCapabilityProfile(string reason = "startup")
        {
            var capability = CapabilityProfile;
            EnqueueLog($"[ActuatorManager] Vehicle capability ({reason}): {capability.Summary}");
        }

        private VehicleCapabilityProfile BuildVehicleCapabilityProfile_NoLock()
        {
            var authority = _authorityProfile;

            var hasAnyThruster = _thrusters.Count > 0;
            var healthyCount = _thrusters.Count(t => t.IsHealthy);
            var healthyReverseCount = _thrusters.Count(t => t.IsHealthy && t.CanReverse);
            var healthyOneWayCount = _thrusters.Count(t => t.IsHealthy && !t.CanReverse);

            var posSurge = Positive(authority.Fx);
            var negSurge = Negative(authority.Fx);

            var posSway = Positive(authority.Fy);
            var negSway = Negative(authority.Fy);

            var posYaw = Positive(authority.Tz);
            var negYaw = Negative(authority.Tz);

            var maxSurge = Math.Max(posSurge, negSurge);
            var maxSway = Math.Max(posSway, negSway);
            var maxYaw = Math.Max(posYaw, negYaw);

            var reverseConfidence = Ratio(
                negSurge,
                Math.Max(0.01, posSurge));

            var lateralConfidence = Ratio(
                Math.Min(posSway, negSway),
                Math.Max(0.01, maxSurge));

            var yawConfidence = Ratio(
                Math.Min(posYaw, negYaw),
                Math.Max(0.01, maxSurge));

            var hasReverseAuthority =
                healthyReverseCount > 0 &&
                negSurge > Math.Max(0.05, posSurge * 0.10);

            var canGenerateLateralForce =
                maxSway > Math.Max(0.05, maxSurge * 0.12);

            var canGenerateYawMoment =
                maxYaw > 0.05;

            var isUnderactuatedSurfaceVehicle =
                hasAnyThruster &&
                (
                    !hasReverseAuthority ||
                    !canGenerateLateralForce ||
                    lateralConfidence < 0.35
                );

            var summary =
                $"CAPABILITY thr={_thrusters.Count} healthy={healthyCount} " +
                $"oneWay={healthyOneWayCount} reverse={healthyReverseCount} " +
                $"surge+={posSurge:F2} surge-={negSurge:F2} " +
                $"sway+={posSway:F2} sway-={negSway:F2} " +
                $"yaw+={posYaw:F2} yaw-={negYaw:F2} " +
                $"revConf={reverseConfidence:F2} latConf={lateralConfidence:F2} yawConf={yawConfidence:F2} " +
                $"underactuated={isUnderactuatedSurfaceVehicle}";

            return new VehicleCapabilityProfile(
                HasAnyThruster: hasAnyThruster,
                HasReverseAuthority: hasReverseAuthority,
                IsUnderactuatedSurfaceVehicle: isUnderactuatedSurfaceVehicle,
                CanGenerateLateralForce: canGenerateLateralForce,
                CanGenerateYawMoment: canGenerateYawMoment,
                PositiveSurgeAuthority: posSurge,
                NegativeSurgeAuthority: negSurge,
                PositiveSwayAuthority: posSway,
                NegativeSwayAuthority: negSway,
                PositiveYawAuthority: posYaw,
                NegativeYawAuthority: negYaw,
                ReverseConfidence: reverseConfidence,
                LateralConfidence: lateralConfidence,
                YawConfidence: yawConfidence,
                Summary: summary);
        }

        private static double Positive(AxisAuthority axis)
        {
            return double.IsFinite(axis.Positive)
                ? Math.Max(0.0, axis.Positive)
                : 0.0;
        }

        private static double Negative(AxisAuthority axis)
        {
            return double.IsFinite(axis.Negative)
                ? Math.Max(0.0, axis.Negative)
                : 0.0;
        }

        private static double Ratio(double value, double reference)
        {
            if (!double.IsFinite(value) || !double.IsFinite(reference) || reference <= 1e-9)
                return 0.0;

            return Math.Clamp(value / reference, 0.0, 1.0);
        }
    }
}