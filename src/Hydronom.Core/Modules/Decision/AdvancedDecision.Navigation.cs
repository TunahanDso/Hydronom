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

            double throttleNorm = 0.10;

            if (clearanceMin < 1.0)
                throttleNorm = 0.02;
            else if (clearanceMin < 2.0)
                throttleNorm = 0.06;

            double rudderNorm = (0.50 + 0.35 * Math.Clamp(clearanceBalance, 0.0, 1.0)) * sideSign;
            rudderNorm = Math.Clamp(rudderNorm, -0.90, 0.90);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: $"AVOID side={(sideSign > 0 ? "right" : "left")} clearL={left:F2} clearR={right:F2}",
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

            double rudderNorm = 0.0;

            if (absDelta > RudderDeadbandDeg || absYawRate > YawRateDeadbandDeg)
            {
                double p = nav.HeadingErrorDeg * NavYawKp;
                double d = -nav.YawRateDeg * NavYawKd;
                rudderNorm = Math.Clamp(p + d, -1.0, 1.0);
            }

            if (nav.DistanceXY < BrakeRadiusM)
            {
                double nearBrake = nav.HeadingErrorDeg * NearYawBrakeKp - nav.YawRateDeg * NearYawBrakeKd;
                rudderNorm = Math.Clamp(nearBrake, -1.0, 1.0);
            }

            double throttleNorm = ComputeApproachThrottle(nav.DistanceXY);

            throttleNorm *= HeadingScale(absDelta);
            throttleNorm *= HeadingThrottleGate(absDelta, absYawRate);

            if (absDelta >= NearTurnInPlaceDeg)
                throttleNorm = Math.Min(throttleNorm, 0.03);

            string reason = "NAVIGATE";

            if (scenarioOwnedTask)
            {
                var arrival = PlanScenarioArrival(throttleNorm, nav);
                throttleNorm = arrival.ThrottleNorm;
                reason = arrival.Reason;
            }
            else
            {
                double desiredForwardSign = nav.TargetBody.X >= 0.0 ? 1.0 : -1.0;

                if (nav.DistanceXY < BrakeRadiusM && desiredForwardSign > 0.0)
                {
                    double brakeNorm = ComputeApproachBrakeNorm(nav.DistanceXY, nav.ForwardSpeedMps);
                    if (brakeNorm > 0.0)
                        throttleNorm = -brakeNorm;
                }

                if (absDelta > 70.0 && nav.ForwardSpeedMps > 0.35)
                {
                    double brakeAssist = Math.Clamp(
                        (nav.ForwardSpeedMps - 0.35) / 0.75,
                        0.0,
                        MaxReverseThrottleNorm * 0.7
                    );

                    throttleNorm = Math.Min(throttleNorm, 0.0);
                    throttleNorm -= brakeAssist;
                }

                throttleNorm = Math.Clamp(throttleNorm, -MaxReverseThrottleNorm, CruiseThrottleNorm);
            }

            if (scenarioOwnedTask)
                throttleNorm = Math.Clamp(throttleNorm, ScenarioMinThrottleNorm, ScenarioMaxApproachThrottleNorm);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            var result = new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: reason,
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );

            return ConstrainExternalScenarioCommandIfNeeded(task, result, reasonOverride: reason);
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