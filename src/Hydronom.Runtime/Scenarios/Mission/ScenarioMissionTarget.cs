using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Scenarios.Mission;

/// <summary>
/// Scenario objective'in runtime görev sistemine verilebilir hedef karşılığıdır.
/// Bu model, JSON senaryo hedefini gerçek runtime TaskDefinition hedefi haline getirmeden önce
/// açıklanabilir ve izlenebilir ara model olarak kullanılır.
/// </summary>
public sealed record ScenarioMissionTarget
{
    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Objective kimliği.
    /// Örnek: reach_wp_1
    /// </summary>
    public string ObjectiveId { get; init; } = string.Empty;

    /// <summary>
    /// Objective sırası.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Objective başlığı.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Hedef world object kimliği.
    /// Örnek: wp_1
    /// </summary>
    public string TargetObjectId { get; init; } = string.Empty;

    /// <summary>
    /// Runtime task hedefi.
    /// Surface araç için Z genelde 0 olur.
    /// Denizaltı/VTOL gibi platformlarda Z gerçek görev ekseni olarak kullanılır.
    /// </summary>
    public Vec3 Target { get; init; } = Vec3.Zero;

    /// <summary>
    /// Hedef toleransı.
    /// Judge, task manager ve raporlama için ortak yorumlanır.
    /// </summary>
    public double ToleranceMeters { get; init; } = 1.0;

    /// <summary>
    /// Hedef object türü.
    /// Örnek: waypoint, gate, buoy, dock.
    /// </summary>
    public string Kind { get; init; } = "unknown";

    /// <summary>
    /// Hedef layer bilgisi.
    /// </summary>
    public string Layer { get; init; } = "mission";

    /// <summary>
    /// Bu hedef zorunlu mu?
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    /// Hedef tamamlandığında hold davranışı isteniyor mu?
    /// Şimdilik false; final objective veya inspection gibi görevlerde ileride true yapılabilir.
    /// </summary>
    public bool HoldOnArrive { get; init; }

    /// <summary>
    /// Ek metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runtime task adı.
    /// AdvancedDecision/SimpleTaskManager tarafı GoTo isimlerini tanır.
    /// </summary>
    public string TaskName =>
        HoldOnArrive
            ? $"GoToHold:{ObjectiveId}"
            : $"GoTo:{ObjectiveId}";

    public override string ToString()
    {
        return
            $"{ObjectiveId} -> {TargetObjectId} " +
            $"target=({Target.X:F2},{Target.Y:F2},{Target.Z:F2}) " +
            $"tol={ToleranceMeters:F2} required={IsRequired}";
    }
}