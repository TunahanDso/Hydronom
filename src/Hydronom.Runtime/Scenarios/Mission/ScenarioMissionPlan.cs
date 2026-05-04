using Hydronom.Core.Scenarios.Models;

namespace Hydronom.Runtime.Scenarios.Mission;

/// <summary>
/// ScenarioDefinition içindeki objective listesinden üretilen runtime mission plan modelidir.
/// Bu plan, scenario JSON'u doğrudan runtime görev sistemine bağlamadan önce
/// sıralı, doğrulanmış ve izlenebilir hedef listesi sağlar.
/// </summary>
public sealed record ScenarioMissionPlan
{
    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo adı.
    /// </summary>
    public string ScenarioName { get; init; } = string.Empty;

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; init; } = "hydronom-main";

    /// <summary>
    /// Araç platformu.
    /// Örnek: surface_vessel, underwater_vehicle, aerial_vehicle.
    /// </summary>
    public string VehiclePlatform { get; init; } = "unknown";

    /// <summary>
    /// Senaryo ailesi.
    /// Örnek: teknofest_2026.
    /// </summary>
    public string ScenarioFamily { get; init; } = string.Empty;

    /// <summary>
    /// Planın üretildiği zaman.
    /// </summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Plan hedefleri.
    /// </summary>
    public IReadOnlyList<ScenarioMissionTarget> Targets { get; init; }
        = Array.Empty<ScenarioMissionTarget>();

    /// <summary>
    /// Senaryo süre limiti.
    /// </summary>
    public double? TimeLimitSeconds { get; init; }

    /// <summary>
    /// Minimum başarı skoru.
    /// </summary>
    public double MinimumSuccessScore { get; init; }

    /// <summary>
    /// Plan oluşturulurken kullanılan ham scenario referansı.
    /// Runtime içinde mutation yapılmamalıdır.
    /// </summary>
    public ScenarioDefinition? SourceScenario { get; init; }

    /// <summary>
    /// Ek plan metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plan hedef içeriyor mu?
    /// </summary>
    public bool HasTargets => Targets.Count > 0;

    /// <summary>
    /// İlk hedef.
    /// </summary>
    public ScenarioMissionTarget? FirstTarget =>
        Targets.OrderBy(x => x.Order).FirstOrDefault();

    /// <summary>
    /// Son hedef.
    /// </summary>
    public ScenarioMissionTarget? LastTarget =>
        Targets.OrderBy(x => x.Order).LastOrDefault();

    /// <summary>
    /// Id ile hedef bulur.
    /// </summary>
    public ScenarioMissionTarget? FindTargetByObjectiveId(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            return null;
        }

        return Targets.FirstOrDefault(x =>
            string.Equals(x.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verilen tamamlanmış objective setine göre sıradaki hedefi bulur.
    /// </summary>
    public ScenarioMissionTarget? FindNextTarget(IReadOnlySet<string> completedObjectiveIds)
    {
        ArgumentNullException.ThrowIfNull(completedObjectiveIds);

        return Targets
            .OrderBy(x => x.Order)
            .ThenBy(x => x.ObjectiveId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => !completedObjectiveIds.Contains(x.ObjectiveId));
    }

    /// <summary>
    /// Planın kısa özeti.
    /// </summary>
    public string Summary =>
        $"ScenarioMissionPlan scenario={ScenarioId}, vehicle={VehicleId}, targets={Targets.Count}, minScore={MinimumSuccessScore:F1}";

    public override string ToString()
    {
        return Summary;
    }
}