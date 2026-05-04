namespace Hydronom.Core.Scenarios.Runtime;

/// <summary>
/// Digital Proving Ground içinde tek bir senaryo koşusunun canlı durumudur.
/// Bu model runtime/judge/report katmanları arasında "koşu şu an nerede?" bilgisini taşır.
/// </summary>
public sealed record ScenarioRunState
{
    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Tekil koşu kimliği.
    /// Aynı senaryo birden fazla kez farklı başlangıç/noise/fault ayarlarıyla koşturulabilir.
    /// </summary>
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; init; } = "hydronom-main";

    /// <summary>
    /// Araç platform tipi.
    /// Örnek: surface_vessel, underwater_vehicle, rover, vtol.
    /// </summary>
    public string VehiclePlatform { get; init; } = "surface_vessel";

    /// <summary>
    /// Koşu durumu.
    /// Örnek: NotStarted, Running, Paused, Completed, Failed, Aborted.
    /// </summary>
    public string Status { get; init; } = ScenarioRunStatus.NotStarted;

    /// <summary>
    /// Koşu başlatıldı mı?
    /// </summary>
    public bool IsStarted { get; init; }

    /// <summary>
    /// Koşu aktif olarak devam ediyor mu?
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Koşu tamamlandı mı?
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Koşu başarısız mı?
    /// </summary>
    public bool IsFailed { get; init; }

    /// <summary>
    /// Koşu iptal edildi mi?
    /// </summary>
    public bool IsAborted { get; init; }

    /// <summary>
    /// Koşunun başladığı UTC zaman.
    /// </summary>
    public DateTime? StartedUtc { get; init; }

    /// <summary>
    /// Koşunun son güncellendiği UTC zaman.
    /// </summary>
    public DateTime? LastUpdatedUtc { get; init; }

    /// <summary>
    /// Koşunun bittiği UTC zaman.
    /// </summary>
    public DateTime? FinishedUtc { get; init; }

    /// <summary>
    /// Koşunun geçen süresi.
    /// </summary>
    public double ElapsedSeconds { get; init; }

    /// <summary>
    /// Senaryo için süre sınırı.
    /// Sıfır veya negatifse süre sınırı yok kabul edilir.
    /// </summary>
    public double TimeLimitSeconds { get; init; }

    /// <summary>
    /// Süre sınırı aşıldı mı?
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// Aktif görev hedefi kimliği.
    /// </summary>
    public string? CurrentObjectiveId { get; init; }

    /// <summary>
    /// Aktif görev hedefi başlığı.
    /// </summary>
    public string? CurrentObjectiveTitle { get; init; }

    /// <summary>
    /// Aktif görev hedefi sırası.
    /// </summary>
    public int CurrentObjectiveOrder { get; init; }

    /// <summary>
    /// Sonraki görev hedefi kimliği.
    /// </summary>
    public string? NextObjectiveId { get; init; }

    /// <summary>
    /// Toplam görev hedefi sayısı.
    /// </summary>
    public int TotalObjectiveCount { get; init; }

    /// <summary>
    /// Tamamlanan görev hedefi sayısı.
    /// </summary>
    public int CompletedObjectiveCount { get; init; }

    /// <summary>
    /// Başarısız olan görev hedefi sayısı.
    /// </summary>
    public int FailedObjectiveCount { get; init; }

    /// <summary>
    /// Görev tamamlanma oranı.
    /// 0.0 - 1.0 arası yorumlanır.
    /// </summary>
    public double CompletionRatio { get; init; }

    /// <summary>
    /// Araç X konumu.
    /// </summary>
    public double VehicleX { get; init; }

    /// <summary>
    /// Araç Y konumu.
    /// </summary>
    public double VehicleY { get; init; }

    /// <summary>
    /// Araç Z konumu.
    /// Denizaltı için derinlik, VTOL için irtifa eksenini temsil edebilir.
    /// </summary>
    public double VehicleZ { get; init; }

    /// <summary>
    /// Araç roll açısı.
    /// </summary>
    public double VehicleRollDeg { get; init; }

    /// <summary>
    /// Araç pitch açısı.
    /// </summary>
    public double VehiclePitchDeg { get; init; }

    /// <summary>
    /// Araç yaw/heading açısı.
    /// </summary>
    public double VehicleYawDeg { get; init; }

    /// <summary>
    /// Araç X hızı.
    /// </summary>
    public double VehicleVx { get; init; }

    /// <summary>
    /// Araç Y hızı.
    /// </summary>
    public double VehicleVy { get; init; }

    /// <summary>
    /// Araç Z hızı.
    /// Denizaltı/VTOL için dikey hareketin analizinde önemlidir.
    /// </summary>
    public double VehicleVz { get; init; }

    /// <summary>
    /// Mevcut hedefe kalan 3D mesafe.
    /// </summary>
    public double? DistanceToCurrentObjectiveMeters { get; init; }

    /// <summary>
    /// Mevcut hedefe kalan yatay XY mesafesi.
    /// </summary>
    public double? HorizontalDistanceToCurrentObjectiveMeters { get; init; }

    /// <summary>
    /// Mevcut hedefe kalan dikey Z mesafesi.
    /// </summary>
    public double? VerticalDistanceToCurrentObjectiveMeters { get; init; }

    /// <summary>
    /// Toplam çarpışma sayısı.
    /// </summary>
    public int CollisionCount { get; init; }

    /// <summary>
    /// Toplam no-go zone ihlal sayısı.
    /// </summary>
    public int NoGoZoneViolationCount { get; init; }

    /// <summary>
    /// Toplam degraded/fault/sensör kaybı olayı.
    /// </summary>
    public int DegradedEventCount { get; init; }

    /// <summary>
    /// Safety katmanı müdahale sayısı.
    /// </summary>
    public int SafetyInterventionCount { get; init; }

    /// <summary>
    /// Koşu skoru.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Koşu cezası.
    /// </summary>
    public double Penalty { get; init; }

    /// <summary>
    /// Net skor.
    /// </summary>
    public double NetScore => Score - Penalty;

    /// <summary>
    /// Kısa insan-okunabilir durum özeti.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Koşu başarısızsa veya iptal edildiyse nedeni.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Ek numeric metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ek string alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Senaryo koşu durum sabitleri.
/// </summary>
public static class ScenarioRunStatus
{
    public const string NotStarted = "NotStarted";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Aborted = "Aborted";
    public const string Timeout = "Timeout";
}