using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedTaskManager son görev raporu.
    ///
    /// Mission journal, diagnostics, Hydronom Ops ve test sistemleri için
    /// açıklanabilir görev durumu sağlar.
    ///
    /// Bu model sadece rapordur.
    /// Heading, force, trajectory, planner kararı veya controller hedefi üretmez.
    /// </summary>
    public readonly record struct AdvancedTaskReport(
        TaskPhase Phase,
        string Reason,
        string? TaskName,
        bool HasTask,
        int CurrentWaypointIndex,
        int WaypointCount,
        int CompletedWaypointCount,
        Vec3? CurrentTarget,
        Vec3? FinalTarget,
        double DistanceToWaypointM,
        double DistanceToFinalM,
        double ProgressPercent,
        double ElapsedTaskSeconds,
        double NoProgressSeconds,
        double ObstacleHoldSeconds,
        double WaitElapsedSeconds,
        double WaitRemainingSeconds,
        bool ObstacleAhead,
        Vec3? Position,
        double SpeedMps,
        double EffectiveArrivalThresholdM,
        int QueuedTaskCount,
        int CompletedTaskCount,
        int StartedTaskCount
    )
    {
        /// <summary>
        /// Orchestration snapshot revision.
        /// Her Task Manager orchestration değişiminde artar.
        /// </summary>
        public long OrchestrationRevision { get; init; }

        /// <summary>
        /// Aktif görevin yapısal tipi.
        /// Görev adı üzerinden niyet çıkarımı yapılmaz.
        /// </summary>
        public TaskKind TaskKind { get; init; } = TaskKind.Unknown;

        public TaskPriority TaskPriority { get; init; } = TaskPriority.Normal;

        public TaskCompletionAuthority CompletionAuthority { get; init; } =
            TaskCompletionAuthority.TaskManager;

        public TaskCompletionMode CompletionMode { get; init; } =
            TaskCompletionMode.None;

        /// <summary>
        /// Aktif görev route taşımıyor ama yine de geçerli bir görev mi?
        /// Örnek: CaptureImage, MonitorBattery, WaitForPartner, CooperateWithFleet.
        /// </summary>
        public bool HasRouteFreeCurrentTask { get; init; }

        /// <summary>
        /// Aktif görevin tamamlanma yetkisi dış sistemde mi?
        /// Örnek: scenario, fleet coordinator, operator.
        /// </summary>
        public bool IsCurrentTaskExternal { get; init; }

        public string? ExternalOwnerId { get; init; }

        public string? ExternalObjectiveId { get; init; }

        /// <summary>
        /// World Model / Perception tarafındaki hedef nesne kimliği.
        /// Örnek: buoy_3, gate_1, pipe_track_2.
        /// </summary>
        public string? TargetObjectId { get; init; }

        public string? TargetObjectKind { get; init; }

        public string? TargetObjectRole { get; init; }

        /// <summary>
        /// Pending primary queue sayısı.
        /// Eski QueuedTaskCount ile aynı anlamda tutulur ama daha açık isimdir.
        /// </summary>
        public int PendingPrimaryTaskCount { get; init; }

        public int PendingGeneratedSubtaskCount { get; init; }

        public int SuspendedPrimaryTaskCount { get; init; }

        public int ActiveParallelTaskCount { get; init; }

        public int ActiveGuardTaskCount { get; init; }

        public bool HasPendingWork =>
            PendingPrimaryTaskCount > 0 ||
            PendingGeneratedSubtaskCount > 0 ||
            SuspendedPrimaryTaskCount > 0;

        public bool HasParallelWork =>
            ActiveParallelTaskCount > 0;

        public bool HasGuardWork =>
            ActiveGuardTaskCount > 0;

        public bool HasAnyOrchestrationWork =>
            HasTask ||
            HasPendingWork ||
            HasParallelWork ||
            HasGuardWork;

        public bool HasWaypointRoute =>
            WaypointCount > 0;

        public bool HasValidWaypointIndex =>
            CurrentWaypointIndex >= 0 &&
            CurrentWaypointIndex < WaypointCount;

        public bool IsWaiting =>
            WaitRemainingSeconds > 0.0;

        public bool IsProgressComplete =>
            ProgressPercent >= 99.999;

        public bool IsAborted =>
            Phase == TaskPhase.Aborted;

        public bool IsArrived =>
            Phase == TaskPhase.Arrived;

        public bool IsActive =>
            Phase == TaskPhase.Active;

        public static AdvancedTaskReport Empty { get; } =
            new(
                Phase: TaskPhase.None,
                Reason: "NOT_COMPUTED",
                TaskName: null,
                HasTask: false,
                CurrentWaypointIndex: -1,
                WaypointCount: 0,
                CompletedWaypointCount: 0,
                CurrentTarget: null,
                FinalTarget: null,
                DistanceToWaypointM: 0.0,
                DistanceToFinalM: 0.0,
                ProgressPercent: 0.0,
                ElapsedTaskSeconds: 0.0,
                NoProgressSeconds: 0.0,
                ObstacleHoldSeconds: 0.0,
                WaitElapsedSeconds: 0.0,
                WaitRemainingSeconds: 0.0,
                ObstacleAhead: false,
                Position: null,
                SpeedMps: 0.0,
                EffectiveArrivalThresholdM: 0.0,
                QueuedTaskCount: 0,
                CompletedTaskCount: 0,
                StartedTaskCount: 0
            )
            {
                OrchestrationRevision = 0,
                TaskKind = TaskKind.Unknown,
                TaskPriority = TaskPriority.Normal,
                CompletionAuthority = TaskCompletionAuthority.TaskManager,
                CompletionMode = TaskCompletionMode.None,
                HasRouteFreeCurrentTask = false,
                IsCurrentTaskExternal = false,
                ExternalOwnerId = null,
                ExternalObjectiveId = null,
                TargetObjectId = null,
                TargetObjectKind = null,
                TargetObjectRole = null,
                PendingPrimaryTaskCount = 0,
                PendingGeneratedSubtaskCount = 0,
                SuspendedPrimaryTaskCount = 0,
                ActiveParallelTaskCount = 0,
                ActiveGuardTaskCount = 0
            };

        public AdvancedTaskReport WithTaskMetadata(TaskDefinition? task)
        {
            if (task is null)
            {
                return this with
                {
                    TaskKind = TaskKind.Unknown,
                    TaskPriority = TaskPriority.Normal,
                    CompletionAuthority = TaskCompletionAuthority.TaskManager,
                    CompletionMode = TaskCompletionMode.None,
                    HasRouteFreeCurrentTask = false,
                    IsCurrentTaskExternal = false,
                    ExternalOwnerId = null,
                    ExternalObjectiveId = null,
                    TargetObjectId = null,
                    TargetObjectKind = null,
                    TargetObjectRole = null
                };
            }

            var normalized = task.Normalize();

            return this with
            {
                TaskKind = normalized.Kind,
                TaskPriority = normalized.Priority,
                CompletionAuthority = normalized.CompletionAuthority,
                CompletionMode = normalized.Completion.Mode,
                HasRouteFreeCurrentTask =
                    normalized.Target is null &&
                    normalized.Waypoints.Count == 0,
                IsCurrentTaskExternal = normalized.IsExternallyCompleted,
                ExternalOwnerId = normalized.ExternalOwnerId,
                ExternalObjectiveId = normalized.ExternalObjectiveId,
                TargetObjectId = normalized.TargetObjectId,
                TargetObjectKind = normalized.TargetObjectKind,
                TargetObjectRole = normalized.TargetObjectRole
            };
        }

        public AdvancedTaskReport WithOrchestration(
            long revision,
            int pendingPrimaryTaskCount,
            int pendingGeneratedSubtaskCount,
            int suspendedPrimaryTaskCount,
            int activeParallelTaskCount,
            int activeGuardTaskCount)
        {
            return this with
            {
                OrchestrationRevision = revision,
                PendingPrimaryTaskCount = pendingPrimaryTaskCount,
                PendingGeneratedSubtaskCount = pendingGeneratedSubtaskCount,
                SuspendedPrimaryTaskCount = suspendedPrimaryTaskCount,
                ActiveParallelTaskCount = activeParallelTaskCount,
                ActiveGuardTaskCount = activeGuardTaskCount
            };
        }

        public override string ToString()
        {
            string wp =
                WaypointCount <= 0 || CurrentWaypointIndex < 0
                    ? "n/a"
                    : $"{CurrentWaypointIndex + 1}/{WaypointCount}";

            string target =
                string.IsNullOrWhiteSpace(TargetObjectId)
                    ? "none"
                    : TargetObjectId!;

            return
                $"Task phase={Phase} reason={Reason} name={TaskName ?? "none"} " +
                $"kind={TaskKind} prio={TaskPriority} auth={CompletionAuthority} mode={CompletionMode} " +
                $"wp={wp} routeFree={HasRouteFreeCurrentTask} external={IsCurrentTaskExternal} " +
                $"targetObj={target} progress={ProgressPercent:F1}% " +
                $"distWp={DistanceToWaypointM:F2}m distFinal={DistanceToFinalM:F2}m " +
                $"speed={SpeedMps:F2}m/s accept={EffectiveArrivalThresholdM:F2}m " +
                $"elapsed={ElapsedTaskSeconds:F1}s noProg={NoProgressSeconds:F1}s " +
                $"wait={WaitElapsedSeconds:F1}/{WaitElapsedSeconds + WaitRemainingSeconds:F1}s " +
                $"obs={ObstacleAhead} obsHold={ObstacleHoldSeconds:F1}s " +
                $"primaryQ={PendingPrimaryTaskCount} subQ={PendingGeneratedSubtaskCount} " +
                $"suspended={SuspendedPrimaryTaskCount} parallel={ActiveParallelTaskCount} guard={ActiveGuardTaskCount} " +
                $"completedTasks={CompletedTaskCount} startedTasks={StartedTaskCount} orchRev={OrchestrationRevision}";
        }
    }
}