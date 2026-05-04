using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Göreve duyarlı varış planlayıcı.
    ///
    /// Şimdilik scenario-owned görevler için one-way thruster varsayımıyla çalışır.
    /// İleride VehicleControlCapability / thruster geometry bilgisi buraya beslenecek.
    /// </summary>
    public static class AdaptiveArrivalPlanner
    {
        public static ArrivalPlan PlanScenarioArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
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
            distanceM = SafeNonNegative(distanceM);
            planarSpeedMps = SafeNonNegative(planarSpeedMps);
            forwardSpeedMps = Safe(forwardSpeedMps);
            baseThrottleNorm = Math.Clamp(Safe(baseThrottleNorm), 0.0, maxApproachThrottleNorm);

            double absHeading = Math.Abs(Safe(headingErrorDeg));

            double stoppingDistance = ComputeStoppingDistance(
                planarSpeedMps,
                estimatedCoastDecelMps2);

            double desiredSpeed = Math.Sqrt(
                Math.Max(
                    0.0,
                    2.0 * Math.Max(0.001, estimatedCoastDecelMps2) * Math.Max(0.0, distanceM - 0.50)
                )
            );

            desiredSpeed = Math.Max(desiredSpeedFloorMps, desiredSpeed);

            bool overshootLikely =
                distanceM <= Math.Max(captureRadiusM, stoppingDistance + 0.25) &&
                planarSpeedMps > maxCaptureSpeedMps;

            if (absHeading > 100.0)
            {
                return new ArrivalPlan(
                    Phase: ArrivalPhase.TurnAlign,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: Math.Min(creepThrottleNorm, maxApproachThrottleNorm),
                    AllowReverseSurge: false,
                    ShouldCoast: false,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    Reason: "SCENARIO_TURN_ALIGN"
                );
            }

            if (distanceM <= captureRadiusM)
            {
                if (planarSpeedMps > maxCaptureSpeedMps)
                {
                    return new ArrivalPlan(
                        Phase: ArrivalPhase.CaptureCoast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: 0.0,
                        AllowReverseSurge: false,
                        ShouldCoast: true,
                        ShouldHold: false,
                        IsOvershootLikely: true,
                        Reason: "SCENARIO_CAPTURE_COAST"
                    );
                }

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Capture,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: creepThrottleNorm,
                    AllowReverseSurge: false,
                    ShouldCoast: false,
                    ShouldHold: true,
                    IsOvershootLikely: false,
                    Reason: "SCENARIO_CAPTURE_CREEP"
                );
            }

            if (distanceM <= coastRadiusM && planarSpeedMps > desiredSpeed)
            {
                return new ArrivalPlan(
                    Phase: ArrivalPhase.Coast,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: 0.0,
                    AllowReverseSurge: false,
                    ShouldCoast: true,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    Reason: "SCENARIO_NEAR_COAST"
                );
            }

            if (distanceM <= slowRadiusM)
            {
                double distanceFactor = Math.Clamp(distanceM / Math.Max(0.001, slowRadiusM), 0.0, 1.0);
                double speedError = desiredSpeed - Math.Max(0.0, forwardSpeedMps);

                if (speedError <= -0.05)
                {
                    return new ArrivalPlan(
                        Phase: ArrivalPhase.Coast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: 0.0,
                        AllowReverseSurge: false,
                        ShouldCoast: true,
                        ShouldHold: false,
                        IsOvershootLikely: overshootLikely,
                        Reason: "SCENARIO_APPROACH_COAST"
                    );
                }

                double speedFactor = Math.Clamp(speedError / Math.Max(0.1, desiredSpeed), 0.0, 1.0);
                double throttle = baseThrottleNorm * distanceFactor * speedFactor;
                throttle = Math.Max(creepThrottleNorm, throttle);
                throttle = Math.Clamp(throttle, 0.0, maxApproachThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Approach,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: throttle,
                    AllowReverseSurge: false,
                    ShouldCoast: false,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    Reason: "SCENARIO_APPROACH"
                );
            }

            return new ArrivalPlan(
                Phase: ArrivalPhase.Cruise,
                DesiredSpeedMps: desiredSpeed,
                StoppingDistanceM: stoppingDistance,
                ThrottleNorm: Math.Clamp(baseThrottleNorm, 0.0, maxApproachThrottleNorm),
                AllowReverseSurge: false,
                ShouldCoast: false,
                ShouldHold: false,
                IsOvershootLikely: false,
                Reason: "SCENARIO_NAVIGATE"
            );
        }

        private static double ComputeStoppingDistance(double speedMps, double decelMps2)
        {
            speedMps = SafeNonNegative(speedMps);
            decelMps2 = Math.Max(0.001, SafeNonNegative(decelMps2));
            return (speedMps * speedMps) / (2.0 * decelMps2);
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