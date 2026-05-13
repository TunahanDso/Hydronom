using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    public sealed partial class PlatformControlModule
    {
        private readonly record struct SecondaryControlAxes(
            double Fy,
            double Fz,
            double Tx,
            double Ty
        );

        private SecondaryControlAxes StabilizeSecondaryAxes(
            ControlIntent intent,
            VehicleState state,
            double dt)
        {
            var velocityBody = state.Orientation.WorldToBody(state.LinearVelocity);

            var fy = -Safe(velocityBody.Y) * 2.0;

            var tx =
                ((-state.Orientation.RollDeg) * RollKp -
                 Safe(state.AngularVelocity.X) * RollKd) *
                MaxTxNm;

            var ty =
                ((-state.Orientation.PitchDeg) * PitchKp -
                 Safe(state.AngularVelocity.Y) * PitchKd) *
                MaxTyNm;

            var fz = 0.0;

            if (intent.HoldDepth)
            {
                var depthError = intent.DesiredDepthMeters - state.Position.Z;
                var verticalVelocity = Safe(velocityBody.Z);

                fz =
                    (depthError * DepthKp - verticalVelocity * DepthKd) *
                    MaxFzN;
            }

            return new SecondaryControlAxes(
                Fy: Math.Clamp(fy, -MaxFyN, MaxFyN),
                Fz: Math.Clamp(fz, -MaxFzN, MaxFzN),
                Tx: Math.Clamp(tx, -MaxTxNm, MaxTxNm),
                Ty: Math.Clamp(ty, -MaxTyNm, MaxTyNm)
            );
        }
    }
}