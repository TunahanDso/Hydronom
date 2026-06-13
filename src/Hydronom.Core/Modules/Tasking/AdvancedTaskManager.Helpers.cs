using System;
using System.Diagnostics;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedTaskManager
    {
        /*
         * Bu dosyanın görevi:
         * - Task Manager iç durumunu sıfırlamak
         * - Görev ilerleme mesafelerini hesaplamak
         * - Varış eşiğini hesaplamak
         * - Monotonic clock ölçümü yapmak
         * - Güvenli numeric sanitize / clamp helper'ları sağlamak
         * - TaskDefinition tabanlı yapısal görev semantiği okumak
         *
         * Bu dosyanın görevi değildir:
         * - Heading üretmek
         * - Force / torque üretmek
         * - Obstacle avoidance yapmak
         * - Planner yerine rota seçmek
         * - Controller hedefi üretmek
         * - Görev adından davranış tahmin etmek
         */

        private void ResetInternalState(bool clearLastIdentity)
        {
            _currentIndex = 0;

            _taskStartTicks = null;
            _waypointArriveTicks = null;
            _lastProgressDist = null;
            _lastProgressTicks = null;
            _waypointStartDistance = null;
            _obstacleSinceTicks = null;

            _completedWaypointCount = 0;

            LastStatusReason = null;

            if (clearLastIdentity)
            {
                _lastTaskName = null;
                _lastTarget = null;
            }
        }

        private void ResetProgressTracking()
        {
            _lastProgressDist = null;
            _lastProgressTicks = null;
            _waypointStartDistance = null;
        }

        private double EstimateCurrentDistance(VehicleState? state)
        {
            if (state is null)
                return 0.0;

            if (!HasRunnableRoute())
                return 0.0;

            if (!IsValidRouteIndex(_currentIndex))
                return 0.0;

            return Distance3D(state.Value.Position, _routePoints[_currentIndex]);
        }

        private double EstimateFinalDistance(VehicleState? state)
        {
            if (state is null)
                return 0.0;

            if (!HasRunnableRoute())
                return 0.0;

            return Distance3D(state.Value.Position, _routePoints[^1]);
        }

        private bool HasRunnableRoute()
        {
            return _routePoints.Count > 0;
        }

        private bool IsValidRouteIndex(int index)
        {
            return index >= 0 && index < _routePoints.Count;
        }

        private int ClampRouteIndex(int index)
        {
            if (_routePoints.Count == 0)
                return 0;

            return Math.Clamp(index, 0, _routePoints.Count - 1);
        }

        private double ComputeEffectiveArrivalThreshold(double speedMps)
        {
            /*
             * Bu eşik yalnızca görev completion zarfıdır.
             * Controller hedefi, trajectory lookahead veya planner davranışı üretmez.
             *
             * Hız arttıkça kabul yarıçapı bir miktar genişler.
             * Böylece hızlı geçişlerde waypoint etrafında gereksiz osilasyon azalır.
             */
            double speed = SafeNonNegative(speedMps, 0.0);
            double threshold = _arriveThresholdM + speed * _dynamicAcceptanceTauSeconds;

            return Math.Clamp(
                threshold,
                _arriveThresholdM,
                _maxArrivalThresholdM);
        }

        private double ComputeTaskArrivalThreshold(TaskDefinition? task, double speedMps)
        {
            double dynamicThreshold = ComputeEffectiveArrivalThreshold(speedMps);

            if (task is null)
                return dynamicThreshold;

            var normalized = task.Normalize();

            double taskThreshold = normalized.Completion.AcceptanceRadiusM;
            if (!double.IsFinite(taskThreshold) || taskThreshold <= 0.0)
                taskThreshold = _arriveThresholdM;

            double upper = Math.Max(_maxArrivalThresholdM, taskThreshold);

            return Math.Clamp(
                Math.Max(dynamicThreshold, taskThreshold),
                _arriveThresholdM,
                upper);
        }

        private static double ComputeSpeedMps(VehicleState? state)
        {
            if (state is null)
                return 0.0;

            var v = state.Value.Velocity;

            double vx = Safe(v.X);
            double vy = Safe(v.Y);
            double vz = Safe(v.Z);

            double speedSquared = vx * vx + vy * vy + vz * vz;
            if (!double.IsFinite(speedSquared) || speedSquared <= 0.0)
                return 0.0;

            double speed = Math.Sqrt(speedSquared);
            return double.IsFinite(speed) ? speed : 0.0;
        }

        private static bool IsStationKeepingTask(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return
                normalized.IsStationKeeping ||
                normalized.Kind is TaskKind.HoldStation or TaskKind.Wait or TaskKind.MaintainDepth ||
                normalized.Behavior.IsStationKeeping ||
                normalized.Completion.Mode == TaskCompletionMode.HoldDurationSatisfied;
        }

        private static bool IsRouteFreeTask(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return
                normalized.Target is null &&
                normalized.Waypoints.Count == 0 &&
                !RequiresRouteTarget(normalized);
        }

        private static bool RequiresRouteTarget(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return normalized.Kind is
                TaskKind.GoToWaypoint or
                TaskKind.FollowRoute or
                TaskKind.ReturnHome or
                TaskKind.HoldStation or
                TaskKind.Surface or
                TaskKind.DiveToDepth or
                TaskKind.MaintainDepth;
        }

        private static bool IsExternalTask(TaskDefinition? task)
        {
            if (task is null)
                return false;

            return task.Normalize().IsExternallyCompleted;
        }

        private static bool RequiresExternalCompletion(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return
                normalized.IsExternallyCompleted ||
                normalized.Completion.RequiresExternalAck;
        }

        private static bool IsGuardTask(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return
                normalized.IsGuardTask ||
                normalized.Behavior.IsGuardTask ||
                normalized.Priority is TaskPriority.SafetyCritical or TaskPriority.Emergency;
        }

        private static bool IsFleetTask(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return
                normalized.IsFleetTask ||
                normalized.Behavior.IsFleetTask ||
                normalized.Kind is
                    TaskKind.Rendezvous or
                    TaskKind.MaintainFormation or
                    TaskKind.CooperateWithFleet or
                    TaskKind.WaitForPartner or
                    TaskKind.RestoreFormation;
        }

        private static bool AllowsParallelExecution(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return normalized.AllowsParallelExecution;
        }

        private static bool CanGenerateSubtasks(TaskDefinition? task)
        {
            if (task is null)
                return false;

            var normalized = task.Normalize();

            return normalized.CanGenerateSubtasks;
        }

        /// <summary>
        /// Legacy fallback.
        ///
        /// Kalıcı mimaride görev semantiği string/name üzerinden çıkarılamaz.
        /// TaskDefinition.Kind / Behavior / Completion kullanılmalıdır.
        ///
        /// Build uyumluluğu için geçici olarak tutulur.
        /// Bilerek false döner; yeni davranış bu metottan beslenmemelidir.
        /// </summary>
        [Obsolete("Use TaskDefinition.Kind, TaskDefinition.Behavior or TaskDefinition.Completion instead. String-based task inference is disabled.")]
        private static bool InferHoldFromName(string? name)
        {
            return false;
        }

        private static double Distance3D(Vec3 a, Vec3 b)
        {
            double dx = Safe(a.X - b.X);
            double dy = Safe(a.Y - b.Y);
            double dz = Safe(a.Z - b.Z);

            double distSquared = dx * dx + dy * dy + dz * dz;
            if (!double.IsFinite(distSquared) || distSquared <= 0.0)
                return 0.0;

            double dist = Math.Sqrt(distSquared);
            return double.IsFinite(dist) ? dist : 0.0;
        }

        private static double Distance2D(Vec3 a, Vec3 b)
        {
            double dx = Safe(a.X - b.X);
            double dy = Safe(a.Y - b.Y);

            double distSquared = dx * dx + dy * dy;
            if (!double.IsFinite(distSquared) || distSquared <= 0.0)
                return 0.0;

            double dist = Math.Sqrt(distSquared);
            return double.IsFinite(dist) ? dist : 0.0;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                Safe(v.X),
                Safe(v.Y),
                Safe(v.Z)
            );
        }

        private static string SafeReason(string? reason, string fallback)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return string.IsNullOrWhiteSpace(fallback)
                    ? "TASK_MANAGER"
                    : fallback.Trim();

            return reason.Trim();
        }

        private static string SafeTaskName(TaskDefinition? task)
        {
            if (task is null)
                return "NO_TASK";

            return string.IsNullOrWhiteSpace(task.Name)
                ? task.Kind.ToString()
                : task.Name.Trim();
        }

        private static long NowTicks()
        {
            return Stopwatch.GetTimestamp();
        }

        private static double ElapsedSeconds(long startTicks, long nowTicks)
        {
            if (startTicks <= 0 || nowTicks <= startTicks)
                return 0.0;

            double elapsed = (nowTicks - startTicks) / (double)Stopwatch.Frequency;
            return double.IsFinite(elapsed) && elapsed >= 0.0 ? elapsed : 0.0;
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return Math.Max(0.0, fallback);

            return Math.Max(0.0, value);
        }

        private static double ClampPositive(double value, double fallback, double min)
        {
            double safeMin = double.IsFinite(min) && min > 0.0 ? min : 0.000001;

            if (!double.IsFinite(value))
                return Math.Max(safeMin, fallback);

            return Math.Max(safeMin, value);
        }

        private static double ClampNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return Math.Max(0.0, fallback);

            return Math.Max(0.0, value);
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Clamp(value, 0.0, 1.0);
        }

        private static double SafePercent(double numerator, double denominator)
        {
            if (!double.IsFinite(numerator) ||
                !double.IsFinite(denominator) ||
                denominator <= 0.0)
            {
                return 0.0;
            }

            return Clamp01(numerator / denominator);
        }
    }
}