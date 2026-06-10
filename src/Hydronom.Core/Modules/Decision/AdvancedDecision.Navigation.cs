using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private DecisionResult Avoid(
            Insights ins,
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            var advice = _currentAdvice.Sanitized();

            if (advice.HasPassableCorridor &&
                advice.SuppressObstaclePanic &&
                advice.PreferCorridorHeading)
            {
                return FollowPassableCorridor(
                    ins,
                    task,
                    state,
                    dt,
                    nav,
                    advice);
            }

            double left = SafeNonNegative(ins.ClearanceLeft, 0.0);
            double right = SafeNonNegative(ins.ClearanceRight, 0.0);

            double sideSign;
            if (Math.Abs(right - left) < 0.10)
            {
                /*
                 * Clearance kararsızsa hedefin istediği tarafa kör kırma.
                 * Örneğin WP2 sağ/aşağıdaysa ve sağ duba yakınsa aynı yöne saplanmasın diye
                 * hedef heading'in ters tarafına recovery açısı veriyoruz.
                 */
                sideSign = nav.HeadingErrorDeg >= 0.0 ? -1.0 : +1.0;
            }
            else
            {
                sideSign = right > left ? +1.0 : -1.0;
            }

            double clearanceMax = Math.Max(left, right);
            double clearanceMin = Math.Min(left, right);

            bool clearancesUseful =
                clearanceMax > 0.05 &&
                double.IsFinite(clearanceMax);

            double clearanceBalance = clearancesUseful
                ? (clearanceMax - clearanceMin) / Math.Max(0.5, clearanceMax)
                : 0.35;

            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            /*
             * Avoid modu stop modu değildir.
             * Yakın engel varsa bile tekne kaçış manevrası yapabilsin diye
             * minimum canlı gaz korunur.
             */
            double throttleNorm =
                urgency >= 0.85 ? 0.11 :
                urgency >= 0.65 ? 0.14 :
                0.18;

            if (clearancesUseful)
            {
                if (clearanceMin < 0.35)
                    throttleNorm *= 0.75;
                else if (clearanceMin < 0.75)
                    throttleNorm *= 0.85;
                else if (clearanceMin < 1.25)
                    throttleNorm *= 0.95;
            }

            throttleNorm *= Math.Clamp(advice.ThrottleScale, 0.35, 1.25);

            if (advice.RequireSlowMode)
                throttleNorm *= 0.85;

            /*
             * ForceCoast artık sadece gerçek son çare sebeplerinde gazı sıfırlayabilir.
             * Recovery / soft obstacle durumunda gazı öldürmesine izin vermiyoruz.
             */
            if (advice.ForceCoast &&
                IsLastResortStopReasonForNavigation(advice.PrimaryReason))
            {
                throttleNorm = 0.0;
            }

            if (throttleNorm > 0.0)
                throttleNorm = Math.Clamp(throttleNorm, 0.06, 0.28);

            double turnBase =
                clearancesUseful
                    ? 0.42 + 0.38 * Math.Clamp(clearanceBalance, 0.0, 1.0)
                    : 0.58;

            double rudderNorm = turnBase * sideSign;

            rudderNorm *= Math.Clamp(advice.YawAggressionScale, 0.45, 2.0);
            rudderNorm *= 1.0 + urgency * 0.35;
            rudderNorm = Math.Clamp(rudderNorm, -0.95, 0.95);

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            string reason = AppendAdviceReason(
                $"AVOID_RECOVERY side={(sideSign > 0 ? "right" : "left")} clearL={left:F2} clearR={right:F2} liveThrottle={throttleNorm:F2}",
                advice);

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: reason,
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );
        }

        private DecisionResult FollowPassableCorridor(
            Insights ins,
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav,
            DecisionAdviceProfile advice)
        {
            double corridorOffsetDeg = Math.Clamp(
                advice.CorridorCenterOffsetDeg,
                -75.0,
                75.0);

            double confidence = Math.Clamp(advice.CorridorConfidence, 0.0, 1.0);
            double absOffset = Math.Abs(corridorOffsetDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);

            double p = corridorOffsetDeg * NavYawKp * 1.25;
            double d = -nav.YawRateDeg * NavYawKd * 0.85;

            double rudderNorm = p + d;

            if (Math.Abs(rudderNorm) < 0.08 && absOffset > 2.0)
                rudderNorm = corridorOffsetDeg >= 0.0 ? 0.08 : -0.08;

            rudderNorm *= Math.Clamp(advice.YawAggressionScale, 0.75, 1.45);
            rudderNorm = Math.Clamp(rudderNorm, -0.82, 0.82);

            /*
             * Koridor takipte de canlı gaz tabanı var.
             * Koridor var diyorsak araç durup bakmayacak, kontrollü geçecek.
             */
            double throttleNorm = 0.14 + confidence * 0.20;

            if (absOffset > 20.0)
                throttleNorm *= 0.75;

            if (absOffset > 35.0)
                throttleNorm *= 0.60;

            if (absYawRate > 35.0)
                throttleNorm *= 0.80;

            throttleNorm *= Math.Clamp(advice.ThrottleScale, 0.45, 1.25);

            if (advice.RequireSlowMode)
                throttleNorm *= 0.90;

            throttleNorm = Math.Clamp(throttleNorm, 0.06, 0.36);

            /*
             * Geçilebilir koridor modunda ForceCoast normalde kullanılmaz.
             * Sadece gerçek last-resort sebebi varsa sıfır gaz kabul edilir.
             */
            if (advice.ForceCoast &&
                IsLastResortStopReasonForNavigation(advice.PrimaryReason))
            {
                throttleNorm = 0.0;
            }

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            string reason =
                $"PASSABLE_CORRIDOR_RECOVERY offset={corridorOffsetDeg:F1}deg " +
                $"width={advice.CorridorWidthMeters:F2}m " +
                $"clear={advice.CorridorClearanceMeters:F2}m " +
                $"conf={confidence:F2} " +
                $"obsAhead={ins.HasObstacleAhead} " +
                $"liveThrottle={throttleNorm:F2}";

            return new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: AppendAdviceReason(reason, advice),
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );
        }

        private DecisionResult NavigateToTarget(
            TaskDefinition task,
            VehicleState state,
            double dt,
            NavigationGeometry nav)
        {
            double absDelta = Math.Abs(nav.HeadingErrorDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);
            bool scenarioOwnedTask = task.IsExternallyCompleted;

            var advice = _currentAdvice.Sanitized();

            bool lastResortStop =
                IsLastResortStopReasonForNavigation(advice.PrimaryReason);

            double rudderNorm = ComputeNavigationRudder(
                nav,
                gainMultiplier: advice.YawAggressionScale);

            if (nav.DistanceXY < BrakeRadiusM)
            {
                double nearBrake = nav.HeadingErrorDeg * NearYawBrakeKp - nav.YawRateDeg * NearYawBrakeKd;
                rudderNorm = Math.Clamp(
                    nearBrake * advice.YawAggressionScale,
                    -1.0,
                    1.0);
            }

            double throttleNorm = ComputeApproachThrottle(nav.DistanceXY);

            throttleNorm *= HeadingScale(absDelta);
            throttleNorm *= HeadingThrottleGate(absDelta, absYawRate);

            if (absDelta >= NearTurnInPlaceDeg)
                throttleNorm = Math.Min(throttleNorm, 0.03);

            throttleNorm *= advice.ThrottleScale;

            if (advice.RequireSlowMode)
                throttleNorm *= 0.55;

            if (advice.ForceCoast && lastResortStop)
                throttleNorm = Math.Min(throttleNorm, 0.0);

            var arrival = PlanMissionArrival(task, throttleNorm, nav, advice);

            throttleNorm = arrival.ThrottleNorm;
            string reason = arrival.Reason;

            rudderNorm = ComputeNavigationRudder(
                nav,
                gainMultiplier: arrival.RecommendedYawGain * advice.YawAggressionScale);

            if (arrival.Phase is ArrivalPhase.Capture or ArrivalPhase.CaptureCoast)
            {
                /*
                 * Fly-through davranışı için capture fazında dümeni fazla öldürmek istemiyoruz.
                 * Ancak final/precision yaklaşmada hâlâ daha sakin dümen iyi olur.
                 */
                if (scenarioOwnedTask && arrival.Reason.Contains("FlyThrough", StringComparison.OrdinalIgnoreCase))
                    rudderNorm *= 0.92;
                else
                    rudderNorm *= scenarioOwnedTask ? 0.75 : 0.85;
            }

            if (arrival.Phase == ArrivalPhase.OvershootRecovery)
            {
                rudderNorm = ComputeOvershootRecoveryRudder(nav);
                rudderNorm = Math.Clamp(
                    rudderNorm * advice.YawAggressionScale,
                    -1.0,
                    1.0);
            }

            if (advice.PreferSafeHeading && advice.ObstacleAvoidanceUrgency > 0.05)
            {
                rudderNorm = ApplySafeHeadingBias(
                    rudderNorm,
                    nav,
                    advice);
            }

            /*
             * Soft obstacle / recovery durumunda RecommendHold aracı öldürmesin.
             * Sadece gerçek last-resort sebebi varsa hold tavsiyesi uygulanır.
             */
            if (advice.RecommendHold &&
                lastResortStop &&
                nav.PlanarSpeedMps <= 0.35)
            {
                throttleNorm = 0.0;
                reason = $"{reason}_ANALYSIS_HOLD_RECOMMENDED";
            }

            if (scenarioOwnedTask)
            {
                throttleNorm = ClampScenarioThrottle(
                    throttleNorm,
                    arrival,
                    reason);
            }
            else
            {
                throttleNorm = Math.Clamp(
                    throttleNorm,
                    arrival.AllowReverseSurge ? -MaxReverseThrottleNorm : 0.0,
                    GeneralMaxApproachThrottleNorm);
            }

            if (advice.ForceCoast &&
                lastResortStop &&
                throttleNorm > 0.0)
            {
                throttleNorm = 0.0;
                reason = $"{reason}_ANALYSIS_FORCE_COAST";
            }

            /*
             * Navigation içinde obstacle risk var ama last-resort yoksa minimum hareket enerjisi korunsun.
             * Bu özellikle "duba gördü, dönüyor ama gitmiyor" davranışını engeller.
             */
            if (!lastResortStop &&
                advice.ObstacleAvoidanceUrgency >= 0.45 &&
                throttleNorm > 0.0)
            {
                throttleNorm = Math.Max(throttleNorm, 0.05);
            }

            var raw = PlanarToRawWrench(throttleNorm, rudderNorm, task, state, dt);
            var output = ScaleCommand(raw);

            var result = new DecisionResult(
                RawCommand: raw,
                OutputCommand: output,
                Reason: AppendAdviceReason(reason, advice),
                ThrottleNorm: throttleNorm,
                RudderNorm: rudderNorm
            );

            return ConstrainExternalScenarioCommandIfNeeded(task, result, reasonOverride: result.Reason);
        }

        private static double ClampScenarioThrottle(
            double throttleNorm,
            ArrivalPlan arrival,
            string reason)
        {
            /*
             * Eski davranış:
             * - Scenario task her zaman ScenarioMinThrottleNorm..ScenarioMaxApproachThrottleNorm aralığına kırpılıyordu.
             * - Bu yüzden AdaptiveArrivalPlanner negatif reverse braking üretse bile burada kayboluyordu.
             * - Ayrıca fly-through checkpointlerde gereksiz minimum throttle baskısı oluşabiliyordu.
             *
             * Yeni davranış:
             * - FlyThrough: negatif fren yok; minimum throttle baskısı hafifletilir.
             * - TurnCritical: küçük negatif reverse braking'e izin verilir.
             * - PrecisionStop/final: daha güçlü negatif fren izni vardır.
             * - Capability/tek yönlü ESC kesmesi actuator allocation tarafında yapılır.
             */
            bool isFlyThrough = reason.Contains("FlyThrough", StringComparison.OrdinalIgnoreCase);
            bool isTurnCritical = reason.Contains("TurnCritical", StringComparison.OrdinalIgnoreCase);
            bool isPrecisionStop = reason.Contains("PrecisionStop", StringComparison.OrdinalIgnoreCase);

            double minThrottle;

            if (isFlyThrough)
            {
                /*
                 * Ara checkpointlerde aracı zorla süründürmeye gerek yok.
                 * Planlayıcı zaten akıcı geçiş için pozitif throttle üretir.
                 */
                minThrottle = 0.0;
            }
            else if (isTurnCritical)
            {
                minThrottle = arrival.AllowReverseSurge ? -MaxReverseThrottleNorm * 0.55 : 0.0;
            }
            else if (isPrecisionStop)
            {
                minThrottle = arrival.AllowReverseSurge ? -MaxReverseThrottleNorm : 0.0;
            }
            else
            {
                minThrottle = arrival.AllowReverseSurge ? -MaxReverseThrottleNorm * 0.50 : ScenarioMinThrottleNorm;
            }

            double maxThrottle = ScenarioMaxApproachThrottleNorm;

            return Math.Clamp(
                throttleNorm,
                minThrottle,
                maxThrottle);
        }

        private static double ComputeNavigationRudder(
            NavigationGeometry nav,
            double gainMultiplier)
        {
            double absDelta = Math.Abs(nav.HeadingErrorDeg);
            double absYawRate = Math.Abs(nav.YawRateDeg);

            if (absDelta <= RudderDeadbandDeg && absYawRate <= YawRateDeadbandDeg)
                return 0.0;

            double p = nav.HeadingErrorDeg * NavYawKp;
            double d = -nav.YawRateDeg * NavYawKd;

            return Math.Clamp((p + d) * Math.Clamp(gainMultiplier, 0.25, 2.0), -1.0, 1.0);
        }

        private static double ComputeOvershootRecoveryRudder(NavigationGeometry nav)
        {
            double p = nav.HeadingErrorDeg * NavYawKp * 1.35;
            double d = -nav.YawRateDeg * NavYawKd * 0.65;

            double rudder = p + d;

            if (Math.Abs(rudder) < 0.25)
                rudder = nav.HeadingErrorDeg >= 0.0 ? 0.25 : -0.25;

            return Math.Clamp(rudder, -1.0, 1.0);
        }

        private static double ApplySafeHeadingBias(
            double rudderNorm,
            NavigationGeometry nav,
            DecisionAdviceProfile advice)
        {
            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            if (urgency <= 0.0)
                return rudderNorm;

            double headingSign = nav.HeadingErrorDeg >= 0.0 ? 1.0 : -1.0;

            if (Math.Abs(rudderNorm) < 0.15)
                rudderNorm = 0.15 * headingSign;

            return Math.Clamp(
                rudderNorm * (1.0 + urgency * 0.35),
                -1.0,
                1.0);
        }

        private static string AppendAdviceReason(
            string reason,
            DecisionAdviceProfile advice)
        {
            if (string.IsNullOrWhiteSpace(advice.PrimaryReason) ||
                advice.PrimaryReason == "NEUTRAL")
            {
                return reason;
            }

            return $"{reason}_ADVICE_{advice.PrimaryReason}";
        }

        private static double ComputeApproachThrottle(double distanceM)
        {
            if (distanceM >= SlowRadiusM)
                return CruiseThrottleNorm;

            double k = (distanceM - StopRadiusM) / (SlowRadiusM - StopRadiusM);
            k = Math.Clamp(k, 0.0, 1.0);

            return MinApproachThrottleNorm + k * (CruiseThrottleNorm - MinApproachThrottleNorm);
        }

        private static NavigationGeometry ComputeNavigationGeometry(Vec3 target, VehicleState state)
        {
            double dx = Safe(target.X - state.Position.X);
            double dy = Safe(target.Y - state.Position.Y);

            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(dist))
                dist = 0.0;

            Vec3 toTargetWorld = new Vec3(dx, dy, 0.0);
            Vec3 toTargetBody = state.Orientation.WorldToBody(toTargetWorld);
            Vec3 velBody = state.Orientation.WorldToBody(state.Velocity);

            double targetHeading = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = Normalize(targetHeading - state.Orientation.YawDeg);

            double planarSpeed = Math.Sqrt(
                Safe(state.Velocity.X) * Safe(state.Velocity.X) +
                Safe(state.Velocity.Y) * Safe(state.Velocity.Y)
            );

            if (!double.IsFinite(planarSpeed))
                planarSpeed = 0.0;

            return new NavigationGeometry(
                DistanceXY: dist,
                TargetBody: toTargetBody,
                VelocityBody: velBody,
                HeadingErrorDeg: delta,
                ForwardSpeedMps: velBody.X,
                PlanarSpeedMps: planarSpeed,
                YawRateDeg: state.AngularVelocity.Z
            );
        }

        private static bool IsLastResortStopReasonForNavigation(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return false;

            return reason.Contains("IMMINENT_CONTACT_LAST_RESORT", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("COLLISION_CANDIDATE", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("HARD_COLLISION", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("HARD_BLOCK", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("MISSION_ABORT", StringComparison.OrdinalIgnoreCase);
        }
    }
}