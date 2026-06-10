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
        /// - Obstacle bilgisi tek başına panic/stop sebebi değildir.
        /// - Analysis/Planner geçilebilir corridor veya recovery davranışı bildiriyorsa
        ///   Decision, aracı öldürmek yerine canlı kaçış/navigate intent üretir.
        /// - Stop/ForceCoast sadece gerçek hard collision / mission abort gibi son çarelerde uygulanır.
        ///
        /// Paket-7A.1:
        /// - Ara scenario waypointleri artık HoldPosition'a düşmez.
        /// - FlyThrough / TurnCritical hedefler akış noktasıdır.
        /// - PrecisionStop hedefler final/duruş noktasıdır.
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

            bool scenarioOwnedTask = task.IsExternallyCompleted;
            bool lastResortStop = IsLastResortStopReasonForIntent(advice.PrimaryReason);

            var scenarioArrivalProfileForHold = scenarioOwnedTask
                ? ResolveScenarioArrivalProfile(task, nav)
                : ArrivalProfileKind.PrecisionStop;

            /*
             * World-aware obstacle arbitration:
             *
             * Eskiden:
             *   insights.HasObstacleAhead == true ise doğrudan AvoidObstacle.
             *
             * Yeni:
             *   Eğer analysis/planner geçilebilir corridor olduğunu söylüyorsa,
             *   obstacle sinyali "yavaşla / dikkatli takip et" seviyesine düşürülür.
             *   Corridor confidence düşük olsa bile bu artık panik stop sebebi değildir.
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

                /*
                 * Son çare olmayan obstacle durumlarında Avoid intent canlı kalmalı.
                 * Bu, "duba gördü ve öküz gibi durdu" davranışını kırar.
                 */
                if (!lastResortStop && avoidSpeed > 0.0)
                    avoidSpeed = Math.Max(avoidSpeed, 0.18);

                LastDecisionReport = AdvancedDecisionReport.Empty with
                {
                    Mode = DecisionMode.Avoid,
                    Reason = AppendAdviceReason("INTENT_OBSTACLE_AHEAD_RECOVERY", advice),
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
                    RiskLevel: Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.25, lastResortStop ? 1.0 : 0.80),
                    Reason: LastDecisionReport.Reason
                );
            }

            /*
             * Paket-7A.1:
             *
             * Eski davranış:
             *   Scenario task ise ve hız düşükse StopRadiusM içinde HoldPosition'a giriyordu.
             *
             * Sorun:
             *   Ara waypointler de "duruş hedefi" gibi ele alınıyordu.
             *   Bu yüzden araç WP alanına girerken gereksiz fren/hold/creep davranışı gösteriyordu.
             *
             * Yeni davranış:
             *   - FlyThrough: asla HoldPosition'a girmez.
             *   - TurnCritical: asla HoldPosition'a girmez; kontrollü döner ama durmaz.
             *   - PrecisionStop: final/hassas hedef; HoldPosition'a girebilir.
             */
            var canEnterHold =
                !scenarioOwnedTask ||
                (scenarioArrivalProfileForHold == ArrivalProfileKind.PrecisionStop &&
                 nav.PlanarSpeedMps <= ScenarioMaxCaptureSpeedMps);

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

            /*
             * Soft obstacle / recovery durumunda navigation intent de canlı kalmalı.
             * Last-resort yoksa heading ayarlarken hızın tamamen ölmesini istemiyoruz.
             */
            if (!lastResortStop &&
                advice.ObstacleAvoidanceUrgency >= 0.45 &&
                desiredSpeed > 0.0)
            {
                desiredSpeed = Math.Max(desiredSpeed, 0.20);
            }

            /*
             * Paket-7A.1:
             * FlyThrough scenario hedeflerinde hedef bölgesine çok yaklaşınca bile
             * hız tabanı korunur. Completion kararını scenario runtime verir,
             * Decision burada "dur ve bekle" refleksine girmez.
             */
            if (scenarioOwnedTask &&
                scenarioArrivalProfileForHold == ArrivalProfileKind.FlyThrough &&
                desiredSpeed > 0.0)
            {
                desiredSpeed = Math.Max(desiredSpeed, 0.45);
            }
            else if (scenarioOwnedTask &&
                     scenarioArrivalProfileForHold == ArrivalProfileKind.TurnCritical &&
                     desiredSpeed > 0.0)
            {
                desiredSpeed = Math.Max(desiredSpeed, 0.32);
            }

            var targetHeadingDeg = Math.Atan2(
                target.Y - state.Position.Y,
                target.X - state.Position.X) * 180.0 / Math.PI;

            /*
             * Eğer geçilebilir corridor varsa ve corridor heading tercih ediliyorsa,
             * target heading'i obstacle panik yönüne değil, corridor hattına yakın tutuyoruz.
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

            if (scenarioOwnedTask)
                reason = $"{reason}_PROFILE_{scenarioArrivalProfileForHold}";

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
                    : Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, lastResortStop ? 1.0 : 0.80),
                Reason: reason
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
             * Eski eşikler fazla korkaktı:
             * confidence < 0.45 veya clearance < 1.0 ise panic bastırmıyordu.
             * Bu yüzden duba kapısı gibi dar ama geçilebilir alanlarda sistem yine Avoid panic'e düşüyordu.
             *
             * Yeni eşik:
             * Corridor gerçekten varsa ve analiz bunu suppress olarak işaretlediyse,
             * düşük/orta confidence ile bile panik bastırılır.
             */
            if (advice.CorridorConfidence < 0.20)
                return false;

            if (advice.CorridorClearanceMeters < 0.30)
                return false;

            return true;
        }

        private static double ApplySuppressedObstacleSpeedPolicy(
            double desiredSpeed,
            DecisionAdviceProfile advice,
            NavigationGeometry nav)
        {
            advice = advice.Sanitized();

            bool lastResortStop = IsLastResortStopReasonForIntent(advice.PrimaryReason);

            var speed = desiredSpeed;

            /*
             * Obstacle var ama güvenli corridor da var:
             * - Tam panik yok.
             * - Yine de hız sınırlanır.
             * - Corridor confidence yüksekse çok öldürülmez.
             */
            var corridorScale = advice.CorridorConfidence switch
            {
                >= 0.85 => 0.90,
                >= 0.65 => 0.78,
                >= 0.40 => 0.68,
                _ => 0.58
            };

            speed *= corridorScale;

            if (advice.RequireSlowMode)
                speed *= 0.85;

            if (Math.Abs(nav.HeadingErrorDeg) > 45.0)
                speed *= 0.72;

            if (Math.Abs(nav.YawRateDeg) > 65.0)
                speed *= 0.78;

            if (advice.ForceCoast && lastResortStop)
                speed = 0.0;

            if (!lastResortStop && speed > 0.0)
                speed = Math.Max(speed, 0.22);

            return Math.Clamp(
                speed,
                0.0,
                Math.Min(1.25, Math.Max(0.28, desiredSpeed)));
        }

        private static double ResolveNavigateSpeedMps(
            NavigationGeometry nav,
            ArrivalPlan arrival,
            DecisionAdviceProfile advice,
            bool scenarioOwnedTask)
        {
            double baseSpeed;

            bool lastResortStop = IsLastResortStopReasonForIntent(advice.PrimaryReason);

            if (arrival.Phase is ArrivalPhase.Capture or ArrivalPhase.CaptureCoast)
                baseSpeed = scenarioOwnedTask ? 0.55 : 0.35;
            else if (arrival.Phase == ArrivalPhase.OvershootRecovery)
                baseSpeed = arrival.AllowReverseSurge ? -0.35 : 0.18;
            else if (nav.DistanceXY < BrakeRadiusM)
                baseSpeed = scenarioOwnedTask ? 0.62 : 0.45;
            else if (nav.DistanceXY < SlowRadiusM)
                baseSpeed = scenarioOwnedTask ? 0.95 : 0.85;
            else
                baseSpeed = scenarioOwnedTask ? 1.45 : 1.35;

            baseSpeed *= Math.Clamp(advice.ThrottleScale, 0.35, 1.5);

            if (advice.RequireSlowMode)
                baseSpeed *= scenarioOwnedTask ? 0.82 : 0.70;

            if (advice.ForceCoast && lastResortStop)
                baseSpeed = 0.0;

            if (!lastResortStop &&
                advice.ObstacleAvoidanceUrgency >= 0.45 &&
                baseSpeed > 0.0)
            {
                baseSpeed = Math.Max(baseSpeed, 0.20);
            }

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
            var maxClear = Math.Max(left, right);

            bool clearancesUseful =
                maxClear > 0.05 &&
                double.IsFinite(maxClear);

            bool lastResortStop = IsLastResortStopReasonForIntent(advice.PrimaryReason);
            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);

            /*
             * Avoid intent için ileri hız tabanı.
             * Engel yakın diye araç ölüme terk edilmeyecek;
             * kaçış yönüne dönerken az da olsa ilerleyecek.
             */
            double speed =
                urgency >= 0.85 ? 0.28 :
                urgency >= 0.65 ? 0.36 :
                0.48;

            if (clearancesUseful)
            {
                if (minClear < 0.35)
                    speed *= 0.75;
                else if (minClear < 0.75)
                    speed *= 0.85;
                else if (minClear < 1.25)
                    speed *= 0.95;
            }

            speed *= Math.Clamp(advice.ThrottleScale, 0.35, 1.2);

            if (advice.RequireSlowMode)
                speed *= 0.85;

            if (Math.Abs(nav.HeadingErrorDeg) > 70.0)
                speed *= 0.75;

            if (Math.Abs(nav.YawRateDeg) > 80.0)
                speed *= 0.75;

            if (advice.ForceCoast && lastResortStop)
                speed = 0.0;

            if (!lastResortStop && speed > 0.0)
                speed = Math.Clamp(speed, 0.18, 0.65);

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
            {
                /*
                 * Clearance kararsızsa target heading'in ters tarafına açıl.
                 * Böylece WP2 sağa/aşağıdaysa sağ dubaya saplanmak yerine diğer tarafa recovery dener.
                 */
                sideSign = nav.HeadingErrorDeg >= 0.0 ? -1.0 : +1.0;
            }
            else
            {
                sideSign = right > left ? +1.0 : -1.0;
            }

            double urgency = Math.Clamp(advice.ObstacleAvoidanceUrgency, 0.0, 1.0);
            double turnDeg = 28.0 + urgency * 42.0;

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

            /*
             * Safe heading bias artık hedef yönünün üstüne aynı tarafa binmek zorunda değil.
             * Obstacle urgency varsa hedefe kör saplanmak yerine ters tarafa küçük recovery açısı verir.
             */
            double sideSign = currentToTargetError >= 0.0 ? -1.0 : +1.0;
            double biasDeg = urgency * 22.0 * sideSign;

            return Normalize(targetHeadingDeg + biasDeg);
        }

        private static bool IsLastResortStopReasonForIntent(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return false;

            /*
             * IMMINENT_CONTACT_LAST_RESORT bilinçli olarak burada tek başına stop sebebi sayılmıyor.
             * Son loglarda geometry clear=0.44 ve collision=false iken analysis bu reason'ı üretebildi.
             * Bu yüzden gerçek stop sadece hard/collision/abort token'larıyla yapılacak.
             */
            return reason.Contains("COLLISION_CANDIDATE", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("HARD_COLLISION", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("HARD_BLOCK", StringComparison.OrdinalIgnoreCase) ||
                   reason.Contains("MISSION_ABORT", StringComparison.OrdinalIgnoreCase);
        }
    }
}