using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Göreve duyarlı varış planlayıcı.
    ///
    /// v3:
    /// - Sadece scenario görevleri için değil, tüm görevler için arrival profile üretir.
    /// - Dinamik durma mesafesini aktif kullanır.
    /// - Capture alanına hızlı girildiğinde coast veya aktif fren davranışı üretir.
    /// - Hedef arkaya düştüğünde / heading çok büyüdüğünde overshoot recovery üretir.
    /// - Tek yönlü thruster görevlerinde negatif surge istemez.
    /// - İki yönlü/reverse destekli görevlerde kontrollü negatif surge frenine izin verir.
    /// - İleride VehicleControlCapability / lookahead bilgisi alacak şekilde ayrık tutulmuştur.
    /// </summary>
    public static class AdaptiveArrivalPlanner
    {
        public static ArrivalPlan PlanMissionArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm,
            double maxReverseThrottleNorm,
            bool allowReverseSurge,
            bool strictCapture,
            string reasonPrefix)
        {
            distanceM = SafeNonNegative(distanceM);
            planarSpeedMps = SafeNonNegative(planarSpeedMps);
            forwardSpeedMps = Safe(forwardSpeedMps);
            targetBodyX = Safe(targetBodyX);

            baseThrottleNorm = Math.Clamp(Safe(baseThrottleNorm), 0.0, maxApproachThrottleNorm);

            double absHeading = Math.Abs(Safe(headingErrorDeg));
            double decel = Math.Max(0.001, SafeNonNegative(estimatedDecelMps2));

            double stoppingDistance = ComputeStoppingDistance(planarSpeedMps, decel);
            double desiredSpeed = ComputeDesiredSpeed(
                distanceM,
                captureRadiusM,
                desiredSpeedFloorMps,
                decel);

            double speedError = desiredSpeed - planarSpeedMps;

            bool targetLikelyBehind =
                absHeading >= 115.0 ||
                targetBodyX < -0.05 ||
                forwardSpeedMps < -0.05;

            bool cannotStopComfortably =
                distanceM <= stoppingDistance + Math.Max(0.35, captureRadiusM * 0.35);

            bool overshootLikely =
                targetLikelyBehind ||
                (cannotStopComfortably && planarSpeedMps > maxCaptureSpeedMps);

            if (overshootLikely && distanceM <= Math.Max(slowRadiusM, stoppingDistance + captureRadiusM))
            {
                double recoveryThrottle = ComputeRecoveryThrottle(
                    allowReverseSurge,
                    forwardSpeedMps,
                    maxReverseThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.OvershootRecovery,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: recoveryThrottle,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: recoveryThrottle >= 0.0,
                    ShouldHold: false,
                    IsOvershootLikely: true,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: 1.20,
                    Reason: $"{reasonPrefix}_OVERSHOOT_RECOVERY"
                );
            }

            if (absHeading > 95.0)
            {
                double turnAlignThrottle = allowReverseSurge && forwardSpeedMps > maxCaptureSpeedMps
                    ? -Math.Min(maxReverseThrottleNorm * 0.35, maxReverseThrottleNorm)
                    : Math.Min(creepThrottleNorm, maxApproachThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.TurnAlign,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: turnAlignThrottle,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: turnAlignThrottle >= 0.0 && turnAlignThrottle <= creepThrottleNorm,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: 1.08,
                    Reason: $"{reasonPrefix}_TURN_ALIGN"
                );
            }

            if (distanceM <= captureRadiusM)
            {
                if (planarSpeedMps > maxCaptureSpeedMps)
                {
                    double captureThrottle = allowReverseSurge
                        ? -ComputeBrakeThrottle(planarSpeedMps, maxCaptureSpeedMps, maxReverseThrottleNorm)
                        : 0.0;

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.CaptureCoast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: captureThrottle,
                        AllowReverseSurge: allowReverseSurge,
                        ShouldCoast: captureThrottle >= 0.0,
                        ShouldHold: false,
                        IsOvershootLikely: true,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: strictCapture ? 0.70 : 0.80,
                        Reason: captureThrottle < 0.0
                            ? $"{reasonPrefix}_CAPTURE_ACTIVE_BRAKE"
                            : $"{reasonPrefix}_CAPTURE_COAST"
                    );
                }

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Capture,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: creepThrottleNorm,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: false,
                    ShouldHold: true,
                    IsOvershootLikely: false,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: strictCapture ? 0.65 : 0.80,
                    Reason: $"{reasonPrefix}_CAPTURE_CREEP"
                );
            }

            if (cannotStopComfortably)
            {
                double stoppingThrottle = allowReverseSurge
                    ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, maxReverseThrottleNorm)
                    : 0.0;

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Coast,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: stoppingThrottle,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: stoppingThrottle >= 0.0,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: strictCapture ? 0.90 : 0.95,
                    Reason: stoppingThrottle < 0.0
                        ? $"{reasonPrefix}_STOPPING_DISTANCE_BRAKE"
                        : $"{reasonPrefix}_STOPPING_DISTANCE_COAST"
                );
            }

            if (distanceM <= coastRadiusM && planarSpeedMps > desiredSpeed)
            {
                double nearThrottle = allowReverseSurge
                    ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, maxReverseThrottleNorm * 0.75)
                    : 0.0;

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Coast,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: nearThrottle,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: nearThrottle >= 0.0,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: 0.90,
                    Reason: nearThrottle < 0.0
                        ? $"{reasonPrefix}_NEAR_ACTIVE_BRAKE"
                        : $"{reasonPrefix}_NEAR_COAST"
                );
            }

            if (distanceM <= slowRadiusM)
            {
                if (speedError <= -0.05)
                {
                    double approachBrakeThrottle = allowReverseSurge
                        ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, maxReverseThrottleNorm * 0.55)
                        : 0.0;

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.Coast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: approachBrakeThrottle,
                        AllowReverseSurge: allowReverseSurge,
                        ShouldCoast: approachBrakeThrottle >= 0.0,
                        ShouldHold: false,
                        IsOvershootLikely: overshootLikely,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: 0.95,
                        Reason: approachBrakeThrottle < 0.0
                            ? $"{reasonPrefix}_APPROACH_ACTIVE_BRAKE"
                            : $"{reasonPrefix}_APPROACH_COAST"
                    );
                }

                double distanceFactor = Math.Clamp(distanceM / Math.Max(0.001, slowRadiusM), 0.0, 1.0);
                double speedFactor = Math.Clamp(speedError / Math.Max(0.1, desiredSpeed), 0.0, 1.0);

                double approachThrottle = baseThrottleNorm * distanceFactor * speedFactor;
                approachThrottle = Math.Max(creepThrottleNorm, approachThrottle);
                approachThrottle = Math.Clamp(approachThrottle, 0.0, maxApproachThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Approach,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: approachThrottle,
                    AllowReverseSurge: allowReverseSurge,
                    ShouldCoast: false,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: 0.98,
                    Reason: $"{reasonPrefix}_APPROACH"
                );
            }

            return new ArrivalPlan(
                Phase: ArrivalPhase.Cruise,
                DesiredSpeedMps: desiredSpeed,
                StoppingDistanceM: stoppingDistance,
                ThrottleNorm: Math.Clamp(baseThrottleNorm, 0.0, maxApproachThrottleNorm),
                AllowReverseSurge: allowReverseSurge,
                ShouldCoast: false,
                ShouldHold: false,
                IsOvershootLikely: false,
                SpeedErrorMps: speedError,
                RecommendedYawGain: 1.0,
                Reason: $"{reasonPrefix}_NAVIGATE"
            );
        }

        public static ArrivalPlan PlanScenarioArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedCoastDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm)
        {
            return PlanMissionArrival(
                distanceM: distanceM,
                planarSpeedMps: planarSpeedMps,
                forwardSpeedMps: forwardSpeedMps,
                targetBodyX: targetBodyX,
                headingErrorDeg: headingErrorDeg,
                baseThrottleNorm: baseThrottleNorm,
                captureRadiusM: captureRadiusM,
                slowRadiusM: slowRadiusM,
                coastRadiusM: coastRadiusM,
                maxCaptureSpeedMps: maxCaptureSpeedMps,
                desiredSpeedFloorMps: desiredSpeedFloorMps,
                estimatedDecelMps2: estimatedCoastDecelMps2,
                creepThrottleNorm: creepThrottleNorm,
                maxApproachThrottleNorm: maxApproachThrottleNorm,
                maxReverseThrottleNorm: 0.0,
                allowReverseSurge: false,
                strictCapture: true,
                reasonPrefix: "SCENARIO"
            );
        }

        private static double ComputeDesiredSpeed(
            double distanceM,
            double captureRadiusM,
            double desiredSpeedFloorMps,
            double decelMps2)
        {
            double brakingDistance = Math.Max(0.0, distanceM - captureRadiusM * 0.65);

            double desired = Math.Sqrt(
                Math.Max(
                    0.0,
                    2.0 * Math.Max(0.001, decelMps2) * brakingDistance
                )
            );

            return Math.Max(desiredSpeedFloorMps, desired);
        }

        private static double ComputeStoppingDistance(double speedMps, double decelMps2)
        {
            speedMps = SafeNonNegative(speedMps);
            decelMps2 = Math.Max(0.001, SafeNonNegative(decelMps2));
            return (speedMps * speedMps) / (2.0 * decelMps2);
        }

        private static double ComputeBrakeThrottle(
            double currentSpeedMps,
            double desiredSpeedMps,
            double maxReverseThrottleNorm)
        {
            currentSpeedMps = SafeNonNegative(currentSpeedMps);
            desiredSpeedMps = SafeNonNegative(desiredSpeedMps);
            maxReverseThrottleNorm = SafeNonNegative(maxReverseThrottleNorm);

            if (maxReverseThrottleNorm <= 0.0)
                return 0.0;

            if (currentSpeedMps <= desiredSpeedMps)
                return 0.0;

            double error = currentSpeedMps - desiredSpeedMps;
            double k = Math.Clamp(error / Math.Max(0.1, desiredSpeedMps + 0.5), 0.0, 1.0);

            return Math.Clamp(k * maxReverseThrottleNorm, 0.0, maxReverseThrottleNorm);
        }

        private static double ComputeRecoveryThrottle(
            bool allowReverseSurge,
            double forwardSpeedMps,
            double maxReverseThrottleNorm)
        {
            if (!allowReverseSurge)
                return 0.0;

            if (forwardSpeedMps <= 0.10)
                return 0.0;

            double k = Math.Clamp(forwardSpeedMps / 1.50, 0.0, 1.0);
            return -Math.Clamp(k * maxReverseThrottleNorm, 0.0, maxReverseThrottleNorm);
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }
    }
}