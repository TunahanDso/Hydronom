using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    /// <summary>
    /// Platform baÃ„Å¸Ã„Â±msÃ„Â±z yÃƒÂ¼ksek frekanslÃ„Â± kontrol modÃƒÂ¼lÃƒÂ¼.
    ///
    /// Decision artÃ„Â±k doÃ„Å¸rudan wrench ÃƒÂ¼retmek zorunda deÃ„Å¸ildir.
    /// Decision, ControlIntent ÃƒÂ¼retir.
    /// Bu modÃƒÂ¼l ise ControlIntent + VehicleState ÃƒÂ¼zerinden gerÃƒÂ§ek DecisionCommand/wrench ÃƒÂ¼retir.
    ///
    /// Paket-6F:
    /// Bu modÃƒÂ¼l artÃ„Â±k vehicle capability aware ÃƒÂ§alÃ„Â±Ã…Å¸Ã„Â±r.
    /// Tek yÃƒÂ¶nlÃƒÂ¼ ESC / underactuated surface vehicle gibi araÃƒÂ§larda ÃƒÂ¼retilmesi fiziksel olarak
    /// mÃƒÂ¼mkÃƒÂ¼n olmayan yanal kuvvet, reverse surge ve aÃ…Å¸Ã„Â±rÃ„Â± yaw moment istekleri daha ÃƒÂ¼st seviyede
    /// bastÃ„Â±rÃ„Â±lÃ„Â±r. BÃƒÂ¶ylece allocation katmanÃ„Â± "imkÃƒÂ¢nsÃ„Â±z wrench" ile boÃ„Å¸ulmaz.
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
        /// Runtime/Actuator katmanÃ„Â± tarafÃ„Â±ndan gÃƒÂ¼ncellenebilecek araÃƒÂ§ kabiliyet profili.
        ///
        /// Default Unknown profili bilerek underactuated surface vehicle gibi temkinli davranÃ„Â±r.
        /// BÃƒÂ¶ylece bilgi yoksa bile kontrolcÃƒÂ¼ omnidirectional/ÃƒÂ§ift yÃƒÂ¶nlÃƒÂ¼ araÃƒÂ§ varsaymaz.
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
            bool avoidanceMode,
            bool allowReverse)
        {
            capability = capability.Sanitized();

            var fx = Safe(command.Fx);
            var fy = Safe(command.Fy);
            var fz = Safe(command.Fz);
            var tx = Safe(command.Tx);
            var ty = Safe(command.Ty);
            var tz = Safe(command.Tz);

            var reverseUsable =
                allowReverse &&
                capability.HasReverseAuthority &&
                capability.NegativeSurgeAuthority > 0.05 &&
                capability.ReverseConfidence > 0.05;

            if (!reverseUsable && fx < 0.0)
                fx = 0.0;

            if (!capability.CanGenerateLateralForce)
            {
                /*
                 * Platform bağımsız kural:
                 * Lateral otorite yoksa Fy isteme.
                 * Araç adı/tipe göre değil, capability profile'a göre karar verilir.
                 */
                fy = 0.0;
            }
            else
            {
                var lateralScale = Math.Clamp(capability.LateralConfidence, 0.0, 1.0);
                fy *= lateralScale;
            }

            if (!capability.CanGenerateYawMoment)
            {
                tz = 0.0;
            }
            else
            {
                var yawScale = Math.Clamp(capability.YawConfidence, 0.05, 1.0);
                tz *= yawScale;
            }

            if (capability.IsUnderactuatedSurfaceVehicle)
            {
                /*
                 * Underactuated araç politikası:
                 * Reverse sadece intent izin veriyor ve araçta gerçek reverse otoritesi varsa açılır.
                 * Normal navigation/bypass ters sürüşe zorlanmaz.
                 * Collision/no-go recovery gibi durumlarda reverse platform bağımsız şekilde kullanılabilir.
                 */
                var minFx = reverseUsable
                    ? -MaxFxN * (avoidanceMode ? 0.45 : 0.65)
                    : 0.0;

                var maxFx = avoidanceMode
                    ? MaxFxN * 0.45
                    : MaxFxN;

                fx = Math.Clamp(fx, minFx, maxFx);
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