using System;

namespace Hydronom.Core.Control
{
    /// <summary>
    /// Araç fiziksel kontrol kabiliyet profilidir.
    ///
    /// Bu model planner/decision/control katmanına şunu söyler:
    /// - Araç reverse yapabilir mi?
    /// - Yanal kuvvet üretme otoritesi var mı?
    /// - Yaw moment otoritesi yeterli mi?
    /// - Kontrol mimarisi omnidirectional mı, surface-boat gibi underactuated mı?
    ///
    /// Kritik amaç:
    /// Decision ve PlatformControlModule, actuator'ın üretemeyeceği wrench'i istememelidir.
    /// </summary>
    public sealed record VehicleCapabilityProfile(
        bool HasAnyThruster,
        bool HasReverseAuthority,
        bool IsUnderactuatedSurfaceVehicle,
        bool CanGenerateLateralForce,
        bool CanGenerateYawMoment,
        double PositiveSurgeAuthority,
        double NegativeSurgeAuthority,
        double PositiveSwayAuthority,
        double NegativeSwayAuthority,
        double PositiveYawAuthority,
        double NegativeYawAuthority,
        double ReverseConfidence,
        double LateralConfidence,
        double YawConfidence,
        string Summary)
    {
        public static VehicleCapabilityProfile Unknown { get; } = new(
            HasAnyThruster: false,
            HasReverseAuthority: false,
            IsUnderactuatedSurfaceVehicle: true,
            CanGenerateLateralForce: false,
            CanGenerateYawMoment: false,
            PositiveSurgeAuthority: 0.0,
            NegativeSurgeAuthority: 0.0,
            PositiveSwayAuthority: 0.0,
            NegativeSwayAuthority: 0.0,
            PositiveYawAuthority: 0.0,
            NegativeYawAuthority: 0.0,
            ReverseConfidence: 0.0,
            LateralConfidence: 0.0,
            YawConfidence: 0.0,
            Summary: "UNKNOWN_CAPABILITY");

        public VehicleCapabilityProfile Sanitized()
        {
            static double S(double value)
            {
                return double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
            }

            var posSurge = S(PositiveSurgeAuthority);
            var negSurge = S(NegativeSurgeAuthority);
            var posSway = S(PositiveSwayAuthority);
            var negSway = S(NegativeSwayAuthority);
            var posYaw = S(PositiveYawAuthority);
            var negYaw = S(NegativeYawAuthority);

            var reverseConfidence = Clamp01(ReverseConfidence);
            var lateralConfidence = Clamp01(LateralConfidence);
            var yawConfidence = Clamp01(YawConfidence);

            return this with
            {
                PositiveSurgeAuthority = posSurge,
                NegativeSurgeAuthority = negSurge,
                PositiveSwayAuthority = posSway,
                NegativeSwayAuthority = negSway,
                PositiveYawAuthority = posYaw,
                NegativeYawAuthority = negYaw,
                ReverseConfidence = reverseConfidence,
                LateralConfidence = lateralConfidence,
                YawConfidence = yawConfidence,
                Summary = string.IsNullOrWhiteSpace(Summary)
                    ? "CAPABILITY"
                    : Summary.Trim()
            };
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }
    }
}