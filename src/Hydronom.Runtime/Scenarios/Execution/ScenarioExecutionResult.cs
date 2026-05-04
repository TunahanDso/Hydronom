using Hydronom.Core.Scenarios.Reports;
using Hydronom.Runtime.Testing.Scenarios;

namespace Hydronom.Runtime.Scenarios.Execution;

/// <summary>
/// ScenarioKinematicExecutor tarafından üretilen final icra sonucudur.
/// Senaryo koşusunun raporunu, timeline örneklerini ve temel icra metriklerini taşır.
/// </summary>
public sealed record ScenarioExecutionResult
{
    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Koşu kimliği.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// İcra başlangıç zamanı.
    /// </summary>
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// İcra bitiş zamanı.
    /// </summary>
    public DateTime FinishedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Simülasyon süresi.
    /// </summary>
    public double SimulatedElapsedSeconds { get; init; }

    /// <summary>
    /// Toplam tick sayısı.
    /// </summary>
    public int TickCount { get; init; }

    /// <summary>
    /// Executor final durumu.
    /// Örnek: Completed, Failed, Timeout, Aborted.
    /// </summary>
    public string FinalStatus { get; init; } = "Unknown";

    /// <summary>
    /// Başarılı mı?
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Başarısız mı?
    /// </summary>
    public bool IsFailure { get; init; }

    /// <summary>
    /// Zaman aşımı oldu mu?
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// İptal edildi mi?
    /// </summary>
    public bool IsAborted { get; init; }

    /// <summary>
    /// Final rapor.
    /// </summary>
    public ScenarioRunReport Report { get; init; } = new();

    /// <summary>
    /// Executor tarafından üretilen araç state timeline örnekleri.
    /// Debug, replay ve Ops Mission Theater için kullanılabilir.
    /// </summary>
    public IReadOnlyList<RuntimeScenarioVehicleState> Timeline { get; init; }
        = Array.Empty<RuntimeScenarioVehicleState>();

    /// <summary>
    /// Kısa insan-okunabilir özet.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Ek numeric metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ek string alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}