using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private DecisionResult Avoid(
            Insights ins,
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            var advice = _currentAdvice.Sanitized();

            double left = SafeNonNegative(ins.ClearanceLeft, 0.0);
            double right = SafeNonNegative(ins.ClearanceRight, 0.0);

            double sideSign;
            if (Math.Abs(right - left) < 0.10)
            {
                sideSign = nav.HeadingErrorDeg >= 0.0 ? +1.0 : -1.0;
            }
            else
            {
                sideSign = right > left ? +1.0 : -1.0;
            }

            double clearanceMax = Math.Max(left, right);
            double clearanceMin = Math.Min(left, right);
            double clearanceBalance = (clearanceMax - clearanceMin) / Math.Max(0.5, clearanceMax);

            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            double throttleNorm = 0.10;

            if (clearanceMin < 1.0)
                throttleNorm = 0.02;
            else if (clearanceMin < 2.0)
                throttleNorm = 0.06;

            throttleNorm *= advice.ThrottleScale;

            if (advice.RequireSlowMode)
                throttleNorm *= 0.55;

            if (advice.ForceCoast)
                throttleNorm = 0.0;

            double rudderNorm =
                (0.50 + 0.35 * Math.Clamp(clearanceBalance, 0.0, 1.0)) *
                sideSign;

            rudderNorm *= Math.Clamp(advice.YawAggressionScale, 0.25, 2.0);
            rudderNorm *= 1.0 + urgency * 0.35;
            rudderNorm = Math.Clamp(rudderNorm, -0.95, 0.95);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            string reason = AppendAdviceReason(
                $"AVOID side={(sideSign > 0 ? "right" : "left")} clearL={left:F2} clearR={right:F2}",
                advice);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: reason,
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );
        }

        private DecisionResult NavigateToTarget(
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            double absDelta = Math.Abs(nav.HeadingErrorDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);
            bool scenarioOwnedTask = task.IsExternallyCompleted;

            var advice = _currentAdvice.Sanitized();

            double rudderNorm = ComputeNavigationRudder(
                nav,
                gainMultiplier: advice.YawAggressionScale);

            if (nav.DistanceXY < BrakeRadiusM)
            {
                double nearBrake = nav.HeadingErrorDeg * NearYawBrakeKp - nav.YawRateDeg * NearYawBrakeKd;
                rudderNorm = Math.Clamp(
                    nearBrake * advice.YawAggressionScale,
                    -1.0,
                    1.0);
            }

            double throttleNorm = ComputeApproachThrottle(nav.DistanceXY);

            throttleNorm *= HeadingScale(absDelta);
            throttleNorm *= HeadingThrottleGate(absDelta, absYawRate);

            if (absDelta >= NearTurnInPlaceDeg)
                throttleNorm = Math.Min(throttleNorm, 0.03);

            /*
             * Analysis tavsiyesi:
             * - ThrottleScale genel gazı azaltır.
             * - RequireSlowMode agresifliği düşürür.
             * - ForceCoast ileri itmeyi keser.
             */
            throttleNorm *= advice.ThrottleScale;

            if (advice.RequireSlowMode)
                throttleNorm *= 0.55;

            if (advice.ForceCoast)
                throttleNorm = Math.Min(throttleNorm, 0.0);

            var arrival = PlanMissionArrival(task, throttleNorm, nav, advice);

            throttleNorm = arrival.ThrottleNorm;
            string reason = arrival.Reason;

            rudderNorm = ComputeNavigationRudder(
                nav,
                gainMultiplier: arrival.RecommendedYawGain * advice.YawAggressionScale);

            if (arrival.Phase is ArrivalPhase.Capture or ArrivalPhase.CaptureCoast)
            {
                rudderNorm *= scenarioOwnedTask ? 0.75 : 0.85;
            }

            if (arrival.Phase == ArrivalPhase.OvershootRecovery)
            {
                rudderNorm = ComputeOvershootRecoveryRudder(nav);
                rudderNorm = Math.Clamp(
                    rudderNorm * advice.YawAggressionScale,
                    -1.0,
                    1.0);
            }

            if (advice.PreferSafeHeading && advice.ObstacleAvoidanceUrgency > 0.05)
            {
                rudderNorm = ApplySafeHeadingBias(
                    rudderNorm,
                    nav,
                    advice);
            }

            if (advice.RecommendHold && nav.PlanarSpeedMps <= 0.35)
            {
                throttleNorm = 0.0;
                reason = $"{reason}_ANALYSIS_HOLD_RECOMMENDED";
            }

            if (scenarioOwnedTask)
            {
                throttleNorm = Math.Clamp(
                    throttleNorm,
                    ScenarioMinThrottleNorm,
                    ScenarioMaxApproachThrottleNorm);
            }
            else
            {
                throttleNorm = Math.Clamp(
                    throttleNorm,
                    arrival.AllowReverseSurge ? -MaxReverseThrottleNorm : 0.0,
                    GeneralMaxApproachThrottleNorm);
            }

            if (advice.ForceCoast && throttleNorm > 0.0)
            {
                throttleNorm = 0.0;
                reason = $"{reason}_ANALYSIS_FORCE_COAST";
            }

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            var result = new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: AppendAdviceReason(reason, advice),
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );

            return ConstrainExternalScenarioCommandIfNeeded(task, result, reasonOverride: result.Reason);
        }

        private static double ComputeNavigationRudder(
            NavigationGeometry nav,
            double gainMultiplier)
        {
            double absDelta = Math.Abs(nav.HeadingErrorDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);

            if (absDelta <= RudderDeadbandDeg && absYawRate <= YawRateDeadbandDeg)
                return 0.0;

            double p = nav.HeadingErrorDeg * NavYawKp;
            double d = -nav.YawRateDeg * NavYawKd;

            return Math.Clamp((p + d) * Math.Clamp(gainMultiplier, 0.25, 2.0), -1.0, 1.0);
        }

        private static double ComputeOvershootRecoveryRudder(NavigationGeometry nav)
        {
            double p = nav.HeadingErrorDeg * NavYawKp * 1.35;
            double d = -nav.YawRateDeg * NavYawKd * 0.65;

            double rudder = p + d;

            if (Math.Abs(rudder) < 0.25)
                rudder = nav.HeadingErrorDeg >= 0.0 ? 0.25 : -0.25;

            return Math.Clamp(rudder, -1.0, 1.0);
        }

        private static double ApplySafeHeadingBias(
            double rudderNorm,
            NavigationGeometry nav,
            DecisionAdviceProfile advice)
        {
            /*
             * İlk v1 safe-heading bias:
             * Analysis "safe heading tercih et" diyorsa yaw tepkisi biraz daha kararlı hale gelir.
             *
             * İleride AdvancedAnalysis.LastReport.BestHeadingOffsetDeg doğrudan
             * DecisionContext ile buraya taşınacak ve gerçek güvenli koridor yönü kullanılacak.
             */
            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            if (urgency <= 0.0)
                return rudderNorm;

            double headingSign = nav.HeadingErrorDeg >= 0.0 ? 1.0 : -1.0;

            if (Math.Abs(rudderNorm) < 0.15)
                rudderNorm = 0.15 * headingSign;

            return Math.Clamp(
                rudderNorm * (1.0 + urgency * 0.35),
                -1.0,
                1.0);
        }

        private static string AppendAdviceReason(
            string reason,
            DecisionAdviceProfile advice)
        {
            if (string.IsNullOrWhiteSpace(advice.PrimaryReason) ||
                advice.PrimaryReason == "NEUTRAL")
            {
                return reason;
            }

            return $"{reason}_ADVICE_{advice.PrimaryReason}";
        }

        private static double ComputeApproachThrottle(double distanceM)
        {
            if (distanceM >= SlowRadiusM)
                return CruiseThrottleNorm;

            double k = (distanceM - StopRadiusM) / (SlowRadiusM - StopRadiusM);
            k = Math.Clamp(k, 0.0, 1.0);

            return MinApproachThrottleNorm + k * (CruiseThrottleNorm - MinApproachThrottleNorm);
        }

        private static NavigationGeometry ComputeNavigationGeometry(Vec3 target, VehicleState state)
        {
            double dx = Safe(target.X - state.Position.X);
            double dy = Safe(target.Y - state.Position.Y);

            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(dist))
                dist = 0.0;

            Vec3 toTargetWorld = new Vec3(dx, dy, 0.0);
            Vec3 toTargetBody = state.Orientation.WorldToBody(toTargetWorld);
            Vec3 velBody = state.Orientation.WorldToBody(state.Velocity);

            double targetHeading = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = Normalize(targetHeading - state.Orientation.YawDeg);

            double planarSpeed = Math.Sqrt(
                Safe(state.Velocity.X) * Safe(state.Velocity.X) +
                Safe(state.Velocity.Y) * Safe(state.Velocity.Y)
            );

            if (!double.IsFinite(planarSpeed))
                planarSpeed = 0.0;

            return new NavigationGeometry(
                DistanceXY: dist,
                TargetBody: toTargetBody,
                VelocityBody: velBody,
                HeadingErrorDeg: delta,
                ForwardSpeedMps: velBody.X,
                PlanarSpeedMps: planarSpeed,
                YawRateDeg: state.AngularVelocity.Z
            );
        }
    }
}