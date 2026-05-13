using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    public sealed partial class PlatformControlModule
    {
        /*
         * Trajectory-aware navigation control.
         *
         * Bu controller artık sadece "hedefe doğru hızlan" mantığıyla çalışmaz.
         * Planner/trajectory tarafından verilen:
         * - TargetPosition
         * - TargetHeadingDeg
         * - DesiredForwardSpeedMps
         * - RiskLevel
         *
         * referanslarını birlikte kullanır.
         *
         * Temel güvenlik:
         * - Heading error büyükse ileri kuvvet ciddi kısılır.
         * - Yaw rate yüksekse ileri kuvvet kısılır.
         * - Turn-align fazında araç önce burnunu trajectory heading'e oturtur.
         * - Yaw damping P kontrolünden bağımsız güçlü biçimde uygulanır.
         * - Lateral error gövde ekseninde sınırlı Fy ile bastırılır.
         */
        private ControlOutput Navigate(
            ControlIntent intent,
            VehicleState state,
            double dt,
            bool avoidanceMode)
        {
            var target = SanitizeVec(intent.TargetPosition);

            var dx = Safe(target.X - state.Position.X);
            var dy = Safe(target.Y - state.Position.Y);

            var distance = Math.Sqrt(dx * dx + dy * dy);

            var geometricHeadingDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            var targetHeadingDeg = double.IsFinite(intent.TargetHeadingDeg)
                ? NormalizeDeg(intent.TargetHeadingDeg)
                : NormalizeDeg(geometricHeadingDeg);

            var headingErrorDeg = NormalizeDeg(targetHeadingDeg - state.Orientation.YawDeg);

            var velocityBody = state.Orientation.WorldToBody(state.LinearVelocity);
            var targetBody = state.Orientation.WorldToBody(new Vec3(dx, dy, 0.0));

            var forwardSpeed = Safe(velocityBody.X);
            var lateralSpeed = Safe(velocityBody.Y);
            var yawRateDeg = Safe(state.AngularVelocity.Z);

            var absHeadingError = Math.Abs(headingErrorDeg);
            var absYawRate = Math.Abs(yawRateDeg);

            var desiredSpeed = ResolveTrajectoryDesiredSpeed(
                intent,
                distance,
                absHeadingError,
                absYawRate,
                avoidanceMode);

            var headingGate = ComputeHeadingSpeedGate(absHeadingError);
            var yawRateGate = ComputeYawRateSpeedGate(absYawRate);
            var distanceGate = ComputeDistanceSpeedGate(distance);
            var riskGate = ComputeRiskSpeedGate(intent.RiskLevel);

            var combinedSpeedGate = Math.Clamp(
                headingGate * yawRateGate * distanceGate * riskGate,
                0.0,
                1.0);

            /*
             * Büyük heading hatasında ileri kuvveti neredeyse kesiyoruz.
             * Bu, "yanlış yöne bakarken gazla savrulma" problemini çözer.
             */
            var gatedDesiredSpeed = desiredSpeed * combinedSpeedGate;

            if (!intent.AllowReverse && gatedDesiredSpeed < 0.0)
                gatedDesiredSpeed = 0.0;

            var gatedSpeedError = gatedDesiredSpeed - forwardSpeed;

            var fx = gatedSpeedError * SpeedKp * MaxFxN;

            /*
             * Turn-align fazı:
             * Heading error çok büyükse ya da yaw rate çok yüksekse ileri thrust
             * pozitif yönde zorlanmaz. Araç önce dönüp hizalanır.
             */
            var turnAlign = absHeadingError >= 55.0 || absYawRate >= 95.0;

            if (turnAlign && fx > 0.0)
                fx *= 0.18;

            if (absHeadingError >= 85.0 && fx > 0.0)
                fx = 0.0;

            if (!intent.AllowReverse && fx < 0.0)
                fx = 0.0;

            /*
             * Yaw kontrol:
             * Açık PD:
             * - headingError pozitifse pozitif yaw moment ister.
             * - yawRate pozitifse damping negatif yönde çalışır.
             */
            var yawP = headingErrorDeg * HeadingKp;
            var yawD = -yawRateDeg * HeadingKd * 1.45;

            var yawCommandNorm = yawP + yawD;

            /*
             * Büyük heading hatasında minimum yaw otoritesi.
             * Küçük hata/yüksek yaw rate durumunda damping baskın kalır.
             */
            if (absHeadingError >= 20.0 && Math.Abs(yawCommandNorm) < 0.18)
                yawCommandNorm = headingErrorDeg >= 0.0 ? 0.18 : -0.18;

            if (absHeadingError >= 65.0 && Math.Abs(yawCommandNorm) < 0.35)
                yawCommandNorm = headingErrorDeg >= 0.0 ? 0.35 : -0.35;

            yawCommandNorm = Math.Clamp(yawCommandNorm, -1.0, 1.0);

            var tz = yawCommandNorm * MaxTzNm;

            /*
             * Lateral path correction:
             * Lookahead noktası gövde ekseninde sağ/sol tarafta kalıyorsa çok sınırlı
             * bir sway kuvveti üretir. Bu ana yönelim kontrolünü ezmez.
             */
            var lateralErrorBody = Safe(targetBody.Y);
            var fyPath = Math.Clamp(
                lateralErrorBody * 1.35 - lateralSpeed * 2.25,
                -MaxFyN * 0.35,
                MaxFyN * 0.35);

            var secondary = StabilizeSecondaryAxes(intent, state, dt);

            var command = ClampCommand(new DecisionCommand(
                fx: fx,
                fy: secondary.Fy + fyPath,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            ));

            var mode = avoidanceMode ? "AVOID_TRAJECTORY_CONTROL" : "TRAJECTORY_CONTROL";

            var reason =
                $"{mode} intent={intent.Kind} " +
                $"dist={distance:F2}m " +
                $"targetHead={targetHeadingDeg:F1}deg " +
                $"geoHead={geometricHeadingDeg:F1}deg " +
                $"headErr={headingErrorDeg:F1}deg " +
                $"yawRate={yawRateDeg:F1}degps " +
                $"v={forwardSpeed:F2}->{desiredSpeed:F2}/{gatedDesiredSpeed:F2}mps " +
                $"gate={combinedSpeedGate:F2} " +
                $"turnAlign={turnAlign} " +
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
            var target = SanitizeVec(intent.TargetPosition);

            var dx = Safe(target.X - state.Position.X);
            var dy = Safe(target.Y - state.Position.Y);

            var velocityBody = state.Orientation.WorldToBody(state.LinearVelocity);

            var targetWorld = new Vec3(dx, dy, 0.0);
            var targetBody = state.Orientation.WorldToBody(targetWorld);

            var fx = targetBody.X * 8.0 - velocityBody.X * 4.0;
            var fy = targetBody.Y * 8.0 - velocityBody.Y * 4.0;

            var headingErrorDeg = NormalizeDeg(intent.TargetHeadingDeg - state.Orientation.YawDeg);
            var yawRateDeg = Safe(state.AngularVelocity.Z);

            var yawCommandNorm =
                headingErrorDeg * HeadingKp -
                yawRateDeg * HeadingKd * 1.45;

            yawCommandNorm = Math.Clamp(yawCommandNorm, -1.0, 1.0);

            var tz = yawCommandNorm * MaxTzNm;

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

        private static double ResolveTrajectoryDesiredSpeed(
            ControlIntent intent,
            double distance,
            double absHeadingErrorDeg,
            double absYawRateDeg,
            bool avoidanceMode)
        {
            var maxSpeed = avoidanceMode ? 0.75 : 2.25;

            var desired = Math.Clamp(
                Safe(intent.DesiredForwardSpeedMps),
                intent.AllowReverse ? -1.25 : 0.0,
                maxSpeed);

            if (distance < 1.0)
                desired *= 0.25;
            else if (distance < 2.0)
                desired *= 0.45;
            else if (distance < 4.0)
                desired *= 0.70;

            if (absHeadingErrorDeg >= 85.0)
                desired = intent.AllowReverse ? Math.Min(desired, 0.0) : 0.0;
            else if (absHeadingErrorDeg >= 65.0)
                desired *= 0.20;
            else if (absHeadingErrorDeg >= 45.0)
                desired *= 0.38;
            else if (absHeadingErrorDeg >= 30.0)
                desired *= 0.62;

            if (absYawRateDeg >= 130.0)
                desired *= 0.15;
            else if (absYawRateDeg >= 95.0)
                desired *= 0.30;
            else if (absYawRateDeg >= 65.0)
                desired *= 0.55;

            if (intent.RiskLevel > 0.75)
                desired *= 0.35;
            else if (intent.RiskLevel > 0.4)
                desired *= 0.65;

            return desired;
        }

        private static double ComputeHeadingSpeedGate(double absHeadingErrorDeg)
        {
            if (absHeadingErrorDeg >= 90.0)
                return 0.0;

            if (absHeadingErrorDeg >= 70.0)
                return 0.12;

            if (absHeadingErrorDeg >= 55.0)
                return 0.25;

            if (absHeadingErrorDeg >= 40.0)
                return 0.45;

            if (absHeadingErrorDeg >= 25.0)
                return 0.70;

            return 1.0;
        }

        private static double ComputeYawRateSpeedGate(double absYawRateDeg)
        {
            if (absYawRateDeg >= 150.0)
                return 0.12;

            if (absYawRateDeg >= 110.0)
                return 0.25;

            if (absYawRateDeg >= 80.0)
                return 0.45;

            if (absYawRateDeg >= 55.0)
                return 0.70;

            return 1.0;
        }

        private static double ComputeDistanceSpeedGate(double distanceMeters)
        {
            if (!double.IsFinite(distanceMeters))
                return 0.0;

            if (distanceMeters <= 0.5)
                return 0.0;

            if (distanceMeters <= 1.0)
                return 0.25;

            if (distanceMeters <= 2.0)
                return 0.45;

            if (distanceMeters <= 4.0)
                return 0.70;

            return 1.0;
        }

        private static double ComputeRiskSpeedGate(double risk)
        {
            risk = Math.Clamp(Safe(risk), 0.0, 1.0);

            if (risk >= 0.85)
                return 0.25;

            if (risk >= 0.65)
                return 0.45;

            if (risk >= 0.40)
                return 0.70;

            return 1.0;
        }

        private static Vec3 SanitizeVec(Vec3 value)
        {
            return new Vec3(
                Safe(value.X),
                Safe(value.Y),
                Safe(value.Z)
            );
        }
    }
}