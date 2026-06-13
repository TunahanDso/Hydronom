using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
        /*
         * Bu dosyanın görevi:
         * - AdvancedTaskReport üretmek
         * - Görev ilerlemesini yüzde olarak hesaplamak
         * - Aktif hedef / final hedef raporlamak
         * - Mission orchestration durumunu rapora izlenebilir şekilde yansıtmak
         * - Route-free görevleri doğru raporlamak
         *
         * Bu dosyanın görevi değildir:
         * - Heading üretmek
         * - Force / torque üretmek
         * - Planner yerine rota seçmek
         * - Controller hedefini değiştirmek
         * - Obstacle-bypass kararı vermek
         * - Görev adından davranış tahmin etmek
         */

        private void RefreshReport(
            string? reason,
            VehicleState? state,
            Insights? insights,
            double distanceToWaypoint,
            double distanceToFinal,
            double speedMps,
            double effectiveArrivalThresholdM,
            double? waitElapsedSeconds = null,
            double? waitRemainingSeconds = null,
            double? progressPercentOverride = null)
        {
            long nowTicks = NowTicks();

            double elapsedTaskSeconds = _taskStartTicks is long start
                ? Math.Max(0.0, ElapsedSeconds(start, nowTicks))
                : 0.0;

            double obstacleElapsedSeconds = _obstacleSinceTicks is long obsStart
                ? Math.Max(0.0, ElapsedSeconds(obsStart, nowTicks))
                : 0.0;

            double noProgressSeconds = _lastProgressTicks is long progStart
                ? Math.Max(0.0, ElapsedSeconds(progStart, nowTicks))
                : 0.0;

            double progressPercent = progressPercentOverride ?? ComputeProgressPercent(distanceToWaypoint);
            string safeReason = BuildReportReason(reason);

            /*
             * Task Manager bu bayrağı rota/heading üretmek için kullanmaz.
             * Sadece görev raporu, guard ve no-progress yorumlaması için taşır.
             */
            bool obstacleAhead = insights?.HasObstacleAhead ?? false;

            LastReport = new AdvancedTaskReport(
                Phase: Phase,
                Reason: safeReason,
                TaskName: GetTaskNameForReport(),
                HasTask: CurrentTask is not null,
                CurrentWaypointIndex: GetCurrentWaypointIndexForReport(),
                WaypointCount: _routePoints.Count,
                CompletedWaypointCount: _completedWaypointCount,
                CurrentTarget: GetCurrentTargetForReport(),
                FinalTarget: GetFinalTargetForReport(),
                DistanceToWaypointM: SafeNonNegative(distanceToWaypoint, 0.0),
                DistanceToFinalM: SafeNonNegative(distanceToFinal, 0.0),
                ProgressPercent: Math.Clamp(progressPercent, 0.0, 100.0),
                ElapsedTaskSeconds: elapsedTaskSeconds,
                NoProgressSeconds: noProgressSeconds,
                ObstacleHoldSeconds: obstacleElapsedSeconds,
                WaitElapsedSeconds: SafeNonNegative(waitElapsedSeconds ?? 0.0, 0.0),
                WaitRemainingSeconds: SafeNonNegative(waitRemainingSeconds ?? 0.0, 0.0),
                ObstacleAhead: obstacleAhead,
                Position: state?.Position,
                SpeedMps: SafeNonNegative(speedMps, 0.0),
                EffectiveArrivalThresholdM: SafeNonNegative(effectiveArrivalThresholdM, _arriveThresholdM),
                QueuedTaskCount: _taskQueue.Count,
                CompletedTaskCount: _completedTaskCount,
                StartedTaskCount: _startedTaskCount
            );
        }

        private string BuildReportReason(string? reason)
        {
            string baseReason = SafeReason(reason, LastStatusReason ?? "UNKNOWN");

            var task = CurrentTask;
            string taskKind = task?.Kind.ToString() ?? TaskKind.Unknown.ToString();
            string priority = task?.Priority.ToString() ?? TaskPriority.Normal.ToString();
            string completionAuthority = task?.CompletionAuthority.ToString()
                ?? TaskCompletionAuthority.TaskManager.ToString();

            bool hasRouteFreeTask = task is not null && _routePoints.Count == 0;
            bool isExternalTask = task?.IsExternallyCompleted ?? false;

            /*
             * AdvancedTaskReport modeli henüz lane/property olarak genişletilmediği için
             * orchestration bilgilerini Reason içine kısa ve makine-okunabilir suffix olarak ekliyoruz.
             *
             * AdvancedTaskReport.cs güncellendiğinde bu alanlar doğrudan property olmalı:
             * - TaskKind
             * - TaskPriority
             * - CompletionAuthority
             * - PendingGeneratedSubtaskCount
             * - SuspendedPrimaryTaskCount
             * - ActiveParallelTaskCount
             * - ActiveGuardTaskCount
             * - HasRouteFreeCurrentTask
             * - IsCurrentTaskExternal
             */
            bool hasOrchestrationState =
                _taskQueue.Count > 0 ||
                _generatedSubtasks.Count > 0 ||
                _suspendedPrimaryTasks.Count > 0 ||
                _parallelTasks.Count > 0 ||
                _guardTasks.Count > 0 ||
                hasRouteFreeTask ||
                isExternalTask;

            if (!hasOrchestrationState && task is null)
                return baseReason;

            return
                $"{baseReason} | " +
                $"orchRev={_orchestrationRevision} " +
                $"kind={taskKind} " +
                $"prio={priority} " +
                $"auth={completionAuthority} " +
                $"primaryQ={_taskQueue.Count} " +
                $"subQ={_generatedSubtasks.Count} " +
                $"suspended={_suspendedPrimaryTasks.Count} " +
                $"parallel={_parallelTasks.Count} " +
                $"guard={_guardTasks.Count} " +
                $"routeFree={hasRouteFreeTask} " +
                $"external={isExternalTask}";
        }

        private double ComputeProgressPercent(double currentDistanceToWaypoint)
        {
            if (CurrentTask is null)
            {
                return Phase == TaskPhase.Arrived ? 100.0 : 0.0;
            }

            var task = CurrentTask.Normalize();

            if (_routePoints.Count == 0)
                return ComputeRouteFreeProgressPercent(task);

            int routeCount = Math.Max(1, _routePoints.Count);
            int completed = Math.Clamp(_completedWaypointCount, 0, routeCount);

            double waypointPart = completed / (double)routeCount;
            double localPart = 0.0;

            if (_waypointStartDistance is double startDist &&
                double.IsFinite(startDist) &&
                startDist > _arriveThresholdM)
            {
                double safeCurrentDistance = SafeNonNegative(currentDistanceToWaypoint, startDist);
                double travelled = Math.Max(0.0, startDist - safeCurrentDistance);
                double denominator = Math.Max(0.001, startDist - _arriveThresholdM);

                localPart = travelled / denominator;
                localPart = Math.Clamp(localPart, 0.0, 1.0);
            }

            double total = (waypointPart + localPart / routeCount) * 100.0;
            return Math.Clamp(total, 0.0, 100.0);
        }

        private double ComputeRouteFreeProgressPercent(TaskDefinition task)
        {
            if (Phase == TaskPhase.Arrived)
                return 100.0;

            if (_taskStartTicks is not long start)
                return 0.0;

            double elapsedSeconds = ElapsedSeconds(start, NowTicks());

            double requiredSeconds = Math.Max(
                SafeNonNegative(task.Completion.RequiredHoldSeconds, 0.0),
                SafeNonNegative(task.WaitSecondsPerPoint, 0.0));

            if (requiredSeconds <= 0.0)
            {
                /*
                 * External completion, sensor confirmation, image validation, fleet condition
                 * gibi görevlerde Task Manager yüzdeyi kendi kendine uydurmaz.
                 */
                return 0.0;
            }

            return Math.Clamp((elapsedSeconds / requiredSeconds) * 100.0, 0.0, 100.0);
        }

        private int GetCurrentWaypointIndexForReport()
        {
            if (CurrentTask is null)
                return -1;

            if (_routePoints.Count == 0)
                return -1;

            return ClampRouteIndex(_currentIndex);
        }

        private Vec3? GetCurrentTargetForReport()
        {
            if (CurrentTask is null)
                return _lastTarget;

            if (_routePoints.Count == 0)
                return CurrentTask.Target ?? _lastTarget;

            int index = ClampRouteIndex(_currentIndex);
            return _routePoints[index];
        }

        private Vec3? GetFinalTargetForReport()
        {
            if (CurrentTask is null)
                return _lastTarget;

            if (_routePoints.Count == 0)
                return CurrentTask.Target ?? _lastTarget;

            return _routePoints[^1];
        }

        private string? GetTaskNameForReport()
        {
            if (CurrentTask is not null)
                return SafeTaskName(CurrentTask);

            return _lastTaskName;
        }
    }
}