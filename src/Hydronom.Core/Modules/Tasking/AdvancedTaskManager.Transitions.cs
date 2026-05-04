using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
        private void StartTaskInternal(TaskDefinition task, string reason, bool preserveQueue)
        {
            if (!preserveQueue)
                _taskQueue.Clear();

            ResetInternalState(clearLastIdentity: false);

            CurrentTask = task;
            Phase = TaskPhase.Active;
            LastStatusReason = reason;

            _taskStartTicks = NowTicks();
            _lastTaskName = task.Name;
            _startedTaskCount++;

            if (!task.HoldOnArrive)
                task.HoldOnArrive = InferHoldFromName(task.Name);

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
                AbortInternal("Görevde hedef noktası yok.", state: null, insights: null);
                return;
            }

            _lastTarget = _routePoints[^1];

            RefreshReport(
                reason: reason,
                state: null,
                insights: null,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: 0.0,
                effectiveArrivalThresholdM: _arriveThresholdM
            );
        }

        private bool TryStartNextQueuedTask(
            string completedReason,
            VehicleState? state,
            Insights? insights)
        {
            if (_taskQueue.Count <= 0)
                return false;

            var next = _taskQueue.Dequeue();

            StartTaskInternal(
                next,
                reason: $"{completedReason}_NEXT_TASK_STARTED",
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
                effectiveArrivalThresholdM: ComputeEffectiveArrivalThreshold(speed)
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

            double waitSec = SafeNonNegative(task.WaitSecondsPerPoint, 0.0);
            if (waitSec > 0.0)
            {
                var elapsed = ElapsedSeconds(_waypointArriveTicks.Value, nowTicks);
                if (elapsed < waitSec)
                {
                    Phase = TaskPhase.Arrived;
                    LastStatusReason = $"Waiting at waypoint {_currentIndex + 1}/{_routePoints.Count}";

                    RefreshReport(
                        reason: LastStatusReason,
                        state: state,
                        insights: insights,
                        distanceToWaypoint: distToWaypoint,
                        distanceToFinal: distToFinal,
                        speedMps: speedMps,
                        effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                        waitElapsedSeconds: elapsed,
                        waitRemainingSeconds: waitSec - elapsed
                    );

                    return;
                }
            }

            _waypointArriveTicks = null;
            bool isLastPoint = _currentIndex == _routePoints.Count - 1;

            if (!isLastPoint)
            {
                _completedWaypointCount = Math.Max(_completedWaypointCount, _currentIndex + 1);
                _currentIndex++;
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

            if (task.HoldOnArrive)
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

            _completedTaskCount++;
            LastStatusReason = task.IsExternallyCompleted
                ? "External scenario task reached final waypoint"
                : "Task completed";

            RefreshReport(
                reason: task.IsExternallyCompleted ? "EXTERNAL_SCENARIO_TASK_REACHED" : "TASK_COMPLETED",
                state: state,
                insights: insights,
                distanceToWaypoint: distToWaypoint,
                distanceToFinal: distToFinal,
                speedMps: speedMps,
                effectiveArrivalThresholdM: effectiveArrivalThresholdM,
                progressPercentOverride: 100.0
            );

            if (task.IsExternallyCompleted)
            {
                /*
                 * Scenario-owned task'larda TaskManager görevi temizlemez.
                 *
                 * Sebep:
                 * - TaskManager sadece navigasyon hedefinin geometrik olarak yakalandığını söyler.
                 * - Scenario objective tamamlandı mı kararını RuntimeScenarioController /
                 *   RuntimeScenarioObjectiveTracker verir.
                 * - Böylece hız/settle/tolerance şartları sağlanmadan CurrentTask=null olmaz.
                 */
                Phase = TaskPhase.Arrived;
                LastStatusReason = "External scenario owns task completion";

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

            CurrentTask = null;
            _routePoints.Clear();
            ResetInternalState(clearLastIdentity: false);
            Phase = TaskPhase.None;

            TryStartNextQueuedTask("TASK_COMPLETED", state, insights);
        }

        private void TrackProgress(
            long nowTicks,
            double dist3D,
            VehicleState? state,
            Insights? insights)
        {
            if (_waypointStartDistance is null)
                _waypointStartDistance = dist3D;

            if (_lastProgressDist is null)
            {
                _lastProgressDist = dist3D;
                _lastProgressTicks = nowTicks;
                return;
            }

            double previous = _lastProgressDist.Value;
            double delta = previous - dist3D;

            if (delta >= _minProgressDeltaM)
            {
                _lastProgressDist = dist3D;
                _lastProgressTicks = nowTicks;
                return;
            }

            if (_lastProgressTicks is long tProg &&
                ElapsedSeconds(tProg, nowTicks) > _maxNoProgressSeconds)
            {
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
            double effectiveArrivalThreshold = ComputeEffectiveArrivalThreshold(speed);

            if (_obstacleSinceTicks is null)
            {
                _obstacleSinceTicks = nowTicks;
                LastStatusReason = "Obstacle detected, temporary hold";

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

            var elapsed = ElapsedSeconds(_obstacleSinceTicks.Value, nowTicks);
            if (elapsed > _maxObstacleHoldSeconds)
            {
                AbortInternal("Engel uzun süre kaldı, görev iptal edildi.", state, insights);
            }
            else
            {
                if (Phase != TaskPhase.Arrived)
                    Phase = TaskPhase.Active;

                LastStatusReason = "Obstacle persists, waiting";

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
        }

        private void AbortInternal(
            string reason,
            VehicleState? state,
            Insights? insights)
        {
            LastStatusReason = reason;
            Phase = TaskPhase.Aborted;

            double speed = ComputeSpeedMps(state);

            RefreshReport(
                reason: reason,
                state: state,
                insights: insights,
                distanceToWaypoint: EstimateCurrentDistance(state),
                distanceToFinal: EstimateFinalDistance(state),
                speedMps: speed,
                effectiveArrivalThresholdM: ComputeEffectiveArrivalThreshold(speed)
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
        }
    }
}