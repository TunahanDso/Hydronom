using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AdvancedTaskManager son görev raporu.
    /// Mission journal, diagnostics, Hydronom Ops ve test sistemleri için açıklanabilir görev durumu sağlar.
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
            );

        public override string ToString()
        {
            string wp =
                WaypointCount <= 0 || CurrentWaypointIndex < 0
                    ? "n/a"
                    : $"{CurrentWaypointIndex + 1}/{WaypointCount}";

            return
                $"Task phase={Phase} reason={Reason} name={TaskName ?? "none"} " +
                $"wp={wp} progress={ProgressPercent:F1}% " +
                $"distWp={DistanceToWaypointM:F2}m distFinal={DistanceToFinalM:F2}m " +
                $"speed={SpeedMps:F2}m/s accept={EffectiveArrivalThresholdM:F2}m " +
                $"elapsed={ElapsedTaskSeconds:F1}s noProg={NoProgressSeconds:F1}s " +
                $"obs={ObstacleAhead} obsHold={ObstacleHoldSeconds:F1}s " +
                $"queue={QueuedTaskCount} completedTasks={CompletedTaskCount}";
        }
    }
}