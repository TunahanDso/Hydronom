using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private DecisionCommand PlanarToRawWrench(
            double throttleNorm,
            double rudderNorm,
            TaskDefinition task,
            VehicleState state,
            double dt)
        {
            var secondary = ComputeSecondaryAxes(task, state, dt);

            double fx = Math.Clamp(throttleNorm, -1.0, 1.0) * MaxFxN;
            double tz = -Math.Clamp(rudderNorm, -1.0, 1.0) * MaxTzNm;

            return new DecisionCommand(
                fx: fx,
                fy: secondary.Fy,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            );
        }

        private static DecisionCommand ScaleCommand(DecisionCommand raw)
        {
            return new DecisionCommand(
                fx: Safe(raw.Fx) * GlobalEffortScale,
                fy: Safe(raw.Fy) * GlobalEffortScale,
                fz: Safe(raw.Fz) * GlobalEffortScale,
                tx: Safe(raw.Tx) * GlobalEffortScale,
                ty: Safe(raw.Ty) * GlobalEffortScale,
                tz: Safe(raw.Tz) * GlobalEffortScale
            );
        }

        private static DecisionResult ConstrainExternalScenarioCommandIfNeeded(
            TaskDefinition task,
            DecisionResult result,
            string? reasonOverride)
        {
            if (!task.IsExternallyCompleted)
                return result;

            var raw = ClampNegativeSurge(result.RawCommand);
            var output = ClampNegativeSurge(result.OutputCommand);

            double throttle = Math.Max(0.0, result.ThrottleNorm);
            string reason = reasonOverride ?? result.Reason;

            if (result.RawCommand.Fx < -1e-6 || result.OutputCommand.Fx < -1e-6)
                reason = $"{reason}_NO_REVERSE_SURGE";

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: reason,
                ThrottleNorm: throttle,
                RudderNorm: result.RudderNorm
            );
        }

        private static DecisionCommand ClampNegativeSurge(DecisionCommand command)
        {
            if (command.Fx >= 0.0)
                return command;

            return new DecisionCommand(
                fx: 0.0,
                fy: command.Fy,
                fz: command.Fz,
                tx: command.Tx,
                ty: command.Ty,
                tz: command.Tz
            );
        }

        private (double Fy, double Fz, double Tx, double Ty)
            ComputeSecondaryAxes(TaskDefinition task, VehicleState state, double dt)
        {
            Vec3 velB = state.Orientation.WorldToBody(state.Velocity);
            double vy = Safe(velB.Y);

            double fyNorm = -vy * SwayVelGain;
            fyNorm = Math.Clamp(fyNorm, -MaxSwayNorm, MaxSwayNorm);
            double fy = fyNorm * MaxFyN;

            double txNorm = (-state.Orientation.RollDeg) * AttKp
                            - state.AngularVelocity.X * AttKd;

            double tyNorm = (-state.Orientation.PitchDeg) * AttKp
                            - state.AngularVelocity.Y * AttKd;

            txNorm = Math.Clamp(Safe(txNorm), -1.0, 1.0);
            tyNorm = Math.Clamp(Safe(tyNorm), -1.0, 1.0);

            double tx = txNorm * MaxTxNm;
            double ty = tyNorm * MaxTyNm;

            double fz = 0.0;
            if (task.Target is Vec3 t3d)
            {
                double fzNorm = ComputeHeave(t3d.Z, state, dt);
                fz = fzNorm * MaxFzN;
            }
            else
            {
                ResetHeaveIntegral();
            }

            return (fy, fz, tx, ty);
        }
    }
}