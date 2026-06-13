using System;
using System.Collections.Generic;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedTaskManager v5.1
    /// ------------------------------------------------------------
    /// Mission / Objective Orchestrator.
    ///
    /// Bu modülün görevi:
    /// - Ana görev yürütmek
    /// - Sıralı görev kuyruğu yönetmek
    /// - Paralel görevleri takip etmek
    /// - Sistem tarafından üretilen ara görevleri yönetmek
    /// - Guard görevleri kayıt altında tutmak
    /// - Route-free görevleri aktif görev olarak taşıyabilmek
    /// - Görev fazı, ilerleme, timeout, no-progress ve completion durumlarını raporlamak
    ///
    /// Bu modülün görevi DEĞİLDİR:
    /// - Heading üretmek
    /// - Force / torque üretmek
    /// - Obstacle avoidance yapmak
    /// - Local-detour veya trajectory üretmek
    /// - Planner yerine rota seçmek
    /// - Controller hedefini ezmek
    /// - World Model'e scenario nesnesi yazmak
    ///
    /// Altın kural:
    /// Task Manager "ne yapılacağını" yönetir.
    /// "Nasıl sürüleceğini" Planner + Trajectory + Controller çözer.
    ///
    /// Bu dosya public API, mission-lane yönetimi ve ana Update akışını taşır.
    /// Geçişler, raporlama ve yardımcılar ayrı partial dosyalardadır.
    /// </summary>
    public partial class AdvancedTaskManager : IMissionOrchestrator
    {
        public TaskDefinition? CurrentTask { get; private set; }
        public TaskPhase Phase { get; private set; } = TaskPhase.None;

        private readonly List<Vec3> _routePoints = new();
        private readonly Queue<TaskDefinition> _taskQueue = new();

        /*
         * Mission lane yapısı:
         *
         * Primary:
         *   Şu anda mission progression'ı belirleyen aktif görev.
         *
         * Sequential queue:
         *   Primary tamamlandıktan sonra çalışacak ana görev kuyruğu.
         *
         * Generated subtask:
         *   Sistem tarafından ana görevi mümkün kılmak için oluşturulan ara görev.
         *   Örnek: AlignCamera, ReacquireTarget, StabilizeForCapture.
         *
         * Parallel:
         *   Primary görevle aynı anda takip edilen destek görevi.
         *   Örnek: RecordVideo, CaptureIfVisible, MaintainFleetLink.
         *
         * Guard:
         *   Güvenlik ve görev geçerliliği izleyici görevi.
         *   Örnek: MonitorBattery, MonitorGeofence, MonitorComms.
         *
         * Suspended primary:
         *   Preemptive generated subtask sırasında askıya alınan primary görev.
         *
         * Bu lane'ler Task Manager içinde görev orkestrasyonu içindir.
         * Hiçbiri doğrudan heading/force/trajectory üretmez.
         */
        private readonly Queue<MissionTaskSlot> _generatedSubtasks = new();
        private readonly Stack<MissionTaskSlot> _suspendedPrimaryTasks = new();
        private readonly List<MissionTaskSlot> _parallelTasks = new();
        private readonly List<MissionTaskSlot> _guardTasks = new();

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

        private long _orchestrationRevision;

        public string? LastStatusReason { get; private set; }

        public AdvancedTaskReport LastReport { get; private set; } =
            AdvancedTaskReport.Empty;

        public AdvancedTaskOrchestrationSnapshot LastOrchestration { get; private set; } =
            AdvancedTaskOrchestrationSnapshot.Empty;

        public IReadOnlyList<MissionTaskSlot> ParallelTasks => _parallelTasks;
        public IReadOnlyList<MissionTaskSlot> GuardTasks => _guardTasks;

        public int PendingGeneratedSubtaskCount => _generatedSubtasks.Count;
        public int PendingSequentialTaskCount => _taskQueue.Count;
        public int SuspendedPrimaryTaskCount => _suspendedPrimaryTasks.Count;

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

            RefreshOrchestrationSnapshot("NO_TASK");

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
        /// Yeni primary görev yükler.
        /// Aktif görevin yerine geçer, mevcut sıralı kuyruk korunur.
        /// </summary>
        public void SetTask(TaskDefinition task)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            StartMissionTaskInternal(
                task,
                reason: "PRIMARY_TASK_STARTED",
                preserveQueue: true);

            RefreshOrchestrationSnapshot("PRIMARY_TASK_STARTED");
        }

        /// <summary>
        /// Görevi sıralı primary kuyruğa ekler.
        /// Aktif primary yoksa hemen başlatır.
        /// </summary>
        public void EnqueueTask(TaskDefinition task)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            task = NormalizeTask(task);

            if (CurrentTask is null)
            {
                StartMissionTaskInternal(
                    task,
                    reason: "PRIMARY_TASK_STARTED_FROM_QUEUE",
                    preserveQueue: true);

                RefreshOrchestrationSnapshot("PRIMARY_TASK_STARTED_FROM_QUEUE");
                return;
            }

            _taskQueue.Enqueue(task);

            RefreshOrchestrationSnapshot("PRIMARY_TASK_ENQUEUED");

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
        /// Birden fazla görevi sıralı primary kuyruğa ekler.
        /// </summary>
        public void EnqueueTasks(IEnumerable<TaskDefinition> tasks)
        {
            if (tasks is null)
                throw new ArgumentNullException(nameof(tasks));

            foreach (var task in tasks)
                EnqueueTask(task);
        }

        /// <summary>
        /// Primary görevle aynı anda takip edilecek paralel görev ekler.
        /// Bu görev aracı sürmez; yalnızca mission orchestration içinde takip edilir.
        /// </summary>
        public MissionTaskSlot AddParallelTask(
            TaskDefinition task,
            string reason = "PARALLEL_TASK_ADDED")
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            task = NormalizeTask(task);

            var slot = CreateSlot(
                MissionTaskLane.Parallel,
                task,
                reason,
                isActive: true,
                isGenerated: false,
                mayPreemptPrimary: false);

            _parallelTasks.Add(slot);

            RefreshOrchestrationSnapshot(reason);

            RefreshReport(
                reason: reason,
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );

            return slot;
        }

        /// <summary>
        /// Güvenlik, haberleşme, enerji veya görev geçerliliği izleyicisi ekler.
        /// Guard görevleri doğrudan sürüş kararı vermez.
        /// </summary>
        public MissionTaskSlot AddGuardTask(
            TaskDefinition task,
            string reason = "GUARD_TASK_ADDED")
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            task = NormalizeTask(task);

            var slot = CreateSlot(
                MissionTaskLane.Guard,
                task,
                reason,
                isActive: true,
                isGenerated: false,
                mayPreemptPrimary: false);

            _guardTasks.Add(slot);

            RefreshOrchestrationSnapshot(reason);

            RefreshReport(
                reason: reason,
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );

            return slot;
        }

        /// <summary>
        /// Sistem tarafından üretilen ara görev ekler.
        ///
        /// Ara görev, ana görevi mümkün kılan geçici görevdir:
        /// - AlignCamera
        /// - StabilizeForCapture
        /// - ReacquireTarget
        /// - WaitForPartner
        /// - RestoreFormation
        ///
        /// startImmediately=false ise ara görev sıraya alınır.
        /// startImmediately=true ise aktif primary görev askıya alınır ve ara görev hemen başlatılır.
        ///
        /// Not:
        /// Bu yöntem rota/heading üretmez. Sadece görev orkestrasyonu yapar.
        /// Ara görevin güvenli yolu Planner tarafından çıkarılır.
        /// </summary>
        public MissionTaskSlot GenerateSubtask(
            TaskDefinition task,
            bool startImmediately = false,
            string reason = "GENERATED_SUBTASK_ADDED")
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            task = NormalizeTask(task);

            var slot = CreateSlot(
                MissionTaskLane.GeneratedSubtask,
                task,
                reason,
                isActive: startImmediately,
                isGenerated: true,
                mayPreemptPrimary: startImmediately);

            if (startImmediately)
            {
                SuspendCurrentTaskForGeneratedSubtask();

                StartMissionTaskInternal(
                    task,
                    reason: "GENERATED_SUBTASK_STARTED",
                    preserveQueue: true);
            }
            else
            {
                _generatedSubtasks.Enqueue(slot);
            }

            RefreshOrchestrationSnapshot(reason);

            RefreshReport(
                reason: reason,
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );

            return slot;
        }

        /// <summary>
        /// Bekleyen sıralı primary görev kuyruğunu temizler.
        /// Aktif görev, paralel görevler, guard görevleri, suspended görevler
        /// ve generated subtask kuyruğu etkilenmez.
        /// </summary>
        public void ClearQueue()
        {
            _taskQueue.Clear();

            RefreshOrchestrationSnapshot("PRIMARY_TASK_QUEUE_CLEARED");

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
        /// Bekleyen generated subtask kuyruğunu temizler.
        /// Aktif primary görev etkilenmez.
        /// </summary>
        public void ClearGeneratedSubtasks()
        {
            _generatedSubtasks.Clear();

            RefreshOrchestrationSnapshot("GENERATED_SUBTASK_QUEUE_CLEARED");

            RefreshReport(
                reason: "GENERATED_SUBTASK_QUEUE_CLEARED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Askıya alınmış primary görevleri temizler.
        /// Aktif görev etkilenmez.
        /// </summary>
        public void ClearSuspendedPrimaryTasks()
        {
            _suspendedPrimaryTasks.Clear();

            RefreshOrchestrationSnapshot("SUSPENDED_PRIMARY_TASKS_CLEARED");

            RefreshReport(
                reason: "SUSPENDED_PRIMARY_TASKS_CLEARED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Paralel görevleri temizler.
        /// Aktif primary görev etkilenmez.
        /// </summary>
        public void ClearParallelTasks()
        {
            _parallelTasks.Clear();

            RefreshOrchestrationSnapshot("PARALLEL_TASKS_CLEARED");

            RefreshReport(
                reason: "PARALLEL_TASKS_CLEARED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Guard görevlerini temizler.
        /// Aktif primary görev etkilenmez.
        /// </summary>
        public void ClearGuardTasks()
        {
            _guardTasks.Clear();

            RefreshOrchestrationSnapshot("GUARD_TASKS_CLEARED");

            RefreshReport(
                reason: "GUARD_TASKS_CLEARED",
                state: null,
                insights: null,
                distanceToWaypoint: LastReport.DistanceToWaypointM,
                distanceToFinal: LastReport.DistanceToFinalM,
                speedMps: LastReport.SpeedMps,
                effectiveArrivalThresholdM: LastReport.EffectiveArrivalThresholdM
            );
        }

        /// <summary>
        /// Her runtime döngüsünde çağrılır.
        ///
        /// Bu Update yalnızca görev orchestration ve completion durumunu yönetir.
        /// Heading, force, trajectory veya obstacle-bypass üretmez.
        /// </summary>
        public void Update(Insights insights, VehicleState? state = null)
        {
            if (CurrentTask is null)
            {
                if (!TryStartNextRunnableTask())
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

                    RefreshOrchestrationSnapshot(LastStatusReason ?? "NO_ACTIVE_TASK");
                    return;
                }
            }

            long nowTicks = NowTicks();
            var task = NormalizeTask(CurrentTask!);
            CurrentTask = task;

            if (IsTaskTimedOut(task, nowTicks))
            {
                AbortInternal("Görev zaman aşımına uğradı.", state, insights);
                RefreshOrchestrationSnapshot("TASK_TIMEOUT_ABORTED");
                return;
            }

            if (HasExplicitRouteTarget(task))
            {
                UpdateRoutedTask(task, nowTicks, insights, state);
                return;
            }

            UpdateRouteFreeTask(task, nowTicks, insights, state);
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

            RefreshOrchestrationSnapshot(LastStatusReason);
        }

        /// <summary>
        /// Tüm mission orchestration durumunu temizler.
        /// Active primary, sequential queue, generated subtasks, suspended primary,
        /// parallel tasks ve guard tasks sıfırlanır.
        /// </summary>
        public void ClearMission()
        {
            CurrentTask = null;
            _routePoints.Clear();
            _taskQueue.Clear();
            _generatedSubtasks.Clear();
            _suspendedPrimaryTasks.Clear();
            _parallelTasks.Clear();
            _guardTasks.Clear();

            ResetInternalState(clearLastIdentity: false);

            if (Phase != TaskPhase.Aborted)
                Phase = TaskPhase.None;

            LastStatusReason = "Mission cleared";

            RefreshReport(
                reason: LastStatusReason,
                state: null,
                insights: null,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: 0.0,
                effectiveArrivalThresholdM: _arriveThresholdM
            );

            RefreshOrchestrationSnapshot(LastStatusReason);
        }

        private void UpdateRoutedTask(
            TaskDefinition task,
            long nowTicks,
            Insights insights,
            VehicleState? state)
        {
            /*
             * Bu guard, Task Manager'ın sürüş kararı vermesi değildir.
             * Yalnızca görevin obstacle nedeniyle uzun süre ilerleyemediğini izler.
             * Kaçış rotasını Planner/Recovery üretmelidir.
             */
            HandleObstacleIfAny(insights, nowTicks, state);
            if (Phase == TaskPhase.Aborted)
            {
                RefreshOrchestrationSnapshot("TASK_ABORTED_BY_GUARD");
                return;
            }

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
                    effectiveArrivalThresholdM: GetTaskArrivalThreshold(task, 0.0)
                );

                RefreshOrchestrationSnapshot(LastStatusReason);
                return;
            }

            if (_routePoints.Count == 0)
            {
                /*
                 * Görev route gerektiriyor ama route yoksa Task Manager rota üretmez.
                 * Bu durumda görevi active/blocked gibi raporlar; planner/mission binder
                 * route/reference üretiminden sorumludur.
                 */
                Phase = TaskPhase.Active;
                LastStatusReason = "Route target unavailable";

                RefreshReport(
                    reason: LastStatusReason,
                    state: state,
                    insights: insights,
                    distanceToWaypoint: 0.0,
                    distanceToFinal: 0.0,
                    speedMps: ComputeSpeedMps(state),
                    effectiveArrivalThresholdM: GetTaskArrivalThreshold(task, ComputeSpeedMps(state))
                );

                RefreshOrchestrationSnapshot(LastStatusReason);
                return;
            }

            _currentIndex = ClampRouteIndex(_currentIndex);

            var s = state.Value;
            var target = _routePoints[_currentIndex];

            double speedMps = ComputeSpeedMps(state);
            double effectiveArrivalThresholdM = GetTaskArrivalThreshold(task, speedMps);

            double distToWaypoint = Distance3D(s.Position, target);
            double distToFinal = Distance3D(s.Position, _routePoints[^1]);

            TrackProgress(nowTicks, distToWaypoint, state, insights);
            if (Phase == TaskPhase.Aborted)
            {
                RefreshOrchestrationSnapshot("TASK_ABORTED_BY_NO_PROGRESS_GUARD");
                return;
            }

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

                RefreshOrchestrationSnapshot("WAYPOINT_ARRIVAL_HANDLED");
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

            RefreshOrchestrationSnapshot(LastStatusReason);
        }

        private void UpdateRouteFreeTask(
            TaskDefinition task,
            long nowTicks,
            Insights insights,
            VehicleState? state)
        {
            /*
             * Route-free görevler:
             * - CaptureImage
             * - CaptureVideo
             * - MonitorBattery
             * - MonitorCommunication
             * - CooperateWithFleet
             * - WaitForPartner
             * - ExternalConfirmation bekleyen görevler
             *
             * Bu görevlerde Task Manager route/heading üretmez.
             * Sadece görevi active tutar, timeout ve basit completion kurallarını izler.
             */

            Phase = TaskPhase.Active;

            double elapsedSeconds = _taskStartTicks is long start
                ? ElapsedSeconds(start, nowTicks)
                : 0.0;

            double requiredHoldSeconds = GetRouteFreeRequiredDurationSeconds(task);
            double progressPercent = requiredHoldSeconds > 0.0
                ? Math.Clamp((elapsedSeconds / requiredHoldSeconds) * 100.0, 0.0, 100.0)
                : 0.0;

            if (CanTaskManagerCompleteRouteFreeTask(task, elapsedSeconds, requiredHoldSeconds))
            {
                CompleteCurrentRouteFreeTask(
                    task,
                    reason: "ROUTE_FREE_TASK_COMPLETED",
                    state: state,
                    insights: insights,
                    progressPercent: 100.0);

                return;
            }

            LastStatusReason = BuildRouteFreeStatusReason(task, elapsedSeconds, requiredHoldSeconds);

            RefreshReport(
                reason: LastStatusReason,
                state: state,
                insights: insights,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: ComputeSpeedMps(state),
                effectiveArrivalThresholdM: _arriveThresholdM,
                waitElapsedSeconds: elapsedSeconds,
                waitRemainingSeconds: requiredHoldSeconds > 0.0
                    ? Math.Max(0.0, requiredHoldSeconds - elapsedSeconds)
                    : 0.0,
                progressPercentOverride: progressPercent
            );

            RefreshOrchestrationSnapshot(LastStatusReason);
        }

        private bool TryStartNextRunnableTask()
        {
            if (_generatedSubtasks.Count > 0)
            {
                var slot = _generatedSubtasks.Dequeue();
                StartMissionTaskFromSlot(slot, "GENERATED_SUBTASK_STARTED_FROM_QUEUE");
                LastStatusReason = "Generated subtask started";
                RefreshOrchestrationSnapshot("GENERATED_SUBTASK_STARTED_FROM_QUEUE");
                return true;
            }

            if (_suspendedPrimaryTasks.Count > 0)
            {
                var slot = _suspendedPrimaryTasks.Pop();
                StartMissionTaskFromSlot(slot, "SUSPENDED_PRIMARY_TASK_RESUMED");
                LastStatusReason = "Suspended primary task resumed";
                RefreshOrchestrationSnapshot("SUSPENDED_PRIMARY_TASK_RESUMED");
                return true;
            }

            if (_taskQueue.Count > 0)
            {
                var task = _taskQueue.Dequeue();

                StartMissionTaskInternal(
                    task,
                    reason: "PRIMARY_TASK_STARTED_FROM_QUEUE",
                    preserveQueue: true);

                LastStatusReason = "Primary task started from queue";
                RefreshOrchestrationSnapshot("PRIMARY_TASK_STARTED_FROM_QUEUE");
                return true;
            }

            return false;
        }

        private void StartMissionTaskFromSlot(MissionTaskSlot slot, string reason)
        {
            StartMissionTaskInternal(
                slot.Task,
                reason: reason,
                preserveQueue: true);

            if (slot.ResumeWaypointIndex is int index && _routePoints.Count > 0)
                _currentIndex = ClampRouteIndex(index);

            if (slot.ResumeCompletedWaypointCount is int completed)
                _completedWaypointCount = Math.Clamp(completed, 0, Math.Max(0, _routePoints.Count));
        }

        private void StartMissionTaskInternal(
            TaskDefinition task,
            string reason,
            bool preserveQueue)
        {
            task = NormalizeTask(task);

            if (HasExplicitRouteTarget(task))
            {
                StartTaskInternal(task, reason, preserveQueue);
                CurrentTask = NormalizeTask(CurrentTask ?? task);
                return;
            }

            StartRouteFreeTaskInternal(task, reason);
        }

        private void StartRouteFreeTaskInternal(TaskDefinition task, string reason)
        {
            task = NormalizeTask(task);

            CurrentTask = task;
            _routePoints.Clear();

            ResetInternalState(clearLastIdentity: false);

            _taskStartTicks = NowTicks();
            _startedTaskCount++;

            Phase = TaskPhase.Active;
            LastStatusReason = string.IsNullOrWhiteSpace(reason)
                ? "ROUTE_FREE_TASK_STARTED"
                : reason.Trim();

            _lastTaskName = task.Name;
            _lastTarget = task.Target;
        }

        private void SuspendCurrentTaskForGeneratedSubtask()
        {
            if (CurrentTask is null)
                return;

            var slot = CreateSlot(
                MissionTaskLane.SuspendedPrimary,
                CurrentTask,
                "PRIMARY_TASK_SUSPENDED_FOR_GENERATED_SUBTASK",
                isActive: false,
                isGenerated: false,
                mayPreemptPrimary: false)
                with
                {
                    IsSuspended = true,
                    ResumeWaypointIndex = _currentIndex,
                    ResumeCompletedWaypointCount = _completedWaypointCount
                };

            _suspendedPrimaryTasks.Push(slot);
        }

        private MissionTaskSlot CreateSlot(
            MissionTaskLane lane,
            TaskDefinition task,
            string reason,
            bool isActive,
            bool isGenerated,
            bool mayPreemptPrimary)
        {
            task = NormalizeTask(task);

            return new MissionTaskSlot(
                Id: Guid.NewGuid().ToString("N"),
                Lane: lane,
                Task: task,
                Reason: string.IsNullOrWhiteSpace(reason) ? "TASK_SLOT_CREATED" : reason.Trim(),
                CreatedTicks: NowTicks(),
                IsActive: isActive,
                IsGenerated: isGenerated,
                MayPreemptPrimary: mayPreemptPrimary)
            {
                TaskKind = task.Kind,
                Priority = task.Priority,
                CompletionAuthority = task.CompletionAuthority
            };
        }

        private void CompleteCurrentRouteFreeTask(
            TaskDefinition task,
            string reason,
            VehicleState? state,
            Insights? insights,
            double progressPercent)
        {
            _lastTaskName = task.Name;
            _lastTarget = task.Target;

            _completedTaskCount++;

            CurrentTask = null;
            _routePoints.Clear();
            ResetInternalState(clearLastIdentity: false);

            Phase = TaskPhase.Arrived;
            LastStatusReason = reason;

            RefreshReport(
                reason: reason,
                state: state,
                insights: insights,
                distanceToWaypoint: 0.0,
                distanceToFinal: 0.0,
                speedMps: ComputeSpeedMps(state),
                effectiveArrivalThresholdM: _arriveThresholdM,
                progressPercentOverride: progressPercent
            );

            RefreshOrchestrationSnapshot(reason);
        }

        private bool IsTaskTimedOut(TaskDefinition task, long nowTicks)
        {
            if (_taskStartTicks is not long start)
                return false;

            double timeoutSeconds = task.Timing.TimeoutSeconds ?? _maxTaskDurationSeconds;
            if (timeoutSeconds <= 0.0)
                return false;

            return ElapsedSeconds(start, nowTicks) > timeoutSeconds;
        }

        private double GetTaskArrivalThreshold(TaskDefinition task, double speedMps)
        {
            double dynamicThreshold = ComputeEffectiveArrivalThreshold(speedMps);
            double taskThreshold = task.Completion.AcceptanceRadiusM;

            if (!double.IsFinite(taskThreshold) || taskThreshold <= 0.0)
                taskThreshold = _arriveThresholdM;

            return Math.Clamp(
                Math.Max(dynamicThreshold, taskThreshold),
                _arriveThresholdM,
                Math.Max(_maxArrivalThresholdM, taskThreshold)
            );
        }

        private static TaskDefinition NormalizeTask(TaskDefinition task)
        {
            return task.Normalize();
        }

        private static bool HasExplicitRouteTarget(TaskDefinition task)
        {
            return task.Target is not null || task.Waypoints.Count > 0;
        }

        private static double GetRouteFreeRequiredDurationSeconds(TaskDefinition task)
        {
            double fromCompletion = task.Completion.RequiredHoldSeconds;
            double fromWait = task.WaitSecondsPerPoint;

            double duration = Math.Max(fromCompletion, fromWait);
            if (!double.IsFinite(duration))
                return 0.0;

            return Math.Max(0.0, duration);
        }

        private static bool CanTaskManagerCompleteRouteFreeTask(
            TaskDefinition task,
            double elapsedSeconds,
            double requiredDurationSeconds)
        {
            if (task.IsExternallyCompleted || task.Completion.RequiresExternalAck)
                return false;

            if (task.Completion.Mode != TaskCompletionMode.HoldDurationSatisfied)
                return false;

            return requiredDurationSeconds <= 0.0 || elapsedSeconds >= requiredDurationSeconds;
        }

        private static string BuildRouteFreeStatusReason(
            TaskDefinition task,
            double elapsedSeconds,
            double requiredDurationSeconds)
        {
            string kind = task.Kind.ToString();

            if (requiredDurationSeconds > 0.0)
            {
                double remaining = Math.Max(0.0, requiredDurationSeconds - elapsedSeconds);
                return $"Route-free task active: {kind}, waitRemaining={remaining:0.00}s";
            }

            if (task.IsExternallyCompleted || task.Completion.RequiresExternalAck)
                return $"Route-free task active: {kind}, waiting external completion";

            return $"Route-free task active: {kind}";
        }

        private void RefreshOrchestrationSnapshot(string reason)
        {
            _orchestrationRevision++;

            LastOrchestration = new AdvancedTaskOrchestrationSnapshot(
                Revision: _orchestrationRevision,
                Reason: string.IsNullOrWhiteSpace(reason) ? "ORCHESTRATION_UPDATED" : reason.Trim(),
                CurrentTaskName: CurrentTask?.Name,
                Phase: Phase,
                CurrentWaypointIndex: _currentIndex,
                RoutePointCount: _routePoints.Count,
                PendingPrimaryTaskCount: _taskQueue.Count,
                PendingGeneratedSubtaskCount: _generatedSubtasks.Count,
                ActiveParallelTaskCount: _parallelTasks.Count,
                ActiveGuardTaskCount: _guardTasks.Count,
                StartedTaskCount: _startedTaskCount,
                CompletedTaskCount: _completedTaskCount,
                CompletedWaypointCount: _completedWaypointCount)
            {
                CurrentTaskKind = CurrentTask?.Kind ?? TaskKind.Unknown,
                CurrentTaskPriority = CurrentTask?.Priority ?? TaskPriority.Normal,
                CurrentCompletionAuthority = CurrentTask?.CompletionAuthority ?? TaskCompletionAuthority.TaskManager,
                SuspendedPrimaryTaskCount = _suspendedPrimaryTasks.Count,
                HasRouteFreeCurrentTask = CurrentTask is not null && _routePoints.Count == 0,
                IsCurrentTaskExternal = CurrentTask?.IsExternallyCompleted ?? false,
                CurrentExternalOwnerId = CurrentTask?.ExternalOwnerId,
                CurrentExternalObjectiveId = CurrentTask?.ExternalObjectiveId
            };
        }
    }

    public enum MissionTaskLane
    {
        Primary = 0,
        SequentialQueue = 1,
        GeneratedSubtask = 2,
        Parallel = 3,
        Guard = 4,
        SuspendedPrimary = 5
    }

    public sealed record MissionTaskSlot(
        string Id,
        MissionTaskLane Lane,
        TaskDefinition Task,
        string Reason,
        long CreatedTicks,
        bool IsActive,
        bool IsGenerated,
        bool MayPreemptPrimary
    )
    {
        public TaskKind TaskKind { get; init; } = TaskKind.Unknown;
        public TaskPriority Priority { get; init; } = TaskPriority.Normal;
        public TaskCompletionAuthority CompletionAuthority { get; init; } = TaskCompletionAuthority.TaskManager;

        public bool IsSuspended { get; init; } = false;
        public int? ResumeWaypointIndex { get; init; }
        public int? ResumeCompletedWaypointCount { get; init; }
    }

    public sealed record AdvancedTaskOrchestrationSnapshot(
        long Revision,
        string Reason,
        string? CurrentTaskName,
        TaskPhase Phase,
        int CurrentWaypointIndex,
        int RoutePointCount,
        int PendingPrimaryTaskCount,
        int PendingGeneratedSubtaskCount,
        int ActiveParallelTaskCount,
        int ActiveGuardTaskCount,
        int StartedTaskCount,
        int CompletedTaskCount,
        int CompletedWaypointCount
    )
    {
        public TaskKind CurrentTaskKind { get; init; } = TaskKind.Unknown;
        public TaskPriority CurrentTaskPriority { get; init; } = TaskPriority.Normal;
        public TaskCompletionAuthority CurrentCompletionAuthority { get; init; } = TaskCompletionAuthority.TaskManager;

        public int SuspendedPrimaryTaskCount { get; init; } = 0;

        public bool HasRouteFreeCurrentTask { get; init; } = false;
        public bool IsCurrentTaskExternal { get; init; } = false;

        public string? CurrentExternalOwnerId { get; init; }
        public string? CurrentExternalObjectiveId { get; init; }

        public static AdvancedTaskOrchestrationSnapshot Empty { get; } = new(
            Revision: 0,
            Reason: "EMPTY",
            CurrentTaskName: null,
            Phase: TaskPhase.None,
            CurrentWaypointIndex: 0,
            RoutePointCount: 0,
            PendingPrimaryTaskCount: 0,
            PendingGeneratedSubtaskCount: 0,
            ActiveParallelTaskCount: 0,
            ActiveGuardTaskCount: 0,
            StartedTaskCount: 0,
            CompletedTaskCount: 0,
            CompletedWaypointCount: 0
        );
    }
}