using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
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

            string safeReason = string.IsNullOrWhiteSpace(reason)
                ? LastStatusReason ?? "UNKNOWN"
                : reason.Trim();

            bool obstacleAhead = insights?.HasObstacleAhead ?? false;

            LastReport = new AdvancedTaskReport(
                Phase: Phase,
                Reason: safeReason,
                TaskName: CurrentTask?.Name ?? _lastTaskName,
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
                WaitElapsedSeconds: waitElapsedSeconds ?? 0.0,
                WaitRemainingSeconds: waitRemainingSeconds ?? 0.0,
                ObstacleAhead: obstacleAhead,
                Position: state?.Position,
                SpeedMps: SafeNonNegative(speedMps, 0.0),
                EffectiveArrivalThresholdM: SafeNonNegative(effectiveArrivalThresholdM, _arriveThresholdM),
                QueuedTaskCount: _taskQueue.Count,
                CompletedTaskCount: _completedTaskCount,
                StartedTaskCount: _startedTaskCount
            );
        }

        private double ComputeProgressPercent(double currentDistanceToWaypoint)
        {
            if (CurrentTask is null || _routePoints.Count == 0)
            {
                if (Phase == TaskPhase.Arrived)
                    return 100.0;

                return 0.0;
            }

            double waypointPart = _routePoints.Count <= 0
                ? 0.0
                : _completedWaypointCount / (double)_routePoints.Count;

            double localPart = 0.0;

            if (_waypointStartDistance is double startDist && startDist > _arriveThresholdM)
            {
                double travelled = Math.Max(0.0, startDist - currentDistanceToWaypoint);
                localPart = travelled / Math.Max(0.001, startDist - _arriveThresholdM);
                localPart = Math.Clamp(localPart, 0.0, 1.0);
            }

            double total = (waypointPart + localPart / Math.Max(1, _routePoints.Count)) * 100.0;
            return Math.Clamp(total, 0.0, 100.0);
        }

        private int GetCurrentWaypointIndexForReport()
        {
            if (CurrentTask is null || _routePoints.Count == 0)
                return -1;

            return Math.Clamp(_currentIndex, 0, _routePoints.Count - 1);
        }

        private Vec3? GetCurrentTargetForReport()
        {
            if (CurrentTask is null || _routePoints.Count == 0)
                return _lastTarget;

            int index = Math.Clamp(_currentIndex, 0, _routePoints.Count - 1);
            return _routePoints[index];
        }

        private Vec3? GetFinalTargetForReport()
        {
            if (CurrentTask is null || _routePoints.Count == 0)
                return _lastTarget;

            return _routePoints[^1];
        }
    }
}
