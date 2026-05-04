using Hydronom.Core.Domain;
using Hydronom.Core.Scenarios.Reports;
using Hydronom.Runtime.Scenarios.Mission;

namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Runtime scenario host'un tek tick sonucudur.
/// Her tick'te aktif objective, araç state'i, judge raporu ve task geçiş bilgisi burada taşınır.
/// </summary>
public sealed record RuntimeScenarioTickResult
{
    /// <summary>
    /// Scenario id.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Runtime scenario run id.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Tick timestamp.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Tick sırası.
    /// </summary>
    public long TickIndex { get; init; }

    /// <summary>
    /// Session state.
    /// </summary>
    public RuntimeScenarioSessionState SessionState { get; init; } =
        RuntimeScenarioSessionState.Created;

    /// <summary>
    /// Tick öncesindeki active objective.
    /// </summary>
    public string? PreviousObjectiveId { get; init; }

    /// <summary>
    /// Tick sonrasındaki active objective.
    /// </summary>
    public string? CurrentObjectiveId { get; init; }

    /// <summary>
    /// Bu tick'te tamamlanan objective.
    /// </summary>
    public string? CompletedObjectiveId { get; init; }

    /// <summary>
    /// Bu tick'te task manager'a yeni task basıldı mı?
    /// </summary>
    public bool AppliedNewTask { get; init; }

    /// <summary>
    /// Uygulanan target.
    /// </summary>
    public ScenarioMissionTarget? AppliedTarget { get; init; }

    /// <summary>
    /// Mevcut runtime state.
    /// </summary>
    public VehicleState VehicleState { get; init; } = VehicleState.Zero;

    /// <summary>
    /// Aktif hedefe yatay mesafe.
    /// </summary>
    public double DistanceToCurrentTargetMeters { get; init; }

    /// <summary>
    /// Aktif hedefe 3D mesafe.
    /// </summary>
    public double Distance3DToCurrentTargetMeters { get; init; }

    /// <summary>
    /// Hedef toleransı.
    /// </summary>
    public double ToleranceMeters { get; init; }

    /// <summary>
    /// Tolerans içine girildi mi?
    /// </summary>
    public bool InsideTolerance { get; init; }

    /// <summary>
    /// Hız arrival için uygun mu?
    /// </summary>
    public bool SpeedSettled { get; init; }

    /// <summary>
    /// Yaw rate arrival için uygun mu?
    /// </summary>
    public bool YawRateSettled { get; init; }

    /// <summary>
    /// Settle süresi doldu mu?
    /// </summary>
    public bool SettleSatisfied { get; init; }

    /// <summary>
    /// Bu tick'te objective tamamlandı mı?
    /// </summary>
    public bool ObjectiveCompleted { get; init; }

    /// <summary>
    /// Tüm objective'ler bitti mi?
    /// </summary>
    public bool AllObjectivesCompleted { get; init; }

    /// <summary>
    /// Timeout oldu mu?
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// Son judge raporu.
    /// </summary>
    public ScenarioRunReport? Report { get; init; }

    /// <summary>
    /// Kısa açıklama.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    public static RuntimeScenarioTickResult Empty(string scenarioId, string runId)
    {
        return new RuntimeScenarioTickResult
        {
            ScenarioId = scenarioId,
            RunId = runId,
            TimestampUtc = DateTime.UtcNow,
            Summary = "Runtime scenario tick empty."
        };
    }
}