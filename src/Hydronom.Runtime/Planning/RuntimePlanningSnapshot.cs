using System;
using Hydronom.Core.Planning.Models;

namespace Hydronom.Runtime.Planning;

/// <summary>
/// Runtime planning hattının son çıktısını taşıyan immutable snapshot.
/// 
/// Bu snapshot:
/// - PlanningContext
/// - GlobalPath
/// - LocalPath
/// - TrajectoryPlan
/// - zaman/versiyon bilgisi
/// - hata/özet bilgisi
/// 
/// içerir. Decision katmanı daha sonra buradan trajectory lookahead okuyacak.
/// </summary>
public sealed record RuntimePlanningSnapshot
{
    public PlanningContext Context { get; init; } = PlanningContext.Idle;

    public PlannedPath GlobalPath { get; init; } = PlannedPath.Empty;

    public PlannedPath LocalPath { get; init; } = PlannedPath.Empty;

    public TrajectoryPlan Trajectory { get; init; } = TrajectoryPlan.Empty;

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public long Version { get; init; }

    public bool HasPlan { get; init; }

    public bool IsValid { get; init; }

    public bool RequiresReplan { get; init; }

    public bool RequiresSlowMode { get; init; }

    public string Summary { get; init; } = "NO_PLAN";

    public string? Error { get; init; }

    public static RuntimePlanningSnapshot Empty { get; } = new()
    {
        Context = PlanningContext.Idle,
        GlobalPath = PlannedPath.Empty,
        LocalPath = PlannedPath.Empty,
        Trajectory = TrajectoryPlan.Empty,
        TimestampUtc = DateTime.MinValue,
        Version = 0,
        HasPlan = false,
        IsValid = false,
        RequiresReplan = false,
        RequiresSlowMode = false,
        Summary = "EMPTY",
        Error = null
    };

    public double AgeMs =>
        HasPlan && TimestampUtc != default && TimestampUtc != DateTime.MinValue
            ? (DateTime.UtcNow - TimestampUtc).TotalMilliseconds
            : double.PositiveInfinity;

    public RuntimePlanningSnapshot Sanitized()
    {
        var context = (Context ?? PlanningContext.Idle).Sanitized();
        var global = (GlobalPath ?? PlannedPath.Empty).Sanitized();
        var local = (LocalPath ?? PlannedPath.Empty).Sanitized();
        var trajectory = (Trajectory ?? TrajectoryPlan.Empty).Sanitized();

        var isValid = IsValid && trajectory.IsValid;

        return this with
        {
            Context = context,
            GlobalPath = global,
            LocalPath = local,
            Trajectory = trajectory,
            TimestampUtc = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            HasPlan = HasPlan && trajectory.IsValid,
            IsValid = isValid,
            RequiresReplan = RequiresReplan || trajectory.RequiresReplan,
            RequiresSlowMode = RequiresSlowMode || trajectory.RequiresSlowMode,
            Summary = string.IsNullOrWhiteSpace(Summary) ? "PLAN" : Summary.Trim(),
            Error = string.IsNullOrWhiteSpace(Error) ? null : Error.Trim()
        };
    }
}