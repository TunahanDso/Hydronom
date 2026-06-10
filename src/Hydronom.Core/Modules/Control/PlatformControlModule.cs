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
    ///
    /// Paket-6F:
    /// Bu modül artık vehicle capability aware çalışır.
    /// Tek yönlü ESC / underactuated surface vehicle gibi araçlarda üretilmesi fiziksel olarak
    /// mümkün olmayan yanal kuvvet, reverse surge ve aşırı yaw moment istekleri daha üst seviyede
    /// bastırılır. Böylece allocation katmanı "imkânsız wrench" ile boğulmaz.
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
        /*
         * Surge speed feed-forward:
         *
         * Pure P speed control settles below the requested speed because thrust
         * becomes zero as error approaches zero. Feed-forward estimates the force
         * needed to maintain the requested speed against nominal hull drag.
         */
        private const double SpeedLinearFeedForwardNPerMps = 4.00;
        private const double SpeedQuadraticFeedForwardNPerMps2 = 0.50;

        private const double DepthKp = 0.85;
        private const double DepthKd = 0.22;

        private const double RollKp = 0.025;
        private const double RollKd = 0.008;

        private const double PitchKp = 0.025;
        private const double PitchKd = 0.008;

        /// <summary>
        /// Runtime/Actuator katmanı tarafından güncellenebilecek araç kabiliyet profili.
        ///
        /// Default Unknown profili bilerek underactuated surface vehicle gibi temkinli davranır.
        /// Böylece bilgi yoksa bile kontrolcü omnidirectional/çift yönlü araç varsaymaz.
        /// </summary>
        public VehicleCapabilityProfile CapabilityProfile { get; private set; } =
            VehicleCapabilityProfile.Unknown;

        public ControlOutput LastOutput { get; private set; } = ControlOutput.Zero;

        public void SetCapabilityProfile(VehicleCapabilityProfile? profile)
        {
            CapabilityProfile = (profile ?? VehicleCapabilityProfile.Unknown).Sanitized();
        }

        public ControlOutput Update(
            ControlIntent intent,
            VehicleState state,
            double dt)
        {
            intent ??= ControlIntent.Idle;
            state = state.Sanitized();
            dt = SanitizeDt(dt);

            var capability = CapabilityProfile.Sanitized();

            var output = intent.Kind switch
            {
                ControlIntentKind.Idle => ControlOutput.Zero,

                ControlIntentKind.EmergencyStop => new ControlOutput(
                    DecisionCommand.Zero,
                    "ESTOP",
                    "EMERGENCY_STOP"),

                ControlIntentKind.Manual => ControlOutput.Zero,

                ControlIntentKind.HoldPosition => HoldPosition(intent, state, dt, capability),

                ControlIntentKind.AvoidObstacle => Navigate(intent, state, dt, avoidanceMode: true, capability),

                ControlIntentKind.Navigate => Navigate(intent, state, dt, avoidanceMode: false, capability),

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

        private static DecisionCommand ApplyCapabilityLimits(
            DecisionCommand command,
            VehicleCapabilityProfile capability,
            bool avoidanceMode)
        {
            capability = capability.Sanitized();

            var fx = Safe(command.Fx);
            var fy = Safe(command.Fy);
            var fz = Safe(command.Fz);
            var tx = Safe(command.Tx);
            var ty = Safe(command.Ty);
            var tz = Safe(command.Tz);

            if (!capability.HasReverseAuthority || capability.IsUnderactuatedSurfaceVehicle)
            {
                if (fx < 0.0)
                    fx = 0.0;
            }

            if (capability.IsUnderactuatedSurfaceVehicle || !capability.CanGenerateLateralForce)
            {
                /*
                 * Tek yönlü / underactuated yüzey araçta lateral wrench hayaldir.
                 * Tam sıfırlıyoruz ama çok küçük damping payı bırakıyoruz ki sim/sualtı profiline
                 * geçildiğinde davranış tamamen kırılmasın.
                 */
                var lateralScale = Math.Clamp(capability.LateralConfidence, 0.0, 0.20);
                fy *= lateralScale;
            }
            else
            {
                var lateralScale = Math.Clamp(capability.LateralConfidence, 0.25, 1.0);
                fy *= lateralScale;
            }

            if (!capability.CanGenerateYawMoment)
            {
                tz *= 0.20;
            }
            else
            {
                var yawScale = Math.Clamp(capability.YawConfidence, 0.35, 1.0);
                tz *= yawScale;
            }

            if (capability.IsUnderactuatedSurfaceVehicle)
            {
                /*
                 * Underactuated surface vehicle politikası:
                 * - Turn-align sırasında tamamen gazı kesmek yerine küçük pozitif ileri akış bırakılabilir.
                 * - Ancak çok riskli avoidance modunda aşırı thrust da verilmez.
                 */
                if (avoidanceMode)
                    fx = Math.Clamp(fx, 0.0, MaxFxN * 0.45);
                else
                    fx = Math.Clamp(fx, 0.0, MaxFxN);
            }

            return ClampCommand(new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: fz,
                tx: tx,
                ty: ty,
                tz: tz
            ));
        }

        private static string CapabilityReasonSuffix(VehicleCapabilityProfile capability)
        {
            capability = capability.Sanitized();

            return
                $"capUnder={capability.IsUnderactuatedSurfaceVehicle} " +
                $"rev={capability.HasReverseAuthority} " +
                $"latConf={capability.LateralConfidence:F2} " +
                $"yawConf={capability.YawConfidence:F2}";
        }
    }
}