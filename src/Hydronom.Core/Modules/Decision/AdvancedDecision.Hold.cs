using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private DecisionResult HoldPosition(
            Vec3 target,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            Vec3 posErrWorld = new Vec3(
                target.X - state.Position.X,
                target.Y - state.Position.Y,
                0.0
            );

            Vec3 posErrBody = state.Orientation.WorldToBody(posErrWorld);
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);

            bool linearSlow =
                Math.Abs(velB.X) < CloseLinearVelThresh &&
                Math.Abs(velB.Y) < CloseLinearVelThresh &&
                Math.Abs(state.Velocity.Z) < CloseLinearVelThresh;

            bool angularSlow =
                Math.Abs(state.AngularVelocity.X) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Y) < CloseAngVelThreshDeg &&
                Math.Abs(state.AngularVelocity.Z) < CloseAngVelThreshDeg;

            if (nav.DistanceXY < CloseEnoughRadiusM && linearSlow && angularSlow)
            {
                ResetHeaveIntegral();

                if (_frozenHoldHeadingDeg is null)
                    FreezeHoldHeading(state.Orientation.YawDeg);

                var zero = DecisionCommand.Zero;
                return new DecisionResult(
                    RawCommand: zero,
                    OutputCommand: zero,
                    Reason: "HOLD_SETTLED_ZERO_WRENCH",
                    ThrottleNorm: 0.0,
                    RudderNorm: 0.0
                );
            }

            double posScale = Math.Clamp(nav.DistanceXY / StopRadiusM, 0.2, 1.0);

            double fxNorm = (posErrBody.X * HoldKp - velB.X * HoldKd) * posScale;
            double fyNorm = (posErrBody.Y * HoldKp - velB.Y * HoldKd) * posScale;

            fxNorm = Math.Clamp(fxNorm, -1.0, 1.0);
            fyNorm = Math.Clamp(fyNorm, -1.0, 1.0);

            double fx = fxNorm * MaxFxN;
            double fy = fyNorm * MaxFyN;

            if (_frozenHoldHeadingDeg is null)
                FreezeHoldHeading(state.Orientation.YawDeg);

            double desiredYawDeg = _frozenHoldHeadingDeg!.Value;

            double dyaw = Normalize(desiredYawDeg - state.Orientation.YawDeg);
            double yawRate = state.AngularVelocity.Z;
            double yawScale = posScale;

            double tzNorm = (dyaw * YawKp - yawRate * YawKd) * yawScale;
            tzNorm = Math.Clamp(tzNorm, -1.0, 1.0);
            double tz = tzNorm * MaxTzNm;

            double rollRate = state.AngularVelocity.X;
            double pitchRate = state.AngularVelocity.Y;

            double txNorm = (-state.Orientation.RollDeg) * AttKp - rollRate * AttKd;
            double tyNorm = (-state.Orientation.PitchDeg) * AttKp - pitchRate * AttKd;

            txNorm = Math.Clamp(txNorm, -1.0, 1.0);
            tyNorm = Math.Clamp(tyNorm, -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            double fzNorm = ComputeHeave(target.Z, state, dt) * posScale;
            double fz = fzNorm * MaxFzN;

            var raw = new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            );

            var output = ScaleCommand(raw);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: "HOLD_POSITION",
                ThrottleNorm: fxNorm,
                RudderNorm: tzNorm
            );
        }

        private double ComputeHeave(double targetZ, VehicleState state, double dt)
        {
            double error = Safe(targetZ - state.Position.Z);
            double vz = Safe(state.Velocity.Z);

            if (Math.Abs(error) < 0.03)
                _heaveIntegral *= 0.90;

            _heaveIntegral += error * HeaveKi * dt;
            _heaveIntegral = Math.Clamp(_heaveIntegral, -HeaveImax, HeaveImax);

            double fzNorm = error * HeaveKp + _heaveIntegral - vz * HeaveKd;
            return Math.Clamp(Safe(fzNorm), -MaxHeaveNorm, MaxHeaveNorm);
        }

        private void ResetHeaveIntegral()
        {
            _heaveIntegral = 0.0;
        }

        private void EnterHoldMode(VehicleState state)
        {
            if (_isHoldingPosition)
                return;

            FreezeHoldHeading(state.Orientation.YawDeg);
            _isHoldingPosition = true;
        }

        private void ExitHoldMode()
        {
            _frozenHoldHeadingDeg = null;
            _isHoldingPosition = false;
        }

        private void FreezeHoldHeading(double yawDeg)
        {
            _frozenHoldHeadingDeg = Normalize(yawDeg);
        }
    }
}