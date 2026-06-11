using System;
using Hydronom.Core.Control;
using Hydronom.Core.Domain;
using Hydronom.Core.Vehicles;

namespace Hydronom.Core.Vehicles.Actuation
{
    /// <summary>
    /// VehicleProfile / VehicleActuationProfile içinden kontrol katmanının anlayacağı
    /// VehicleCapabilityProfile modelini türetir.
    ///
    /// Bu sınıf çok önemli:
    /// Artık PlatformControlModule'a "elle" kabiliyet vermek yerine,
    /// araç profil paketinden otomatik kabiliyet üretilecek.
    /// </summary>
    public static class VehicleCapabilityProfileFactory
    {
        public static VehicleCapabilityProfile FromVehicleProfile(VehicleProfile? profile)
        {
            if (profile is null)
                return VehicleCapabilityProfile.Unknown;

            return FromActuationProfile(
                profile.Actuation,
                profile.PlatformKind,
                profile.Identity.DisplayName);
        }

        public static VehicleCapabilityProfile FromActuationProfile(
            VehicleActuationProfile? actuation,
            VehiclePlatformKind platformKind,
            string? displayName = null)
        {
            actuation = (actuation ?? VehicleActuationProfile.Empty).Sanitized();

            var thrusters = actuation.ActiveThrusters;

            if (!actuation.Enabled || thrusters.Count == 0)
            {
                return VehicleCapabilityProfile.Unknown with
                {
                    Summary = BuildSummary(displayName, platformKind, "NO_ACTIVE_THRUSTERS")
                };
            }

            var positiveSurge = 0.0;
            var negativeSurge = 0.0;

            var positiveSway = 0.0;
            var negativeSway = 0.0;

            var positiveHeave = 0.0;
            var negativeHeave = 0.0;

            var positiveYaw = 0.0;
            var negativeYaw = 0.0;

            foreach (var thruster in thrusters)
            {
                AccumulateThrusterAuthority(
                    thruster,
                    ref positiveSurge,
                    ref negativeSurge,
                    ref positiveSway,
                    ref negativeSway,
                    ref positiveHeave,
                    ref negativeHeave,
                    ref positiveYaw,
                    ref negativeYaw);
            }

            var isSurface = platformKind == VehiclePlatformKind.SurfaceVessel;
            var isUnderwater = platformKind is VehiclePlatformKind.UnderwaterVehicle or VehiclePlatformKind.MiniRov;

            var hasReverse = negativeSurge > 0.05;
            var canGenerateLateral = positiveSway > 0.05 || negativeSway > 0.05;
            var canGenerateYaw = positiveYaw > 0.05 || negativeYaw > 0.05;

            /*
             * Surface vessel varsayılanı:
             * - Çoğu tekne underactuated kabul edilir.
             * - Yanal kuvvet doğrudan üretmiyorsa controller lateral wrench istememeli.
             *
             * Underwater vehicle varsayılanı:
             * - Eğer yanal/dikey thruster varsa omnidirectional davranabilir.
             * - Yoksa yine capability düşük çıkar.
             */
            var isUnderactuatedSurface =
                isSurface &&
                !canGenerateLateral &&
                !actuation.HasVectoring;

            var lateralConfidence = ComputeConfidence(
                positiveSway,
                negativeSway,
                positiveSurge,
                negativeSurge);

            var yawConfidence = ComputeConfidence(
                positiveYaw,
                negativeYaw,
                positiveSurge,
                negativeSurge);

            var reverseConfidence = ComputeReverseConfidence(
                negativeSurge,
                positiveSurge);

            if (isUnderwater && (positiveHeave > 0.05 || negativeHeave > 0.05))
            {
                /*
                 * İlk sürüm VehicleCapabilityProfile içinde heave/dikey alan yok.
                 * Yine de summary içine işliyoruz.
                 * İleride VehicleCapabilityProfile v2'ye VerticalAuthority eklenecek.
                 */
            }

            return new VehicleCapabilityProfile(
                HasAnyThruster: true,
                HasReverseAuthority: hasReverse,
                IsUnderactuatedSurfaceVehicle: isUnderactuatedSurface,
                CanGenerateLateralForce: canGenerateLateral,
                CanGenerateYawMoment: canGenerateYaw,
                PositiveSurgeAuthority: positiveSurge,
                NegativeSurgeAuthority: negativeSurge,
                PositiveSwayAuthority: positiveSway,
                NegativeSwayAuthority: negativeSway,
                PositiveYawAuthority: positiveYaw,
                NegativeYawAuthority: negativeYaw,
                ReverseConfidence: reverseConfidence,
                LateralConfidence: lateralConfidence,
                YawConfidence: yawConfidence,
                Summary: BuildSummary(
                    displayName,
                    platformKind,
                    $"surge=+{positiveSurge:F1}/-{negativeSurge:F1}N " +
                    $"sway=+{positiveSway:F1}/-{negativeSway:F1}N " +
                    $"heave=+{positiveHeave:F1}/-{negativeHeave:F1}N " +
                    $"yaw=+{positiveYaw:F1}/-{negativeYaw:F1}Nm " +
                    $"rev={hasReverse} lat={canGenerateLateral} yaw={canGenerateYaw}")
            ).Sanitized();
        }

        private static void AccumulateThrusterAuthority(
            VehicleThrusterProfile thruster,
            ref double positiveSurge,
            ref double negativeSurge,
            ref double positiveSway,
            ref double negativeSway,
            ref double positiveHeave,
            ref double negativeHeave,
            ref double positiveYaw,
            ref double negativeYaw)
        {
            thruster = thruster.Sanitized();

            var dir = thruster.NormalizedDirection;
            var pos = thruster.PositionM;

            AddAxisAuthority(
                dir.X,
                thruster.MaxForwardThrustN,
                thruster.CanReverse ? thruster.MaxReverseThrustN : 0.0,
                ref positiveSurge,
                ref negativeSurge);

            AddAxisAuthority(
                dir.Y,
                thruster.MaxForwardThrustN,
                thruster.CanReverse ? thruster.MaxReverseThrustN : 0.0,
                ref positiveSway,
                ref negativeSway);

            AddAxisAuthority(
                dir.Z,
                thruster.MaxForwardThrustN,
                thruster.CanReverse ? thruster.MaxReverseThrustN : 0.0,
                ref positiveHeave,
                ref negativeHeave);

            /*
             * Yaw moment yaklaşık hesabı:
             * torqueZ = r_x * F_y - r_y * F_x
             *
             * Bu, gövde koordinatında basit ama çok faydalı bir ilk tahmindir.
             */
            var forwardForce = new Vec3(
                dir.X * thruster.MaxForwardThrustN,
                dir.Y * thruster.MaxForwardThrustN,
                dir.Z * thruster.MaxForwardThrustN);

            var yawForward = pos.X * forwardForce.Y - pos.Y * forwardForce.X;
            AddSignedAuthority(yawForward, ref positiveYaw, ref negativeYaw);

            if (thruster.CanReverse && thruster.MaxReverseThrustN > 0.0)
            {
                var reverseForce = new Vec3(
                    -dir.X * thruster.MaxReverseThrustN,
                    -dir.Y * thruster.MaxReverseThrustN,
                    -dir.Z * thruster.MaxReverseThrustN);

                var yawReverse = pos.X * reverseForce.Y - pos.Y * reverseForce.X;
                AddSignedAuthority(yawReverse, ref positiveYaw, ref negativeYaw);
            }
        }

        private static void AddAxisAuthority(
            double directionComponent,
            double forwardThrust,
            double reverseThrust,
            ref double positive,
            ref double negative)
        {
            if (!double.IsFinite(directionComponent))
                return;

            var component = Math.Clamp(directionComponent, -1.0, 1.0);

            var forwardContribution = component * SafePositive(forwardThrust);
            AddSignedAuthority(forwardContribution, ref positive, ref negative);

            if (reverseThrust > 0.0)
            {
                var reverseContribution = -component * SafePositive(reverseThrust);
                AddSignedAuthority(reverseContribution, ref positive, ref negative);
            }
        }

        private static void AddSignedAuthority(
            double value,
            ref double positive,
            ref double negative)
        {
            if (!double.IsFinite(value))
                return;

            if (value >= 0.0)
                positive += value;
            else
                negative += Math.Abs(value);
        }

        private static double ComputeConfidence(
            double positive,
            double negative,
            double referencePositive,
            double referenceNegative)
        {
            var authority = Math.Min(SafePositive(positive), SafePositive(negative));
            var reference = Math.Max(
                Math.Max(SafePositive(referencePositive), SafePositive(referenceNegative)),
                1.0);

            return Math.Clamp(authority / reference, 0.0, 1.0);
        }

        private static double ComputeReverseConfidence(
            double negativeSurge,
            double positiveSurge)
        {
            var pos = Math.Max(SafePositive(positiveSurge), 1.0);
            return Math.Clamp(SafePositive(negativeSurge) / pos, 0.0, 1.0);
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }

        private static string BuildSummary(
            string? displayName,
            VehiclePlatformKind platformKind,
            string details)
        {
            var name = string.IsNullOrWhiteSpace(displayName)
                ? "Vehicle"
                : displayName.Trim();

            return $"{name} {platformKind} {details}".Trim();
        }
    }
}