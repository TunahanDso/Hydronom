using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    /// <summary>
    /// Platform bağımsız yüksek frekanslı kontrol modülü.
    ///
    /// Decision artık doğrudan wrench üretmek zorunda değildir.
    /// Decision, ControlIntent üretir.
    /// Bu modül ise ControlIntent + VehicleState üzerinden gerçek DecisionCommand/wrench üretir.
    /// </summary>
    public sealed partial class PlatformControlModule : IControlModule
    {
        private const double MaxFxN = 24.0;
        private const double MaxFyN = 12.0;
        private const double MaxFzN = 35.0;

        private const double MaxTxNm = 5.0;
        private const double MaxTyNm = 7.0;
        private const double MaxTzNm = 8.0;

        private const double HeadingKp = 0.028;
        private const double HeadingKd = 0.010;

        private const double SpeedKp = 0.65;
        private const double SpeedKd = 0.12;

        private const double DepthKp = 0.85;
        private const double DepthKd = 0.22;

        private const double RollKp = 0.025;
        private const double RollKd = 0.008;

        private const double PitchKp = 0.025;
        private const double PitchKd = 0.008;

        public ControlOutput LastOutput { get; private set; } = ControlOutput.Zero;

        public ControlOutput Update(
            ControlIntent intent,
            VehicleState state,
            double dt)
        {
            intent ??= ControlIntent.Idle;
            state = state.Sanitized();
            dt = SanitizeDt(dt);

            var output = intent.Kind switch
            {
                ControlIntentKind.Idle => ControlOutput.Zero,

                ControlIntentKind.EmergencyStop => new ControlOutput(
                    DecisionCommand.Zero,
                    "ESTOP",
                    "EMERGENCY_STOP"),

                ControlIntentKind.Manual => ControlOutput.Zero,

                ControlIntentKind.HoldPosition => HoldPosition(intent, state, dt),

                ControlIntentKind.AvoidObstacle => Navigate(intent, state, dt, avoidanceMode: true),

                ControlIntentKind.Navigate => Navigate(intent, state, dt, avoidanceMode: false),

                _ => ControlOutput.Zero
            };

            LastOutput = output;
            return output;
        }

        private static double SanitizeDt(double dt)
        {
            if (!double.IsFinite(dt) || dt <= 1e-4)
                return 0.02;

            return Math.Min(dt, 0.25);
        }

        private static double Safe(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }

        private static DecisionCommand ClampCommand(DecisionCommand command)
        {
            return new DecisionCommand(
                fx: Math.Clamp(Safe(command.Fx), -MaxFxN, MaxFxN),
                fy: Math.Clamp(Safe(command.Fy), -MaxFyN, MaxFyN),
                fz: Math.Clamp(Safe(command.Fz), -MaxFzN, MaxFzN),
                tx: Math.Clamp(Safe(command.Tx), -MaxTxNm, MaxTxNm),
                ty: Math.Clamp(Safe(command.Ty), -MaxTyNm, MaxTyNm),
                tz: Math.Clamp(Safe(command.Tz), -MaxTzNm, MaxTzNm)
            );
        }
    }
}