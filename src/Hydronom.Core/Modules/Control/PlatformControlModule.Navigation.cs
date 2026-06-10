using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    public sealed partial class PlatformControlModule
    {
        /*
         * Trajectory-aware navigation control.
         *
         * Stabil rollback / compile-fix sürümü:
         * - 7A.3 liveScenarioFlow patch'i YOK.
         * - Boundary clamp YOK.
         * - Eski navigation davranışı korunur.
         * - Sadece yeni capability-aware PlatformControlModule.cs çağrılarına uyumlu imza vardır.
         *
         * PlatformControlModule.cs artık şunu çağırıyor:
         * Navigate(intent, state, dt, avoidanceMode, capability)
         * HoldPosition(intent, state, dt, capability)
         *
         * Bu dosya capability parametresini kabul eder ama eski stabil davranışı bozmamak için
         * navigation matematiğinde agresif yeni müdahale yapmaz.
         */
        private ControlOutput Navigate(
            ControlIntent intent,
            VehicleState state,
            double dt,
            bool avoidanceMode,
            VehicleCapabilityProfile capability)
        {
            capability = capability.Sanitized();

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
                avoidanceMode);

            var speedLimit = ComputeOptimalSpeedLimit(
                intent,
                distance,
                absHeadingError,
                absYawRate,
                avoidanceMode);

            /*
             * Constraint-optimal speed policy:
             *
             * Trajectory already computes the desired mission speed.
             * Control must not multiply it with fear gates again.
             *
             * We only cap it by physically/control-wise valid speed limits.
             */
            var gatedDesiredSpeed = Math.Clamp(
                desiredSpeed,
                intent.AllowReverse ? -speedLimit : 0.0,
                speedLimit);
            if (!intent.AllowReverse && gatedDesiredSpeed < 0.0)
                gatedDesiredSpeed = 0.0;

            var gatedSpeedError = gatedDesiredSpeed - forwardSpeed;

            /*
             * Constraint-optimal surge control:
             *
             * P control corrects speed error.
             * Feed-forward supplies the force needed to maintain requested speed.
             *
             * Without feed-forward, 0.75 m/s request settles around 0.59 m/s
             * because P thrust balances drag before the target speed is reached.
             */
            var feedForwardFx = ComputeSpeedFeedForwardFx(gatedDesiredSpeed);

            var fx =
                feedForwardFx +
                gatedSpeedError * SpeedKp * MaxFxN;

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

            var rawCommand = new DecisionCommand(
                fx: fx,
                fy: secondary.Fy + fyPath,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            );

            /*
             * Eski stabil davranışa en yakın yol:
             * Önce klasik ClampCommand.
             *
             * Not:
             * Capability parametresi imza uyumu için var. Burada agresif capability shaping
             * yapmıyoruz çünkü son testlerde davranışı bozan şey controller tarafındaki
             * ekstra müdahalelerdi.
             */
            var command = ClampCommand(rawCommand);

            var mode = avoidanceMode ? "AVOID_TRAJECTORY_CONTROL" : "TRAJECTORY_CONTROL";

            var reason =
                $"{mode} intent={intent.Kind} " +
                $"dist={distance:F2}m " +
                $"targetHead={targetHeadingDeg:F1}deg " +
                $"geoHead={geometricHeadingDeg:F1}deg " +
                $"headErr={headingErrorDeg:F1}deg " +
                $"yawRate={yawRateDeg:F1}degps " +
                $"v={forwardSpeed:F2}->{desiredSpeed:F2}/{gatedDesiredSpeed:F2}mps " +
                $"vLimit={speedLimit:F2} " +
                $"turnAlign={turnAlign} " +
                $"risk={intent.RiskLevel:F2} " +
                $"cap={capability.Summary} " +
                $"src={intent.Reason}";

            return new ControlOutput(
                command,
                mode,
                reason);
        }

        private ControlOutput HoldPosition(
            ControlIntent intent,
            VehicleState state,
            double dt,
            VehicleCapabilityProfile capability)
        {
            capability = capability.Sanitized();

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

            var rawCommand = new DecisionCommand(
                fx: fx,
                fy: fy,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            );

            var command = ClampCommand(rawCommand);

            return new ControlOutput(
                command,
                "HOLD_CONTROL",
                $"HOLD posErr=({dx:F2},{dy:F2}) " +
                $"headErr={headingErrorDeg:F1} " +
                $"cap={capability.Summary} " +
                $"src={intent.Reason}");
        }

        private static double ComputeSpeedFeedForwardFx(double desiredSpeedMps)
        {
            desiredSpeedMps = Safe(desiredSpeedMps);

            if (Math.Abs(desiredSpeedMps) <= 1e-6)
                return 0.0;

            return
                SpeedLinearFeedForwardNPerMps * desiredSpeedMps +
                SpeedQuadraticFeedForwardNPerMps2 * desiredSpeedMps * Math.Abs(desiredSpeedMps);
        }
        private static double ResolveTrajectoryDesiredSpeed(
            ControlIntent intent,
            bool avoidanceMode)
        {
            /*
             * Desired speed is a mission/trajectory request.
             * Do not downscale it here. Only clamp impossible values.
             */
            var maxSpeed = avoidanceMode ? 1.10 : 2.25;

            return Math.Clamp(
                Safe(intent.DesiredForwardSpeedMps),
                intent.AllowReverse ? -maxSpeed : 0.0,
                maxSpeed);
        }

        private static double ComputeOptimalSpeedLimit(
            ControlIntent intent,
            double distanceMeters,
            double absHeadingErrorDeg,
            double absYawRateDeg,
            bool avoidanceMode)
        {
            /*
             * This is not a "mode". It is the physical/control envelope:
             * go as fast as possible while still being able to steer, brake,
             * and respect risk/geometry constraints.
             */
            var limit = avoidanceMode ? 1.10 : 2.25;

            if (!double.IsFinite(distanceMeters) || distanceMeters <= 0.15)
                return 0.0;

            /*
             * Distance limit is only a braking envelope near the target.
             * No more 4m / 2m / 1m double slowdown.
             */
            if (distanceMeters <= 0.45)
                limit = Math.Min(limit, 0.22);
            else if (distanceMeters <= 0.80)
                limit = Math.Min(limit, 0.38);
            else if (distanceMeters <= 1.25)
                limit = Math.Min(limit, 0.58);

            /*
             * Heading envelope:
             * If we are badly misaligned, cap forward speed.
             * If heading is reasonable, keep mission speed alive.
             */
            if (absHeadingErrorDeg >= 105.0)
                limit = 0.0;
            else if (absHeadingErrorDeg >= 85.0)
                limit = Math.Min(limit, 0.25);
            else if (absHeadingErrorDeg >= 70.0)
                limit = Math.Min(limit, 0.45);
            else if (absHeadingErrorDeg >= 55.0)
                limit = Math.Min(limit, 0.70);
            else if (absHeadingErrorDeg >= 40.0)
                limit = Math.Min(limit, 1.00);

            /*
             * Yaw-rate envelope:
             * High yaw rate means the boat is already rotating hard,
             * so cap forward speed until it stabilizes.
             */
            if (absYawRateDeg >= 150.0)
                limit = Math.Min(limit, 0.25);
            else if (absYawRateDeg >= 110.0)
                limit = Math.Min(limit, 0.45);
            else if (absYawRateDeg >= 80.0)
                limit = Math.Min(limit, 0.75);
            else if (absYawRateDeg >= 55.0)
                limit = Math.Min(limit, 1.10);

            /*
             * Risk envelope:
             * Risk is a speed limit, not a multiplicative panic brake.
             */
            var risk = Math.Clamp(Safe(intent.RiskLevel), 0.0, 1.0);

            if (risk >= 0.95)
                limit = Math.Min(limit, 0.25);
            else if (risk >= 0.85)
                limit = Math.Min(limit, 0.45);
            else if (risk >= 0.70)
                limit = Math.Min(limit, 0.75);
            else if (risk >= 0.55)
                limit = Math.Min(limit, 1.05);

            return Math.Clamp(limit, 0.0, avoidanceMode ? 1.10 : 2.25);
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