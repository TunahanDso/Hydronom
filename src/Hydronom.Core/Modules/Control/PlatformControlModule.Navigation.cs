using System;
using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules.Control
{
    public sealed partial class PlatformControlModule
    {
        /*
         * Trajectory-aware navigation control.
         *
         * Paket-8H:
         * Obstacle-bypass / local-detour takip davranÄ±ÅŸÄ± tekne kinematiÄŸine gÃ¶re ÅŸekillendirildi.
         *
         * KÃ¶k problem:
         * Planner artÄ±k doÄŸru ÅŸekilde obstacle-bypass path seÃ§iyor fakat control katmanÄ±
         * lookahead/local-detour noktasÄ±nÄ± takip ederken speed-error yÃ¼zÃ¼nden negatif Fx Ã¼retiyordu.
         * Tekne bu yÃ¼zden bypass noktasÄ±na ileri yay Ã§izerek gitmek yerine, fren/geri/yan/yaw
         * karÄ±ÅŸÄ±mÄ±yla dubanÄ±n yanÄ±nda sÃ¼rÃ¼nÃ¼yordu.
         *
         * Yeni davranÄ±ÅŸ:
         * - obstacle-bypass / local-detour / detour reason gÃ¶rÃ¼lÃ¼rse bypass-follow mode aÃ§Ä±lÄ±r.
         * - bypass-follow modunda reverse surge yasaklanÄ±r.
         * - yÃ¼ksek heading error olsa bile kÃ¼Ã§Ã¼k pozitif forward-flow korunur.
         * - lateral Fy sÄ±nÄ±rlandÄ±rÄ±lÄ±r; tekne yanlamasÄ±na hedef kovalamaz.
         * - yaw moment saturasyona daha az gider; araÃ§ Ã¶nce akÄ±ÅŸla dÃ¶nerek bypass noktasÄ±na yaklaÅŸÄ±r.
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

            var geometryEscapeRecoveryMode = IsGeometryEscapeRecoveryIntent(
                intent,
                avoidanceMode);

            var bypassFollowMode =
                !geometryEscapeRecoveryMode &&
                IsBypassFollowIntent(
                    intent,
                    avoidanceMode);

            var desiredSpeed = ResolveTrajectoryDesiredSpeed(
                intent,
                avoidanceMode,
                bypassFollowMode);

            var speedLimit = ComputeOptimalSpeedLimit(
                intent,
                distance,
                absHeadingError,
                absYawRate,
                avoidanceMode,
                bypassFollowMode);

            /*
             * Paket-8H:
             * Bypass takipte reverse surge yasaktÄ±r.
             *
             * Sebep:
             * local-detour ileri/yan tarafta iken speed error negatif kalabiliyor ve Fx tersine dÃ¶nÃ¼yor.
             * Tekne bu durumda bypass rotasÄ±nÄ± takip etmek yerine obstacle yanÄ±nda debeleniyor.
             */
            var allowReverseForNavigation =
                intent.AllowReverse &&
                !bypassFollowMode;

            var gatedDesiredSpeed = Math.Clamp(
                desiredSpeed,
                allowReverseForNavigation ? -speedLimit : 0.0,
                speedLimit);

            if (!allowReverseForNavigation && gatedDesiredSpeed < 0.0)
                gatedDesiredSpeed = 0.0;

            if (bypassFollowMode && distance > 0.75)
            {
                /*
                 * Bypass rotasÄ±nda tamamen sÄ±fÄ±r speed, aracÄ±n yaw saturasyonunda dÃ¶nÃ¼p kalmasÄ±na
                 * sebep oluyor. KÃ¼Ã§Ã¼k pozitif akÄ±ÅŸ ÅŸart.
                 */
                var minimumBypassSpeed = ResolveMinimumBypassSpeed(
                    absHeadingError,
                    absYawRate,
                    intent.RiskLevel);

                gatedDesiredSpeed = Math.Max(
                    gatedDesiredSpeed,
                    Math.Min(minimumBypassSpeed, speedLimit));
            }

            var gatedSpeedError = gatedDesiredSpeed - forwardSpeed;

            var feedForwardFx = ComputeSpeedFeedForwardFx(gatedDesiredSpeed);

            var fx =
                feedForwardFx +
                gatedSpeedError * SpeedKp * MaxFxN;

            var turnAlign = absHeadingError >= 55.0 || absYawRate >= 95.0;

            if (bypassFollowMode)
            {
                /*
                 * Bypass takipte ileri akÄ±ÅŸ tamamen Ã¶ldÃ¼rÃ¼lmez.
                 * BÃ¼yÃ¼k heading hatasÄ±nda bile tekne kÃ¼Ã§Ã¼k bir yay Ã§izerek dÃ¶nmelidir.
                 */
                if (turnAlign && fx > 0.0)
                    fx *= 0.45;

                if (absHeadingError >= 100.0 && fx > 0.0)
                    fx *= 0.35;

                var minimumForwardFx = ResolveMinimumBypassForwardFx(
                    distance,
                    absHeadingError,
                    absYawRate,
                    intent.RiskLevel);

                if (distance > 0.75 && fx < minimumForwardFx)
                    fx = minimumForwardFx;

                /*
                 * Reverse surge bypass takipte kesin kapalÄ±.
                 */
                if (fx < 0.0)
                    fx = minimumForwardFx;
            }
            else
            {
                /*
                 * Eski stabil davranÄ±ÅŸ:
                 * Heading error Ã§ok bÃ¼yÃ¼kse ya da yaw rate Ã§ok yÃ¼ksekse ileri thrust azaltÄ±lÄ±r.
                 */
                if (turnAlign && fx > 0.0)
                    fx *= 0.18;

                if (absHeadingError >= 85.0 && fx > 0.0)
                    fx = 0.0;

                if (!intent.AllowReverse && fx < 0.0)
                    fx = 0.0;
            }

            /*
             * Yaw kontrol:
             * AÃ§Ä±k PD:
             * - headingError pozitifse pozitif yaw moment ister.
             * - yawRate pozitifse damping negatif yÃ¶nde Ã§alÄ±ÅŸÄ±r.
             */
            var yawP = headingErrorDeg * HeadingKp;
            var yawD = -yawRateDeg * HeadingKd * 1.45;

            var yawCommandNorm = yawP + yawD;

            if (absHeadingError >= 20.0 && Math.Abs(yawCommandNorm) < 0.18)
                yawCommandNorm = headingErrorDeg >= 0.0 ? 0.18 : -0.18;

            if (absHeadingError >= 65.0 && Math.Abs(yawCommandNorm) < 0.35)
                yawCommandNorm = headingErrorDeg >= 0.0 ? 0.35 : -0.35;

            yawCommandNorm = Math.Clamp(yawCommandNorm, -1.0, 1.0);

            if (bypassFollowMode)
            {
                /*
                 * Rudder/yaw saturasyonu bypass sÄ±rasÄ±nda tekneyi olduÄŸu yerde dÃ¶ndÃ¼rÃ¼yor.
                 * Biraz yaw otoritesi kalacak ama ileri akÄ±ÅŸla beraber ark Ã§izilecek.
                 */
                var yawLimit = ResolveBypassYawLimit(
                    absHeadingError,
                    absYawRate,
                    intent.RiskLevel);

                yawCommandNorm = Math.Clamp(
                    yawCommandNorm,
                    -yawLimit,
                    yawLimit);
            }

            var tz = yawCommandNorm * MaxTzNm;

            /*
             * Lateral path correction:
             * Lookahead noktasÄ± gÃ¶vde ekseninde saÄŸ/sol tarafta kalÄ±yorsa sÄ±nÄ±rlÄ± sway Ã¼retir.
             *
             * Paket-8H:
             * Bypass takipte lateral kuvvet daha da sÄ±nÄ±rlandÄ±rÄ±lÄ±r.
             * Aksi halde araÃ§ local-detour noktasÄ±nÄ± yanlayarak kovalamaya Ã§alÄ±ÅŸÄ±yor.
             */
            var lateralErrorBody = Safe(targetBody.Y);

            var fyLimitRatio = bypassFollowMode
                ? 0.20
                : 0.35;

            var fyPath = Math.Clamp(
                lateralErrorBody * 1.35 - lateralSpeed * 2.25,
                -MaxFyN * fyLimitRatio,
                MaxFyN * fyLimitRatio);

            var secondary = StabilizeSecondaryAxes(intent, state, dt);

            var rawCommand = new DecisionCommand(
                fx: fx,
                fy: secondary.Fy + fyPath,
                fz: secondary.Fz,
                tx: secondary.Tx,
                ty: secondary.Ty,
                tz: tz
            );

            var command = ApplyCapabilityLimits(
                rawCommand,
                capability,
                avoidanceMode,
                intent.AllowReverse);

            var mode = geometryEscapeRecoveryMode
                ? "GEOMETRY_ESCAPE_RECOVERY_CONTROL"
                : bypassFollowMode
                    ? "BYPASS_TRAJECTORY_CONTROL"
                    : avoidanceMode
                        ? "AVOID_TRAJECTORY_CONTROL"
                        : "TRAJECTORY_CONTROL";

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
                $"bypassFollow={bypassFollowMode} " +
                $"reverseNav={allowReverseForNavigation} " +
                $"targetBody=({targetBody.X:F2},{targetBody.Y:F2}) " +
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

            var command = ApplyCapabilityLimits(
                rawCommand,
                capability,
                avoidanceMode: false,
                allowReverse: intent.AllowReverse);

            return new ControlOutput(
                command,
                "HOLD_CONTROL",
                $"HOLD posErr=({dx:F2},{dy:F2}) " +
                $"headErr={headingErrorDeg:F1} " +
                $"cap={capability.Summary} " +
                $"src={intent.Reason}");
        }

        private static bool IsGeometryEscapeRecoveryIntent(
            ControlIntent intent,
            bool avoidanceMode)
        {
            if (!avoidanceMode)
                return false;

            var reason = intent.Reason ?? string.Empty;

            return ContainsIgnoreCase(reason, "GEOM_ESCAPE_RECOVERY");
        }

        private static bool IsBypassFollowIntent(
            ControlIntent intent,
            bool avoidanceMode)
        {
            if (!avoidanceMode)
                return false;

            var reason = intent.Reason ?? string.Empty;
            var kind = intent.Kind.ToString();

            return
                ContainsIgnoreCase(reason, "obstacle-bypass") ||
                ContainsIgnoreCase(reason, "local-detour") ||
                ContainsIgnoreCase(reason, "BYPASS") ||
                ContainsIgnoreCase(reason, "detour") ||
                ContainsIgnoreCase(kind, "Avoid");
        }

        private static double ResolveMinimumBypassSpeed(
            double absHeadingErrorDeg,
            double absYawRateDeg,
            double riskLevel)
        {
            var risk = Math.Clamp(Safe(riskLevel), 0.0, 1.0);

            var speed = 0.28;

            if (absHeadingErrorDeg >= 85.0)
                speed = 0.18;
            else if (absHeadingErrorDeg >= 65.0)
                speed = 0.22;

            if (absYawRateDeg >= 120.0)
                speed = Math.Min(speed, 0.16);
            else if (absYawRateDeg >= 85.0)
                speed = Math.Min(speed, 0.20);

            if (risk >= 0.85)
                speed = Math.Min(speed, 0.18);
            else if (risk >= 0.70)
                speed = Math.Min(speed, 0.22);

            return Math.Clamp(speed, 0.12, 0.35);
        }

        private static double ResolveMinimumBypassForwardFx(
            double distanceMeters,
            double absHeadingErrorDeg,
            double absYawRateDeg,
            double riskLevel)
        {
            if (!double.IsFinite(distanceMeters) || distanceMeters <= 0.75)
                return 0.0;

            var risk = Math.Clamp(Safe(riskLevel), 0.0, 1.0);

            var ratio = 0.105;

            if (absHeadingErrorDeg >= 90.0)
                ratio = 0.055;
            else if (absHeadingErrorDeg >= 70.0)
                ratio = 0.075;

            if (absYawRateDeg >= 120.0)
                ratio *= 0.55;
            else if (absYawRateDeg >= 85.0)
                ratio *= 0.72;

            if (risk >= 0.85)
                ratio *= 0.55;
            else if (risk >= 0.70)
                ratio *= 0.75;

            return Math.Clamp(
                MaxFxN * ratio,
                0.18,
                MaxFxN * 0.14);
        }

        private static double ResolveBypassYawLimit(
            double absHeadingErrorDeg,
            double absYawRateDeg,
            double riskLevel)
        {
            var risk = Math.Clamp(Safe(riskLevel), 0.0, 1.0);

            var limit = 0.68;

            if (absHeadingErrorDeg >= 90.0)
                limit = 0.48;
            else if (absHeadingErrorDeg >= 70.0)
                limit = 0.56;

            if (absYawRateDeg >= 120.0)
                limit = Math.Min(limit, 0.42);
            else if (absYawRateDeg >= 85.0)
                limit = Math.Min(limit, 0.52);

            if (risk >= 0.85)
                limit = Math.Min(limit, 0.46);
            else if (risk >= 0.70)
                limit = Math.Min(limit, 0.56);

            return Math.Clamp(limit, 0.35, 0.72);
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
            bool avoidanceMode,
            bool bypassFollowMode)
        {
            var maxSpeed = bypassFollowMode
                ? 0.85
                : avoidanceMode
                    ? 1.10
                    : 2.25;

            return Math.Clamp(
                Safe(intent.DesiredForwardSpeedMps),
                intent.AllowReverse && !bypassFollowMode ? -maxSpeed : 0.0,
                maxSpeed);
        }

        private static double ComputeOptimalSpeedLimit(
            ControlIntent intent,
            double distanceMeters,
            double absHeadingErrorDeg,
            double absYawRateDeg,
            bool avoidanceMode,
            bool bypassFollowMode)
        {
            var limit = bypassFollowMode
                ? 0.85
                : avoidanceMode
                    ? 1.10
                    : 2.25;

            if (!double.IsFinite(distanceMeters) || distanceMeters <= 0.15)
                return 0.0;

            if (distanceMeters <= 0.45)
                limit = Math.Min(limit, 0.22);
            else if (distanceMeters <= 0.80)
                limit = Math.Min(limit, 0.38);
            else if (distanceMeters <= 1.25)
                limit = Math.Min(limit, 0.58);

            if (bypassFollowMode)
            {
                /*
                 * Bypass sÄ±rasÄ±nda heading error speed'i Ã¶ldÃ¼rmez; sadece limitler.
                 * Tam sÄ±fÄ±r hÄ±z, local-detour takipte kÃ¶tÃ¼ davranÄ±yor.
                 */
                if (absHeadingErrorDeg >= 115.0)
                    limit = Math.Min(limit, 0.18);
                else if (absHeadingErrorDeg >= 95.0)
                    limit = Math.Min(limit, 0.25);
                else if (absHeadingErrorDeg >= 75.0)
                    limit = Math.Min(limit, 0.38);
                else if (absHeadingErrorDeg >= 55.0)
                    limit = Math.Min(limit, 0.55);
            }
            else
            {
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
            }

            if (absYawRateDeg >= 150.0)
                limit = Math.Min(limit, bypassFollowMode ? 0.20 : 0.25);
            else if (absYawRateDeg >= 110.0)
                limit = Math.Min(limit, bypassFollowMode ? 0.28 : 0.45);
            else if (absYawRateDeg >= 80.0)
                limit = Math.Min(limit, bypassFollowMode ? 0.42 : 0.75);
            else if (absYawRateDeg >= 55.0)
                limit = Math.Min(limit, bypassFollowMode ? 0.60 : 1.10);

            var risk = Math.Clamp(Safe(intent.RiskLevel), 0.0, 1.0);

            if (risk >= 0.95)
                limit = Math.Min(limit, bypassFollowMode ? 0.20 : 0.25);
            else if (risk >= 0.85)
                limit = Math.Min(limit, bypassFollowMode ? 0.28 : 0.45);
            else if (risk >= 0.70)
                limit = Math.Min(limit, bypassFollowMode ? 0.45 : 0.75);
            else if (risk >= 0.55)
                limit = Math.Min(limit, bypassFollowMode ? 0.65 : 1.05);

            return Math.Clamp(
                limit,
                0.0,
                bypassFollowMode ? 0.85 : avoidanceMode ? 1.10 : 2.25);
        }

        private static bool ContainsIgnoreCase(
            string value,
            string needle)
        {
            return value.Contains(
                needle,
                StringComparison.OrdinalIgnoreCase);
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