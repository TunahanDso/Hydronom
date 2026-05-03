using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedTaskManager v4.1
    /// ------------------------------------------------------------
    /// Mission execution / gÃ¶rev yÃ¶netim katmanÄ±.
    ///
    /// Ana sorumluluklar:
    /// - Aktif gÃ¶rev yÃ¶netimi
    /// - GÃ¶rev kuyruÄŸu
    /// - Waypoint rota yÃ¼rÃ¼tme
    /// - HÄ±z baÄŸÄ±mlÄ± varÄ±ÅŸ zarfÄ±
    /// - Monotonic clock tabanlÄ± sÃ¼re Ã¶lÃ§Ã¼mÃ¼
    /// - No-progress / obstacle hold / timeout tespiti
    /// - AÃ§Ä±klanabilir AdvancedTaskReport Ã¼retimi
    ///
    /// Bu dosya sadece public API ve ana Update akÄ±ÅŸÄ±nÄ± taÅŸÄ±r.
    /// GeÃ§iÅŸler, raporlama ve yardÄ±mcÄ±lar ayrÄ± partial dosyalardadÄ±r.
    /// </summary>
    public partial class AdvancedTaskManager : ITaskManager
    {
        public TaskDefinition? CurrentTask { get; private set; }
        public TaskPhase Phase { get; private set; } = TaskPhase.None;

        private readonly List<Vec3> _routePoints = new();
        private readonly Queue<TaskDefinition> _taskQueue = new();

        private int _currentIndex;
        private int _completedWaypointCount;
        private int _completedTaskCount;
        private int _startedTaskCount;

        private long? _taskStartTicks;
        private long? _waypointArriveTicks;
        private long? _lastProgressTicks;
        private long? _obstacleSinceTicks;

        private double? _lastProgressDist;
        private double? _waypointStartDistance;

        private string? _lastTaskName;
        private Vec3? _lastTarget;

        public string? LastStatusReason { get; private set; }

        public AdvancedTaskReport LastReport { get; private set; } =
            AdvancedTaskReport.Empty;

        private readonly double _arriveThresholdM;
        private readonly double _dynamicAcceptanceTauSeconds;
        private readonly double _maxArrivalThresholdM;

        private readonly double _maxTaskDurationSeconds;
        private readonly double _maxNoProgressSeconds;
        private readonly double _maxObstacleHoldSeconds;
        private readonly double _minProgressDeltaM;

        public AdvancedTaskManager(
            double arriveThresholdM = 0.75,
            double maxTaskDurationSeconds = 600.0,
            double maxNoProgressSeconds = 60.0,
            double maxObstacleHoldSeconds = 120.0,
            double minProgressDeltaM = 0.25,
            double dynamicAcceptanceTauSeconds = 0.60,
            double maxArrivalThresholdM = 3.00)
        {
            _arriveThresholdM = ClampPositive(arriveThresholdM, 0.75, min: 0.05);

            _maxTaskDurationSeconds = ClampPositive(maxTaskDurationSeconds, 600.0, min: 1.0);
            _maxNoProgressSeconds = ClampPositive(maxNoProgressSeconds, 60.0, min: 1.0);
            _maxObstacleHoldSeconds = ClampPositive(maxObstacleHoldSeconds, 120.0, min: 1.0);
            _minProgressDeltaM = ClampPositive(minProgressDeltaM, 0.25, min: 0.01);

            _dynamicAcceptanceTauSeconds = ClampNonNegative(dynamicAcceptanceTauSeconds, fallback: 0.60);
            _maxArrivalThresholdM = Math.Max(
                _arriveThresholdM,
                ClampPositive(maxArrivalThresholdM, 3.0, min: _arriveThresholdM)
            );

            RefreshReport(
                reason: "NO_TASK",
                state: null,
                insights: null,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: 0.0,
                effectiveArrivalThresholdM: _arriveThresholdM
            );
        }

        /// <summary>
        /// Yeni gÃ¶rev yÃ¼kler. Aktif gÃ¶revin yerine geÃ§er, mevcut kuyruk korunur.
        /// </summary>
        public void SetTask(TaskDefinition task)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            StartTaskInternal(task, reason: "TASK_STARTED", preserveQueue: true);
        }

        /// <summary>
        /// GÃ¶revi kuyruÄŸa ekler. Aktif gÃ¶rev yoksa hemen baÅŸlatÄ±r.
        /// </summary>
        public void EnqueueTask(TaskDefinition task)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            if (CurrentTask is null || _routePoints.Count == 0)
            {
                StartTaskInternal(task, reason: "TASK_STARTED_FROM_QUEUE", preserveQueue: true);
                return;
            }

            _taskQueue.Enqueue(task);

            RefreshReport(
                reason: "TASK_ENQUEUED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Birden fazla gÃ¶revi sÄ±rayla kuyruÄŸa ekler.
        /// </summary>
        public void EnqueueTasks(IEnumerable<TaskDefinition> tasks)
        {
            if (tasks is null)
                throw new ArgumentNullException(nameof(tasks));

            foreach (var task in tasks)
                EnqueueTask(task);
        }

        /// <summary>
        /// Bekleyen gÃ¶rev kuyruÄŸunu temizler. Aktif gÃ¶rev etkilenmez.
        /// </summary>
        public void ClearQueue()
        {
            _taskQueue.Clear();

            RefreshReport(
                reason: "TASK_QUEUE_CLEARED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Her runtime dÃ¶ngÃ¼sÃ¼nde Ã§aÄŸrÄ±lÄ±r.
        /// </summary>
        public void Update(Insights insights, VehicleState? state = null)
        {
            if (CurrentTask is null || _routePoints.Count == 0)
            {
                if (Phase != TaskPhase.Aborted)
                    Phase = TaskPhase.None;

                RefreshReport(
                    reason: LastStatusReason ?? "NO_ACTIVE_TASK",
                    state: state,
                    insights: insights,
                    distanceToWaypoint: 0.0,
                    distanceToFinal: 0.0,
                    speedMps: ComputeSpeedMps(state),
                    effectiveArrivalThresholdM: _arriveThresholdM
                );

                return;
            }

            long nowTicks = NowTicks();
            var task = CurrentTask!;

            if (_taskStartTicks is long t0 &&
                ElapsedSeconds(t0, nowTicks) > _maxTaskDurationSeconds)
            {
                AbortInternal("GÃ¶rev zaman aÅŸÄ±mÄ±na uÄŸradÄ±.", state, insights);
                return;
            }

            HandleObstacleIfAny(insights, nowTicks, state);
            if (Phase == TaskPhase.Aborted)
                return;

            if (state is null)
            {
                if (Phase != TaskPhase.Arrived)
                    Phase = TaskPhase.Active;

                LastStatusReason = "State unavailable";

                RefreshReport(
                    reason: LastStatusReason,
                    state: null,
                    insights: insights,
                    distanceToWaypoint: 0.0,
                    distanceToFinal: 0.0,
                    speedMps: 0.0,
                    effectiveArrivalThresholdM: _arriveThresholdM
                );

                return;
            }

            var s = state.Value;
            var target = _routePoints[_currentIndex];

            double speedMps = ComputeSpeedMps(state);
            double effectiveArrivalThresholdM = ComputeEffectiveArrivalThreshold(speedMps);

            double distToWaypoint = Distance3D(s.Position, target);
            double distToFinal = Distance3D(s.Position, _routePoints[^1]);

            TrackProgress(nowTicks, distToWaypoint, state, insights);
            if (Phase == TaskPhase.Aborted)
                return;

            if (distToWaypoint <= effectiveArrivalThresholdM)
            {
                HandleWaypointArrival(
                    nowTicks,
                    task,
                    state,
                    insights,
                    distToWaypoint,
                    distToFinal,
                    speedMps,
                    effectiveArrivalThresholdM
                );

                return;
            }

            _waypointArriveTicks = null;
            Phase = TaskPhase.Active;
            LastStatusReason = $"Navigating to waypoint {_currentIndex + 1}/{_routePoints.Count}";

            RefreshReport(
                reason: LastStatusReason,
                state: state,
                insights: insights,
                distanceToWaypoint: distToWaypoint,
                distanceToFinal: distToFinal,
                speedMps: speedMps,
                effectiveArrivalThresholdM: effectiveArrivalThresholdM
            );
        }

        public void ClearTask()
        {
            CurrentTask = null;
            _routePoints.Clear();
            ResetInternalState(clearLastIdentity: false);

            if (Phase != TaskPhase.Aborted)
                Phase = TaskPhase.None;

            LastStatusReason = "Task cleared";

            RefreshReport(
                reason: LastStatusReason,
                state: null,
                insights: null,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: 0.0,
                effectiveArrivalThresholdM: _arriveThresholdM
            );
        }
    }
}
