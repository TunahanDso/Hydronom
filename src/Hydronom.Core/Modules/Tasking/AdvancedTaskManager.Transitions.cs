using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
        /*
         * Bu dosyanın görevi:
         * - Routed görevleri başlatmak
         * - Waypoint arrival / wait / loop / final completion yönetmek
         * - Task progress / no-progress guard takip etmek
         * - Obstacle hold guard takip etmek
         * - Current task abort etmek
         *
         * Bu dosyanın görevi değildir:
         * - Heading üretmek
         * - Force / torque üretmek
         * - Obstacle avoidance yapmak
         * - Planner yerine rota seçmek
         * - Trajectory / local-detour üretmek
         * - Controller hedefini ezmek
         * - Görev adından davranış tahmin etmek
         */

        private void StartTaskInternal(TaskDefinition task, string reason, bool preserveQueue)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            task = task.Normalize();

            if (!preserveQueue)
                _taskQueue.Clear();

            ResetInternalState(clearLastIdentity: false);
            _routePoints.Clear();

            CurrentTask = task;
            Phase = TaskPhase.Active;
            LastStatusReason = SafeReason(reason, "ROUTED_TASK_STARTED");

            _taskStartTicks = NowTicks();
            _lastTaskName = SafeTaskName(task);
            _startedTaskCount++;

            /*
             * Görev semantiği artık string/name üzerinden çıkarılamaz.
             * Hold / wait / station behavior sadece TaskDefinition içinden okunur:
             * - task.Kind
             * - task.Behavior
             * - task.Completion
             * - task.HoldOnArrive
             */

            if (task.Waypoints is { Count: > 0 })
            {
                foreach (var point in task.Waypoints)
                    _routePoints.Add(SanitizeVec(point));
            }
            else if (task.Target is Vec3 t)
            {
                _routePoints.Add(SanitizeVec(t));
            }

            if (_routePoints.Count == 0)
            {
                AbortInternal("Routed görevde hedef noktası yok.", state: null, insights: null);
                return;
            }

            _currentIndex = ClampRouteIndex(_currentIndex);
            _lastTarget = _routePoints[^1];
            _waypointStartDistance = null;

            RefreshReport(
                reason: LastStatusReason,
                state: null,
                insights: null,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: 0.0,
                effectiveArrivalThresholdM: ComputeTaskArrivalThreshold(task, 0.0)
            );
        }

        /// <summary>
        /// Eski partial dosyalar hâlâ bu metodu çağırıyorsa uyumluluk için tutulur.
        /// Yeni ana akışta TryStartNextRunnableTask kullanılır.
        /// </summary>
        private bool TryStartNextQueuedTask(
            string completedReason,
            VehicleState? state,
            Insights? insights)
        {
            if (_taskQueue.Count <= 0)
                return false;

            var next = _taskQueue.Dequeue();

            StartMissionTaskInternal(
                next,
                reason: $"{SafeReason(completedReason, "TASK_COMPLETED")}_NEXT_TASK_STARTED",
                preserveQueue: true
            );

            double speed = ComputeSpeedMps(state);

            RefreshReport(
                reason: LastStatusReason ?? "NEXT_TASK_STARTED",
                state: state,
                insights: insights,
                distanceToWaypoint: EstimateCurrentDistance(state),
                distanceToFinal: EstimateFinalDistance(state),
                speedMps: speed,
                effectiveArrivalThresholdM: ComputeTaskArrivalThreshold(CurrentTask, speed)
            );

            return true;
        }

        private void HandleWaypointArrival(
            long nowTicks,
            TaskDefinition task,
            VehicleState? state,
            Insights insights,
            double distToWaypoint,
            double distToFinal,
            double speedMps,
            double effectiveArrivalThresholdM)
        {
            task = task.Normalize();

            if (_routePoints.Count == 0)
            {
                AbortInternal("Waypoint arrival işlendi ama route boş.", state, insights);
                return;
            }

            _currentIndex = ClampRouteIndex(_currentIndex);

            bool isLastPoint = _currentIndex == _routePoints.Count - 1;

            if (_waypointArriveTicks is null)
            {
                _waypointArriveTicks = nowTicks;
                Phase = TaskPhase.Arrived;
                LastStatusReason = $"Arrived at waypoint {_currentIndex + 1}/{_routePoints.Count}";

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: distToWaypoint,
                    distanceToFinal: distToFinal,
                    speedMps: speedMps,
                    effectiveArrivalThresholdM: effectiveArrivalThresholdM
                );

                return;
            }

            double requiredWaitSec = GetRequiredWaypointWaitSeconds(task, isLastPoint);
            if (requiredWaitSec > 0.0)
            {
                double elapsed = ElapsedSeconds(_waypointArriveTicks.Value, nowTicks);
                if (elapsed < requiredWaitSec)
                {
                    Phase = TaskPhase.Arrived;
                    LastStatusReason = isLastPoint
                        ? $"Holding final waypoint {_currentIndex + 1}/{_routePoints.Count}"
                        : $"Waiting at waypoint {_currentIndex + 1}/{_routePoints.Count}";

                    RefreshReport(
                        reason: LastStatusReason,
                        state: state,
                        insights: insights,
                        distanceToWaypoint: distToWaypoint,
                        distanceToFinal: distToFinal,
                        speedMps: speedMps,
                        effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                        waitElapsedSeconds: elapsed,
                        waitRemainingSeconds: Math.Max(0.0, requiredWaitSec - elapsed)
                    );

                    return;
                }
            }

            _waypointArriveTicks = null;

            if (!isLastPoint)
            {
                _completedWaypointCount = Math.Max(_completedWaypointCount, _currentIndex + 1);
                _currentIndex = ClampRouteIndex(_currentIndex + 1);

                Phase = TaskPhase.Active;
                LastStatusReason = $"Proceeding to waypoint {_currentIndex + 1}/{_routePoints.Count}";

                ResetProgressTracking();

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: 0.0,
                    distanceToFinal: distToFinal,
                    speedMps: speedMps,
                    effectiveArrivalThresholdM: effectiveArrivalThresholdM
                );

                return;
            }

            _completedWaypointCount = _routePoints.Count;

            if (task.Loop)
            {
                _currentIndex = 0;
                _completedWaypointCount = 0;

                Phase = TaskPhase.Active;
                LastStatusReason = "Looping route from beginning";

                ResetProgressTracking();

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: 0.0,
                    distanceToFinal: distToFinal,
                    speedMps: speedMps,
                    effectiveArrivalThresholdM: effectiveArrivalThresholdM
                );

                return;
            }

            if (IsExternalRouteCompletion(task))
            {
                /*
                 * Scenario / fleet / operator-owned task'larda TaskManager görevi temizlemez.
                 *
                 * Sebep:
                 * - TaskManager sadece "navigasyon hedefi geometrik olarak yakalandı" bilgisini taşır.
                 * - Objective gerçekten tamamlandı mı kararını dış sahip verir.
                 * - Böylece hız/settle/tolerance/sensör doğrulama şartları sağlanmadan CurrentTask=null olmaz.
                 */
                Phase = TaskPhase.Arrived;
                LastStatusReason = BuildExternalCompletionWaitReason(task);

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: distToWaypoint,
                    distanceToFinal: distToFinal,
                    speedMps: speedMps,
                    effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                    progressPercentOverride: 100.0
                );

                return;
            }

            /*
             * HoldStation / Wait / HoldOnArrive görevlerinde requiredWaitSec zaten yukarıda beklendi.
             * Eğer görev indefinite hold ise TaskManager complete etmez, görevi Arrived fazında tutar.
             */
            if (ShouldHoldIndefinitelyOnFinal(task))
            {
                Phase = TaskPhase.Arrived;
                LastStatusReason = "Holding on final waypoint";

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: distToWaypoint,
                    distanceToFinal: distToFinal,
                    speedMps: speedMps,
                    effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                    progressPercentOverride: 100.0
                );

                return;
            }

            CompleteCurrentRoutedTask(
                task,
                reason: "TASK_COMPLETED",
                state: state,
                insights: insights,
                distToWaypoint: distToWaypoint,
                distToFinal: distToFinal,
                speedMps: speedMps,
                effectiveArrivalThresholdM: effectiveArrivalThresholdM
            );
        }

        private void CompleteCurrentRoutedTask(
            TaskDefinition task,
            string reason,
            VehicleState? state,
            Insights? insights,
            double distToWaypoint,
            double distToFinal,
            double speedMps,
            double effectiveArrivalThresholdM)
        {
            _lastTaskName = SafeTaskName(task);
            _lastTarget = _routePoints.Count > 0 ? _routePoints[^1] : task.Target;

            _completedTaskCount++;

            RefreshReport(
                reason: reason,
                state: state,
                insights: insights,
                distanceToWaypoint: distToWaypoint,
                distanceToFinal: distToFinal,
                speedMps: speedMps,
                effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                progressPercentOverride: 100.0
            );

            CurrentTask = null;
            _routePoints.Clear();
            ResetInternalState(clearLastIdentity: false);

            Phase = TaskPhase.None;
            LastStatusReason = reason;

            if (!TryStartNextRunnableTask())
            {
                RefreshOrchestrationSnapshot(reason);
            }
        }

        private void TrackProgress(
            long nowTicks,
            double dist3D,
            VehicleState? state,
            Insights? insights)
        {
            double safeDist = SafeNonNegative(dist3D, 0.0);

            if (_waypointStartDistance is null)
                _waypointStartDistance = safeDist;

            if (_lastProgressDist is null)
            {
                _lastProgressDist = safeDist;
                _lastProgressTicks = nowTicks;
                return;
            }

            double previous = SafeNonNegative(_lastProgressDist.Value, safeDist);
            double delta = previous - safeDist;

            if (delta >= _minProgressDeltaM)
            {
                _lastProgressDist = safeDist;
                _lastProgressTicks = nowTicks;
                return;
            }

            double maxNoProgressSeconds = CurrentTask?.Timing.MaxNoProgressSeconds ?? _maxNoProgressSeconds;
            maxNoProgressSeconds = SafeNonNegative(maxNoProgressSeconds, _maxNoProgressSeconds);

            if (_lastProgressTicks is long tProg &&
                maxNoProgressSeconds > 0.0 &&
                ElapsedSeconds(tProg, nowTicks) > maxNoProgressSeconds)
            {
                if (insights?.HasObstacleAhead == true)
                {
                    /*
                     * Task Manager burada kaçış kararı vermez.
                     * Sadece no-progress abort'u obstacle varken bastırır.
                     * Planner/Recovery katmanı güvenli rota üretmelidir.
                     */
                    _lastProgressDist = safeDist;
                    _lastProgressTicks = nowTicks;
                    LastStatusReason = "Obstacle hold; no-progress abort suppressed";
                    return;
                }

                AbortInternal("Görev ilerleme göstermiyor (takılmış olabilir).", state, insights);
            }
        }

        private void HandleObstacleIfAny(
            Insights insights,
            long nowTicks,
            VehicleState? state)
        {
            bool obstacleAhead = insights.HasObstacleAhead;

            if (!obstacleAhead)
            {
                _obstacleSinceTicks = null;
                return;
            }

            double speed = ComputeSpeedMps(state);
            double effectiveArrivalThreshold = ComputeTaskArrivalThreshold(CurrentTask, speed);

            if (_obstacleSinceTicks is null)
            {
                _obstacleSinceTicks = nowTicks;
                LastStatusReason = "Obstacle detected; task guard observing";

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: EstimateCurrentDistance(state),
                    distanceToFinal: EstimateFinalDistance(state),
                    speedMps: speed,
                    effectiveArrivalThresholdM: effectiveArrivalThreshold
                );

                return;
            }

            double maxObstacleHoldSeconds =
                CurrentTask?.Timing.MaxObstacleHoldSeconds ?? _maxObstacleHoldSeconds;

            maxObstacleHoldSeconds = SafeNonNegative(maxObstacleHoldSeconds, _maxObstacleHoldSeconds);

            var elapsed = ElapsedSeconds(_obstacleSinceTicks.Value, nowTicks);
            if (maxObstacleHoldSeconds > 0.0 && elapsed > maxObstacleHoldSeconds)
            {
                AbortInternal("Engel uzun süre kaldı, görev iptal edildi.", state, insights);
                return;
            }

            if (Phase != TaskPhase.Arrived)
                Phase = TaskPhase.Active;

            LastStatusReason = "Obstacle persists; guard observing";

            RefreshReport(
                reason: LastStatusReason,
                state: state,
                insights: insights,
                distanceToWaypoint: EstimateCurrentDistance(state),
                distanceToFinal: EstimateFinalDistance(state),
                speedMps: speed,
                effectiveArrivalThresholdM: effectiveArrivalThreshold
            );
        }

        private void AbortInternal(
            string reason,
            VehicleState? state,
            Insights? insights)
        {
            LastStatusReason = SafeReason(reason, "TASK_ABORTED");
            Phase = TaskPhase.Aborted;

            double speed = ComputeSpeedMps(state);

            RefreshReport(
                reason: LastStatusReason,
                state: state,
                insights: insights,
                distanceToWaypoint: EstimateCurrentDistance(state),
                distanceToFinal: EstimateFinalDistance(state),
                speedMps: speed,
                effectiveArrivalThresholdM: ComputeTaskArrivalThreshold(CurrentTask, speed)
            );

            CurrentTask = null;
            _routePoints.Clear();

            _currentIndex = 0;
            _taskStartTicks = null;
            _waypointArriveTicks = null;
            _lastProgressDist = null;
            _lastProgressTicks = null;
            _waypointStartDistance = null;
            _obstacleSinceTicks = null;

            RefreshOrchestrationSnapshot(LastStatusReason);
        }

        private static double GetRequiredWaypointWaitSeconds(TaskDefinition task, bool isLastPoint)
        {
            task = task.Normalize();

            double perPointWait = SafeNonNegative(task.WaitSecondsPerPoint, 0.0);

            if (!isLastPoint)
                return perPointWait;

            double completionHold = task.Completion.Mode == TaskCompletionMode.HoldDurationSatisfied
                ? SafeNonNegative(task.Completion.RequiredHoldSeconds, 0.0)
                : 0.0;

            return Math.Max(perPointWait, completionHold);
        }

        private static bool IsExternalRouteCompletion(TaskDefinition task)
        {
            task = task.Normalize();

            return
                task.IsExternallyCompleted ||
                task.Completion.RequiresExternalAck ||
                task.Completion.Mode is
                    TaskCompletionMode.ExternalConfirmation or
                    TaskCompletionMode.GatePlaneCrossed or
                    TaskCompletionMode.FleetConditionSatisfied;
        }

        private static bool ShouldHoldIndefinitelyOnFinal(TaskDefinition task)
        {
            task = task.Normalize();

            if (!task.HoldOnArrive && !task.IsStationKeeping)
                return false;

            if (task.Completion.Mode == TaskCompletionMode.HoldDurationSatisfied &&
                task.Completion.RequiredHoldSeconds > 0.0)
            {
                return false;
            }

            if (task.IsExternallyCompleted || task.Completion.RequiresExternalAck)
                return true;

            return task.HoldOnArrive || task.IsStationKeeping;
        }

        private static string BuildExternalCompletionWaitReason(TaskDefinition task)
        {
            task = task.Normalize();

            string owner = string.IsNullOrWhiteSpace(task.ExternalOwnerId)
                ? "external"
                : task.ExternalOwnerId!.Trim();

            string objective = string.IsNullOrWhiteSpace(task.ExternalObjectiveId)
                ? "objective"
                : task.ExternalObjectiveId!.Trim();

            return $"External completion pending: owner={owner}, objective={objective}, kind={task.Kind}";
        }
    }
}