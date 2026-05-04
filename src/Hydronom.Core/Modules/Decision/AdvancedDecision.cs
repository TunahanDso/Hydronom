using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedDecision v3.3
    /// ------------------------------------------------------------
    /// Platform bağımsız 6-DoF wrench tabanlı karar modülü.
    ///
    /// Bu sürüm partial dosyalara ayrılmıştır:
    /// - Ana karar akışı
    /// - Navigation
    /// - Arrival planner
    /// - Hold
    /// - Wrench üretimi
    /// - Reporting
    /// - Helper/model katmanları
    ///
    /// v3.3 ekleri:
    /// - Analysis → Decision köprüsü için DecisionAdviceProfile kabul eder.
    /// - Runtime, AdvancedAnalysis.LastOperationalContext.Advice bilgisini
    ///   UpdateAdvice(...) üzerinden buraya aktarabilir.
    /// - Advice yoksa nötr profil kullanılır; IDecisionModule sözleşmesi bozulmaz.
    /// ------------------------------------------------------------
    /// Not:
    /// SafetyLimiter komutu ayrıca yumuşatır.
    /// ActuatorManager ise gerçekten üretilebilen wrench'i allocation raporuyla açıklar.
    /// </summary>
    public partial class AdvancedDecision : IDecisionModule
    {
        private double _heaveIntegral = 0.0;
        private Vec3? _lastTarget = null;
        private double? _frozenHoldHeadingDeg = null;
        private bool _isHoldingPosition = false;

        /*
         * Analysis → Decision köprüsü.
         *
         * Bu değer nötr kaldığında karar modülü normal davranır.
         * Runtime loop, AdvancedAnalysis.LastOperationalContext.Advice değerini
         * bu modüle aktarırsa karar modülü hız/gaz/yaw/arrival davranışını
         * operasyonel riske göre ayarlayabilir.
         */
        private DecisionAdviceProfile _currentAdvice = DecisionAdviceProfile.Neutral;

        public AdvancedDecisionReport LastDecisionReport { get; private set; } =
            AdvancedDecisionReport.Empty;

        /// <summary>
        /// Analysis katmanından gelen son operasyonel karar tavsiyesi.
        /// Eğer runtime bu değeri güncellemezse nötr profil kullanılır.
        /// </summary>
        public DecisionAdviceProfile CurrentAdvice => _currentAdvice;

        /// <summary>
        /// Analysis → Decision köprüsü.
        ///
        /// AdvancedAnalysis.LastOperationalContext.Advice buraya aktarılabilir.
        /// Bu metot IDecisionModule arayüzünü bozmadan AdvancedDecision'a
        /// daha zengin operasyonel bağlam taşır.
        /// </summary>
        public void UpdateAdvice(DecisionAdviceProfile advice)
        {
            _currentAdvice = advice.Sanitized();
        }

        /// <summary>
        /// Tavsiyeyi nötr hale getirir.
        /// Test, fallback veya analysis yokluğu durumunda kullanılabilir.
        /// </summary>
        public void ClearAdvice()
        {
            _currentAdvice = DecisionAdviceProfile.Neutral;
        }

        public DecisionCommand Decide(Insights insights, TaskDefinition? task, VehicleState state, double dt)
        {
            dt = SanitizeDt(dt);
            state = state.Sanitized();

            if (task is null)
            {
                ResetControllerState();
                return ReportAndReturn(
                    DecisionMode.Idle,
                    "NO_TASK",
                    DecisionCommand.Zero,
                    DecisionCommand.Zero,
                    state,
                    target: null,
                    distanceXY: 0.0,
                    headingErrorDeg: 0.0,
                    forwardSpeedMps: 0.0,
                    yawRateDeg: state.AngularVelocity.Z,
                    obstacleAhead: insights.HasObstacleAhead
                );
            }

            if (task.Target is not Vec3 target)
            {
                ResetControllerState();
                return ReportAndReturn(
                    DecisionMode.Idle,
                    "TASK_HAS_NO_VEC3_TARGET",
                    DecisionCommand.Zero,
                    DecisionCommand.Zero,
                    state,
                    target: null,
                    distanceXY: 0.0,
                    headingErrorDeg: 0.0,
                    forwardSpeedMps: 0.0,
                    yawRateDeg: state.AngularVelocity.Z,
                    obstacleAhead: insights.HasObstacleAhead
                );
            }

            target = SanitizeVec(target);
            HandleTargetChange(target);

            var nav = ComputeNavigationGeometry(target, state);
            bool scenarioOwnedTask = task.IsExternallyCompleted;

            if (insights.HasObstacleAhead)
            {
                ExitHoldMode();

                var avoidCmd = Avoid(insights, task, state, dt, nav);
                avoidCmd = ConstrainExternalScenarioCommandIfNeeded(task, avoidCmd, reasonOverride: null);

                return ReportAndReturn(
                    DecisionMode.Avoid,
                    "OBSTACLE_AHEAD",
                    avoidCmd.RawCommand,
                    avoidCmd.OutputCommand,
                    state,
                    target,
                    nav.DistanceXY,
                    nav.HeadingErrorDeg,
                    nav.ForwardSpeedMps,
                    nav.YawRateDeg,
                    obstacleAhead: true,
                    throttleNorm: avoidCmd.ThrottleNorm,
                    rudderNorm: avoidCmd.RudderNorm
                );
            }

            bool canEnterHold =
                !scenarioOwnedTask ||
                nav.PlanarSpeedMps <= ScenarioMaxCaptureSpeedMps;

            if (nav.DistanceXY <= StopRadiusM && canEnterHold)
            {
                EnterHoldMode(state);

                var hold = HoldPosition(target, state, dt, nav);
                hold = ConstrainExternalScenarioCommandIfNeeded(task, hold, reasonOverride: null);

                return ReportAndReturn(
                    DecisionMode.Hold,
                    hold.Reason,
                    hold.RawCommand,
                    hold.OutputCommand,
                    state,
                    target,
                    nav.DistanceXY,
                    nav.HeadingErrorDeg,
                    nav.ForwardSpeedMps,
                    nav.YawRateDeg,
                    obstacleAhead: false,
                    throttleNorm: hold.ThrottleNorm,
                    rudderNorm: hold.RudderNorm
                );
            }

            ExitHoldMode();

            var navCmd = NavigateToTarget(task, state, dt, nav);
            return ReportAndReturn(
                DecisionMode.Navigate,
                navCmd.Reason,
                navCmd.RawCommand,
                navCmd.OutputCommand,
                state,
                target,
                nav.DistanceXY,
                nav.HeadingErrorDeg,
                nav.ForwardSpeedMps,
                nav.YawRateDeg,
                obstacleAhead: false,
                throttleNorm: navCmd.ThrottleNorm,
                rudderNorm: navCmd.RudderNorm
            );
        }
    }
}