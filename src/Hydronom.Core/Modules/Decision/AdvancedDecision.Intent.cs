using System;
using Hydronom.Core.Control;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        /// <summary>
        /// Decision katmanının yeni ürün seviyesi çıktısıdır.
        ///
        /// Eski Decide(...) metodu geriye dönük uyumluluk için DecisionCommand üretmeye devam eder.
        /// Bu metot ise motor/wrench üretmez; controller'a davranış niyeti verir.
        ///
        /// Kritik mimari kural:
        /// - Obstacle bilgisi tek başına panic Avoid sebebi değildir.
        /// - Analysis/Planner geçilebilir corridor veya güvenli trajectory bildiriyorsa
        ///   Decision, AvoidObstacle yerine Navigate/Track intent üretir.
        /// - Böylece Planner, Analysis ve Decision aynı operasyonel gerçeklikte hizalanır.
        /// </summary>
        public ControlIntent DecideIntent(
            Insights insights,
            TaskDefinition? task,
            VehicleState state,
            double dt)
        {
            dt = SanitizeDt(dt);
            state = state.Sanitized();

            if (task is null)
            {
                ResetControllerState();

                LastDecisionReport = AdvancedDecisionReport.Empty with
                {
                    Mode = DecisionMode.Idle,
                    Reason = "NO_TASK_INTENT"
                };

                return ControlIntent.Idle;
            }

            if (task.Target is not Vec3 target)
            {
                ResetControllerState();

                LastDecisionReport = AdvancedDecisionReport.Empty with
                {
                    Mode = DecisionMode.Idle,
                    Reason = "TASK_HAS_NO_VEC3_TARGET_INTENT"
                };

                return ControlIntent.Idle;
            }

            target = SanitizeVec(target);
            HandleTargetChange(target);

            var nav = ComputeNavigationGeometry(target, state);
            var advice = _currentAdvice.Sanitized();

            var scenarioOwnedTask = task.IsExternallyCompleted;

            /*
             * World-aware obstacle arbitration:
             *
             * Eskiden:
             *   insights.HasObstacleAhead == true ise doğrudan AvoidObstacle.
             *
             * Yeni:
             *   Eğer analysis/planner geçilebilir corridor olduğunu söylüyorsa
             *   ve obstacle panic bastırılmışsa, obstacle sinyali "yavaşla / dikkatli takip et"
             *   seviyesine düşürülür. Böylece world-aware planner'ın güvenli corridor kararı
             *   decision tarafından ezilmez.
             */
            var suppressObstaclePanic = ShouldSuppressObstaclePanic(
                insights,
                advice);

            if (insights.HasObstacleAhead && !suppressObstaclePanic)
            {
                ExitHoldMode();

                var avoidHeading = ResolveAvoidHeadingDeg(
                    insights,
                    nav,
                    advice,
                    state);

                var avoidSpeed = ResolveAvoidSpeedMps(
                    insights,
                    nav,
                    advice);

                LastDecisionReport = AdvancedDecisionReport.Empty with
                {
                    Mode = DecisionMode.Avoid,
                    Reason = AppendAdviceReason("INTENT_OBSTACLE_AHEAD", advice),
                    HeadingErrorDeg = Normalize(avoidHeading - state.Orientation.YawDeg),
                    ForwardSpeedMps = nav.ForwardSpeedMps,
                    ObstacleAhead = true,
                    ThrottleNorm = Math.Clamp(avoidSpeed / 3.0, 0.0, 1.0),
                    RudderNorm = Math.Clamp(Normalize(avoidHeading - state.Orientation.YawDeg) / 45.0, -1.0, 1.0)
                };

                return new ControlIntent(
                    Kind: ControlIntentKind.AvoidObstacle,
                    TargetPosition: target,
                    TargetHeadingDeg: avoidHeading,
                    DesiredForwardSpeedMps: avoidSpeed,
                    DesiredDepthMeters: target.Z,
                    DesiredAltitudeMeters: 0.0,
                    HoldHeading: true,
                    HoldDepth: true,
                    AllowReverse: false,
                    RiskLevel: Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.25, 1.0),
                    Reason: LastDecisionReport.Reason
                );
            }

            var canEnterHold =
                !scenarioOwnedTask ||
                nav.PlanarSpeedMps <= ScenarioMaxCaptureSpeedMps;

            if (nav.DistanceXY <= StopRadiusM && canEnterHold)
            {
                EnterHoldMode(state);

                var holdHeading = _frozenHoldHeadingDeg ?? state.Orientation.YawDeg;

                LastDecisionReport = AdvancedDecisionReport.Empty with
                {
                    Mode = DecisionMode.Hold,
                    Reason = suppressObstaclePanic
                        ? AppendAdviceReason("INTENT_HOLD_POSITION_SUPPRESSED_OBSTACLE_PANIC", advice)
                        : "INTENT_HOLD_POSITION",
                    HeadingErrorDeg = Normalize(holdHeading - state.Orientation.YawDeg),
                    ForwardSpeedMps = nav.ForwardSpeedMps,
                    ObstacleAhead = insights.HasObstacleAhead,
                    ThrottleNorm = 0.0,
                    RudderNorm = 0.0
                };

                return new ControlIntent(
                    Kind: ControlIntentKind.HoldPosition,
                    TargetPosition: target,
                    TargetHeadingDeg: holdHeading,
                    DesiredForwardSpeedMps: 0.0,
                    DesiredDepthMeters: target.Z,
                    DesiredAltitudeMeters: 0.0,
                    HoldHeading: true,
                    HoldDepth: true,
                    AllowReverse: true,
                    RiskLevel: suppressObstaclePanic
                        ? Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 0.35)
                        : 0.0,
                    Reason: LastDecisionReport.Reason
                );
            }

            ExitHoldMode();

            var arrival = PlanMissionArrival(
                task,
                ComputeApproachThrottle(nav.DistanceXY),
                nav,
                advice);

            var desiredSpeed = ResolveNavigateSpeedMps(
                nav,
                arrival,
                advice,
                scenarioOwnedTask);

            if (suppressObstaclePanic)
            {
                desiredSpeed = ApplySuppressedObstacleSpeedPolicy(
                    desiredSpeed,
                    advice,
                    nav);
            }

            var targetHeadingDeg = Math.Atan2(
                target.Y - state.Position.Y,
                target.X - state.Position.X) * 180.0 / Math.PI;

            /*
             * Eğer geçilebilir corridor varsa ve corridor heading tercih ediliyorsa,
             * target heading'i obstacle panik yönüne değil, mevcut trajectory/corridor hattına
             * yakın tutuyoruz. Runtime zaten planner reference'ını daha sonra intent üzerine
             * bind ediyor; bu kural burada eski obstacle bias'ın sistemi bozmasını engeller.
             */
            if (advice.HasPassableCorridor &&
                advice.PreferCorridorHeading &&
                Math.Abs(advice.CorridorCenterOffsetDeg) > 1.0)
            {
                targetHeadingDeg = Normalize(
                    state.Orientation.YawDeg + advice.CorridorCenterOffsetDeg);
            }
            else if (advice.PreferSafeHeading &&
                     advice.ObstacleAvoidanceUrgency > 0.05 &&
                     !suppressObstaclePanic)
            {
                targetHeadingDeg = ResolveSafeHeadingDeg(
                    targetHeadingDeg,
                    nav,
                    advice,
                    state);
            }

            var headingError = Normalize(targetHeadingDeg - state.Orientation.YawDeg);

            var reason = AppendAdviceReason(
                suppressObstaclePanic
                    ? $"INTENT_{arrival.Reason}_TRACK_CORRIDOR_SUPPRESS_OBSTACLE_PANIC"
                    : $"INTENT_{arrival.Reason}",
                advice);

            LastDecisionReport = AdvancedDecisionReport.Empty with
            {
                Mode = DecisionMode.Navigate,
                Reason = reason,
                HeadingErrorDeg = headingError,
                ForwardSpeedMps = nav.ForwardSpeedMps,
                ObstacleAhead = insights.HasObstacleAhead,
                ThrottleNorm = Math.Clamp(desiredSpeed / 3.0, -1.0, 1.0),
                RudderNorm = Math.Clamp(headingError / 45.0, -1.0, 1.0)
            };

            return new ControlIntent(
                Kind: ControlIntentKind.Navigate,
                TargetPosition: target,
                TargetHeadingDeg: targetHeadingDeg,
                DesiredForwardSpeedMps: desiredSpeed,
                DesiredDepthMeters: target.Z,
                DesiredAltitudeMeters: 0.0,
                HoldHeading: true,
                HoldDepth: true,
                AllowReverse: arrival.AllowReverseSurge,
                RiskLevel: suppressObstaclePanic
                    ? Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 0.35)
                    : Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0),
                Reason: LastDecisionReport.Reason
            );
        }

        private static bool ShouldSuppressObstaclePanic(
            Insights insights,
            DecisionAdviceProfile advice)
        {
            if (!insights.HasObstacleAhead)
                return false;

            advice = advice.Sanitized();

            if (!advice.HasPassableCorridor)
                return false;

            if (!advice.SuppressObstaclePanic)
                return false;

            /*
             * Corridor confidence ve clearance düşükse panic bastırma yapılmaz.
             * Bu eşikler bilerek temkinli tutuldu.
             */
            if (advice.CorridorConfidence < 0.45)
                return false;

            if (advice.CorridorClearanceMeters < 1.0)
                return false;

            return true;
        }

        private static double ApplySuppressedObstacleSpeedPolicy(
            double desiredSpeed,
            DecisionAdviceProfile advice,
            NavigationGeometry nav)
        {
            advice = advice.Sanitized();

            var speed = desiredSpeed;

            /*
             * Obstacle var ama güvenli corridor da var:
             * - Tam panik yok.
             * - Yine de hız sınırlanır.
             * - Corridor confidence yüksekse çok öldürülmez.
             */
            var corridorScale = advice.CorridorConfidence switch
            {
                >= 0.85 => 0.85,
                >= 0.65 => 0.70,
                _ => 0.55
            };

            speed *= corridorScale;

            if (advice.RequireSlowMode)
                speed *= 0.75;

            if (Math.Abs(nav.HeadingErrorDeg) > 45.0)
                speed *= 0.65;

            if (Math.Abs(nav.YawRateDeg) > 65.0)
                speed *= 0.70;

            if (advice.ForceCoast)
                speed = 0.0;

            return Math.Clamp(
                speed,
                0.0,
                Math.Min(1.2, Math.Max(0.25, desiredSpeed)));
        }

        private static double ResolveNavigateSpeedMps(
            NavigationGeometry nav,
            ArrivalPlan arrival,
            DecisionAdviceProfile advice,
            bool scenarioOwnedTask)
        {
            double baseSpeed;

            if (arrival.Phase is ArrivalPhase.Capture or ArrivalPhase.CaptureCoast)
                baseSpeed = scenarioOwnedTask ? 0.55 : 0.35;
            else if (arrival.Phase == ArrivalPhase.OvershootRecovery)
                baseSpeed = arrival.AllowReverseSurge ? -0.35 : 0.0;
            else if (nav.DistanceXY < BrakeRadiusM)
                baseSpeed = 0.45;
            else if (nav.DistanceXY < SlowRadiusM)
                baseSpeed = 0.85;
            else
                baseSpeed = 1.35;

            baseSpeed *= Math.Clamp(advice.ThrottleScale, 0.0, 1.5);

            if (advice.RequireSlowMode)
                baseSpeed *= 0.55;

            if (advice.ForceCoast)
                baseSpeed = 0.0;

            return Math.Clamp(
                baseSpeed,
                arrival.AllowReverseSurge ? -0.8 : 0.0,
                scenarioOwnedTask ? 1.4 : 2.0);
        }

        private static double ResolveAvoidSpeedMps(
            Insights insights,
            NavigationGeometry nav,
            DecisionAdviceProfile advice)
        {
            var left = SafeNonNegative(insights.ClearanceLeft, 0.0);
            var right = SafeNonNegative(insights.ClearanceRight, 0.0);
            var minClear = Math.Min(left, right);

            double speed = 0.45;

            if (minClear < 1.0)
                speed = 0.10;
            else if (minClear < 2.0)
                speed = 0.25;

            speed *= Math.Clamp(advice.ThrottleScale, 0.0, 1.2);

            if (advice.RequireSlowMode)
                speed *= 0.55;

            if (advice.ForceCoast)
                speed = 0.0;

            return Math.Clamp(speed, 0.0, 0.8);
        }

        private static double ResolveAvoidHeadingDeg(
            Insights insights,
            NavigationGeometry nav,
            DecisionAdviceProfile advice,
            VehicleState state)
        {
            if (advice.HasPassableCorridor &&
                advice.PreferCorridorHeading)
            {
                return Normalize(state.Orientation.YawDeg + advice.CorridorCenterOffsetDeg);
            }

            double left = SafeNonNegative(insights.ClearanceLeft, 0.0);
            double right = SafeNonNegative(insights.ClearanceRight, 0.0);

            double sideSign;

            if (Math.Abs(right - left) < 0.10)
                sideSign = nav.HeadingErrorDeg >= 0.0 ? +1.0 : -1.0;
            else
                sideSign = right > left ? +1.0 : -1.0;

            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);
            double turnDeg = 25.0 + urgency * 35.0;

            return Normalize(state.Orientation.YawDeg + sideSign * turnDeg);
        }

        private static double ResolveSafeHeadingDeg(
            double targetHeadingDeg,
            NavigationGeometry nav,
            DecisionAdviceProfile advice,
            VehicleState state)
        {
            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            if (urgency <= 0.0)
                return targetHeadingDeg;

            double currentToTargetError = Normalize(targetHeadingDeg - state.Orientation.YawDeg);
            double sideSign = currentToTargetError >= 0.0 ? +1.0 : -1.0;
            double biasDeg = urgency * 18.0 * sideSign;

            return Normalize(targetHeadingDeg + biasDeg);
        }
    }
}