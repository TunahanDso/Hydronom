using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    public sealed partial class PlatformControlModule
    {
        private ControlOutput Navigate(
            ControlIntent intent,
            VehicleState state,
            double dt,
            bool avoidanceMode)
        {
            var target = intent.TargetPosition;

            var dx = Safe(target.X - state.Position.X);
            var dy = Safe(target.Y - state.Position.Y);

            var distance = Math.Sqrt(dx * dx + dy * dy);

            var targetHeadingDeg = double.IsFinite(intent.TargetHeadingDeg)
                ? intent.TargetHeadingDeg
                : Math.Atan2(dy, dx) * 180.0 / Math.PI;

            var headingErrorDeg = NormalizeDeg(targetHeadingDeg - state.Orientation.YawDeg);

            var velocityBody = state.Orientation.WorldToBody(state.LinearVelocity);
            var forwardSpeed = Safe(velocityBody.X);
            var yawRateDeg = Safe(state.AngularVelocity.Z);

            var desiredSpeed = Math.Clamp(
                Safe(intent.DesiredForwardSpeedMps),
                intent.AllowReverse ? -2.0 : 0.0,
                avoidanceMode ? 0.8 : 3.0);

            if (intent.RiskLevel > 0.4)
                desiredSpeed *= 0.65;

            if (intent.RiskLevel > 0.75)
                desiredSpeed *= 0.35;

            var speedError = desiredSpeed - forwardSpeed;

            var fx = speedError * SpeedKp * MaxFxN;

            if (!intent.AllowReverse && fx < 0.0)
                fx = 0.0;

            var tz =
                -(headingErrorDeg * HeadingKp - yawRateDeg * HeadingKd) *
                MaxTzNm;

            var secondary = StabilizeSecondaryAxes(intent, state, dt);

            var command = ClampCommand(new DecisionCommand(
                fx: fx,
                fy: secondary.Fy,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            ));

            var mode = avoidanceMode ? "AVOID_CONTROL" : "NAV_CONTROL";

            var reason =
                $"{mode} intent={intent.Kind} " +
                $"dist={distance:F2}m " +
                $"headErr={headingErrorDeg:F1}deg " +
                $"v={forwardSpeed:F2}->{desiredSpeed:F2}mps " +
                $"risk={intent.RiskLevel:F2} " +
                $"src={intent.Reason}";

            return new ControlOutput(
                command,
                mode,
                reason);
        }

        private ControlOutput HoldPosition(
            ControlIntent intent,
            VehicleState state,
            double dt)
        {
            var target = intent.TargetPosition;

            var dx = Safe(target.X - state.Position.X);
            var dy = Safe(target.Y - state.Position.Y);

            var velocityBody = state.Orientation.WorldToBody(state.LinearVelocity);

            var targetWorld = new Vec3(dx, dy, 0.0);
            var targetBody = state.Orientation.WorldToBody(targetWorld);

            var fx = targetBody.X * 8.0 - velocityBody.X * 4.0;
            var fy = targetBody.Y * 8.0 - velocityBody.Y * 4.0;

            var headingErrorDeg = NormalizeDeg(intent.TargetHeadingDeg - state.Orientation.YawDeg);
            var yawRateDeg = Safe(state.AngularVelocity.Z);

            var tz =
                -(headingErrorDeg * HeadingKp - yawRateDeg * HeadingKd) *
                MaxTzNm;

            var secondary = StabilizeSecondaryAxes(intent, state, dt);

            var command = ClampCommand(new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            ));

            return new ControlOutput(
                command,
                "HOLD_CONTROL",
                $"HOLD posErr=({dx:F2},{dy:F2}) headErr={headingErrorDeg:F1} src={intent.Reason}");
        }
    }
}