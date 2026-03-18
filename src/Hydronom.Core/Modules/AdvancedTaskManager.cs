using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedTaskManager v2.0
    /// ------------------------------------------------------------
    /// Özellikler:
    /// - Tek nokta goto (SimpleTaskManager ile geri uyumlu).
    /// - Çok noktalı rota (Waypoints) desteği.
    /// - Loop / devriye modu.
    /// - Her waypoint'te bekleme süresi.
    /// - İlerleme takibi ve "takılma" tespiti.
    /// - Görev zaman aşımı.
    /// - Engel nedeniyle geçici bekleme / uzun engelde iptal.
    /// - Hold görevlerinde son noktada Arrived fazında kalma.
    ///
    /// Notlar:
    /// - Abort olduğunda görev temizlenir ama faz "Aborted" korunur.
    /// - Insights.HasObstacleAhead doğrudan kullanılır.
    /// - 3D mesafe üzerinden waypoint varışı izlenir.
    /// </summary>
    public class AdvancedTaskManager : ITaskManager
    {
        public TaskDefinition? CurrentTask { get; private set; }
        public TaskPhase Phase { get; private set; } = TaskPhase.None;

        // Aktif rota
        private readonly List<Vec3> _routePoints = new();
        private int _currentIndex = 0;

        // Zaman / ilerleme izleme
        private DateTime? _taskStartUtc;
        private DateTime? _waypointArriveUtc;
        private double? _lastProgressDist;
        private DateTime? _lastProgressUtc;
        private DateTime? _obstacleSinceUtc;

        // Son durum nedeni (opsiyonel debug)
        public string? LastStatusReason { get; private set; }

        // Eşikler
        private readonly double _arriveThresholdM;
        private readonly TimeSpan _maxTaskDuration;
        private readonly TimeSpan _maxNoProgress;
        private readonly TimeSpan _maxObstacleHold;
        private readonly double _minProgressDeltaM;

        public AdvancedTaskManager(
            double arriveThresholdM = 0.75,
            double maxTaskDurationSeconds = 600.0,
            double maxNoProgressSeconds = 60.0,
            double maxObstacleHoldSeconds = 120.0,
            double minProgressDeltaM = 0.25)
        {
            _arriveThresholdM = Math.Max(0.05, arriveThresholdM);
            _maxTaskDuration = TimeSpan.FromSeconds(Math.Max(1.0, maxTaskDurationSeconds));
            _maxNoProgress = TimeSpan.FromSeconds(Math.Max(1.0, maxNoProgressSeconds));
            _maxObstacleHold = TimeSpan.FromSeconds(Math.Max(1.0, maxObstacleHoldSeconds));
            _minProgressDeltaM = Math.Max(0.01, minProgressDeltaM);
        }

        /// <summary>
        /// Yeni görev yüklenir.
        /// </summary>
        public void SetTask(TaskDefinition task)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            ResetInternalState();

            CurrentTask = task;
            Phase = TaskPhase.Active;
            LastStatusReason = "Task started";
            _taskStartUtc = DateTime.UtcNow;

            // Hold davranışını isimden türet (yalnız kullanıcı açıkça vermediyse)
            if (!task.HoldOnArrive)
                task.HoldOnArrive = InferHoldFromName(task.Name);

            // Çoklu waypoint varsa onları kullan
            if (task.Waypoints is { Count: > 0 })
            {
                _routePoints.AddRange(task.Waypoints);
            }
            // Yoksa Target tek waypoint gibi ele alınır
            else if (task.Target is Vec3 t)
            {
                _routePoints.Add(t);
            }

            if (_routePoints.Count == 0)
            {
                AbortInternal("Görevde hedef noktası yok.");
                return;
            }
        }

        /// <summary>
        /// Her döngüde çağrılır.
        /// </summary>
        public void Update(Insights insights, VehicleState? state = null)
        {
            if (CurrentTask is null || _routePoints.Count == 0)
            {
                if (Phase != TaskPhase.Aborted)
                    Phase = TaskPhase.None;
                return;
            }

            var now = DateTime.UtcNow;

            // Global görev zaman aşımı
            if (_taskStartUtc is DateTime t0 && now - t0 > _maxTaskDuration)
            {
                AbortInternal("Görev zaman aşımına uğradı.");
                return;
            }

            // Engel yönetimi state olmasa da çalışabilir
            HandleObstacleIfAny(insights, now);
            if (Phase == TaskPhase.Aborted)
                return;

            // Konum yoksa ilerleme / varış hesaplayamayız
            if (state is null)
            {
                if (Phase != TaskPhase.Arrived)
                    Phase = TaskPhase.Active;

                LastStatusReason = "State unavailable";
                return;
            }

            var s = state.Value;
            var task = CurrentTask!;
            var target = _routePoints[_currentIndex];

            double dist3D = Distance3D(s.Position, target);

            // İlerleme takibi
            TrackProgress(now, dist3D);
            if (Phase == TaskPhase.Aborted)
                return;

            // Varış kontrolü
            if (dist3D <= _arriveThresholdM)
            {
                HandleWaypointArrival(now, task);
                return;
            }

            // Hedefe gidiyor
            _waypointArriveUtc = null;
            Phase = TaskPhase.Active;
            LastStatusReason = $"Navigating to waypoint {_currentIndex + 1}/{_routePoints.Count}";
        }

        public void ClearTask()
        {
            CurrentTask = null;
            _routePoints.Clear();
            ResetInternalState();

            // Abort sonrası faz korunur; normal temizlemede None yapılır
            if (Phase != TaskPhase.Aborted)
                Phase = TaskPhase.None;
        }

        // --------------------------------------------------------------------
        // Dahili yardımcılar
        // --------------------------------------------------------------------

        private void ResetInternalState()
        {
            _currentIndex = 0;
            _taskStartUtc = null;
            _waypointArriveUtc = null;
            _lastProgressDist = null;
            _lastProgressUtc = null;
            _obstacleSinceUtc = null;
            LastStatusReason = null;
        }

        private void AbortInternal(string reason)
        {
            LastStatusReason = reason;
            Phase = TaskPhase.Aborted;

            CurrentTask = null;
            _routePoints.Clear();
            _currentIndex = 0;
            _taskStartUtc = null;
            _waypointArriveUtc = null;
            _lastProgressDist = null;
            _lastProgressUtc = null;
            _obstacleSinceUtc = null;
        }

        private void HandleWaypointArrival(DateTime now, TaskDefinition task)
        {
            // Waypoint'e ilk giriş
            if (_waypointArriveUtc is null)
            {
                _waypointArriveUtc = now;
                Phase = TaskPhase.Arrived;
                LastStatusReason = $"Arrived at waypoint {_currentIndex + 1}/{_routePoints.Count}";
                return;
            }

            double waitSec = task.WaitSecondsPerPoint;
            if (waitSec > 0.0)
            {
                var elapsed = (now - _waypointArriveUtc.Value).TotalSeconds;
                if (elapsed < waitSec)
                {
                    Phase = TaskPhase.Arrived;
                    LastStatusReason = $"Waiting at waypoint {_currentIndex + 1}/{_routePoints.Count}";
                    return;
                }
            }

            _waypointArriveUtc = null;
            bool isLastPoint = (_currentIndex == _routePoints.Count - 1);

            if (!isLastPoint)
            {
                _currentIndex++;
                Phase = TaskPhase.Active;
                LastStatusReason = $"Proceeding to waypoint {_currentIndex + 1}/{_routePoints.Count}";
                ResetProgressTracking();
                return;
            }

            if (task.Loop)
            {
                _currentIndex = 0;
                Phase = TaskPhase.Active;
                LastStatusReason = "Looping route from beginning";
                ResetProgressTracking();
                return;
            }

            if (task.HoldOnArrive)
            {
                Phase = TaskPhase.Arrived;
                LastStatusReason = "Holding on final waypoint";
            }
            else
            {
                LastStatusReason = "Task completed";
                CurrentTask = null;
                _routePoints.Clear();
                ResetInternalState();
                Phase = TaskPhase.None;
            }
        }

        private void TrackProgress(DateTime now, double dist3D)
        {
            if (_lastProgressDist is null)
            {
                _lastProgressDist = dist3D;
                _lastProgressUtc = now;
                return;
            }

            double previous = _lastProgressDist.Value;
            double delta = previous - dist3D;

            if (delta >= _minProgressDeltaM)
            {
                _lastProgressDist = dist3D;
                _lastProgressUtc = now;
                return;
            }

            if (_lastProgressUtc is DateTime tProg && now - tProg > _maxNoProgress)
            {
                AbortInternal("Görev ilerleme göstermiyor (takılmış olabilir).");
            }
        }

        private void ResetProgressTracking()
        {
            _lastProgressDist = null;
            _lastProgressUtc = null;
        }

        private void HandleObstacleIfAny(Insights insights, DateTime now)
        {
            bool obstacleAhead = insights.HasObstacleAhead;

            if (!obstacleAhead)
            {
                _obstacleSinceUtc = null;
                return;
            }

            if (_obstacleSinceUtc is null)
            {
                _obstacleSinceUtc = now;
                LastStatusReason = "Obstacle detected, temporary hold";
                return;
            }

            var elapsed = now - _obstacleSinceUtc.Value;
            if (elapsed > _maxObstacleHold)
            {
                AbortInternal("Engel uzun süre kaldı, görev iptal edildi.");
            }
            else
            {
                if (Phase != TaskPhase.Arrived)
                    Phase = TaskPhase.Active;

                LastStatusReason = "Obstacle persists, waiting";
            }
        }

        private static bool InferHoldFromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var n = name.Trim().ToLowerInvariant();
            return n.Contains("hold");
        }

        private static double Distance3D(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}