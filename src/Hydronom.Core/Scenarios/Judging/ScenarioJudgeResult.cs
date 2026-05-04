namespace Hydronom.Core.Scenarios.Judging;

/// <summary>
/// ScenarioJudge tarafından üretilen anlık veya final değerlendirme sonucudur.
/// Digital Proving Ground içinde "bu koşuda Hydronom ne yaptı?" sorusuna cevap verir.
/// </summary>
public sealed record ScenarioJudgeResult
{
    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Tekil koşu kimliği.
    /// Aynı senaryo farklı başlangıç/noise/fault kombinasyonlarıyla tekrar tekrar koşturulabilir.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Değerlendirmenin üretildiği UTC zaman.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Judge sonucu.
    /// Örnek: Running, Success, Failed, Timeout, Aborted.
    /// </summary>
    public string Status { get; init; } = ScenarioJudgeStatus.Running;

    /// <summary>
    /// Senaryo başarılı kabul ediliyor mu?
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Senaryo başarısız kabul ediliyor mu?
    /// </summary>
    public bool IsFailure { get; init; }

    /// <summary>
    /// Senaryo hâlâ çalışıyor mu?
    /// </summary>
    public bool IsRunning { get; init; } = true;

    /// <summary>
    /// Toplam skor.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Uygulanan toplam ceza.
    /// </summary>
    public double Penalty { get; init; }

    /// <summary>
    /// Skor ve cezadan sonra kalan net skor.
    /// </summary>
    public double NetScore => Score - Penalty;

    /// <summary>
    /// Senaryonun tamamlanma oranı.
    /// 0.0 - 1.0 arası yorumlanır.
    /// </summary>
    public double CompletionRatio { get; init; }

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
    /// Aktif görev aşaması kimliği.
    /// </summary>
    public string? CurrentObjectiveId { get; init; }

    /// <summary>
    /// Aktif görev aşaması başlığı.
    /// </summary>
    public string? CurrentObjectiveTitle { get; init; }

    /// <summary>
    /// Sonraki görev aşaması kimliği.
    /// </summary>
    public string? NextObjectiveId { get; init; }

    /// <summary>
    /// Toplam çarpışma sayısı.
    /// </summary>
    public int CollisionCount { get; init; }

    /// <summary>
    /// Toplam no-go zone ihlal sayısı.
    /// </summary>
    public int NoGoZoneViolationCount { get; init; }

    /// <summary>
    /// Toplam sensör/fault/degraded olay sayısı.
    /// </summary>
    public int DegradedEventCount { get; init; }

    /// <summary>
    /// SafetyLimiter veya güvenlik katmanı müdahale sayısı.
    /// </summary>
    public int SafetyInterventionCount { get; init; }

    /// <summary>
    /// Koşunun başladığı UTC zaman.
    /// </summary>
    public DateTime? StartedUtc { get; init; }

    /// <summary>
    /// Koşunun bittiği UTC zaman.
    /// </summary>
    public DateTime? FinishedUtc { get; init; }

    /// <summary>
    /// Koşu süresi.
    /// </summary>
    public double ElapsedSeconds { get; init; }

    /// <summary>
    /// Başarısızlık nedeni.
    /// Başarısız değilse null kalabilir.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Kısa insan-okunabilir özet.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Görev hedefi bazlı durumlar.
    /// </summary>
    public IReadOnlyList<ScenarioObjectiveJudgeState> Objectives { get; init; }
        = Array.Empty<ScenarioObjectiveJudgeState>();

    /// <summary>
    /// Judge tarafından üretilen olaylar.
    /// Örnek: gate geçildi, hedef kaçırıldı, no-go zone ihlali oldu.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeEvent> Events { get; init; }
        = Array.Empty<ScenarioJudgeEvent>();

    /// <summary>
    /// Judge tarafından tespit edilen ihlaller.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeViolation> Violations { get; init; }
        = Array.Empty<ScenarioJudgeViolation>();

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
/// ScenarioJudge durum sabitleri.
/// </summary>
public static class ScenarioJudgeStatus
{
    public const string Running = "Running";
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Timeout = "Timeout";
    public const string Aborted = "Aborted";
    public const string NotStarted = "NotStarted";
}

/// <summary>
/// Tek bir görev hedefinin judge tarafından takip edilen durumudur.
/// </summary>
public sealed record ScenarioObjectiveJudgeState
{
    /// <summary>
    /// Hedef kimliği.
    /// </summary>
    public string ObjectiveId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef tipi.
    /// Örnek: pass_gate, reach_zone, avoid_zone.
    /// </summary>
    public string ObjectiveType { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir hedef başlığı.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Hedef sırası.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Hedef zorunlu mu?
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    /// Hedef tamamlandı mı?
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Hedef başarısız oldu mu?
    /// </summary>
    public bool IsFailed { get; init; }

    /// <summary>
    /// Hedef şu anda aktif mi?
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Hedef için kazanılan skor.
    /// </summary>
    public double ScoreEarned { get; init; }

    /// <summary>
    /// Hedef için uygulanan ceza.
    /// </summary>
    public double PenaltyApplied { get; init; }

    /// <summary>
    /// Hedefe kalan 3D mesafe.
    /// Surface vessel gibi 2D görevlerde Z farkı genellikle 0 kabul edilir.
    /// Denizaltı ve VTOL görevlerinde X/Y/Z birlikte değerlendirilir.
    /// </summary>
    public double? DistanceToTargetMeters { get; init; }

    /// <summary>
    /// Hedefe kalan yatay XY mesafesi.
    /// Denizaltı/VTOL gibi 3D platformlarda 3D mesafeden ayrı takip edilir.
    /// </summary>
    public double? HorizontalDistanceToTargetMeters { get; init; }

    /// <summary>
    /// Hedefe kalan dikey Z mesafesi.
    /// Denizaltı için derinlik farkını, hava aracı için irtifa farkını temsil edebilir.
    /// </summary>
    public double? VerticalDistanceToTargetMeters { get; init; }

    /// <summary>
    /// Hedefin başladığı UTC zaman.
    /// </summary>
    public DateTime? StartedUtc { get; init; }

    /// <summary>
    /// Hedefin tamamlandığı UTC zaman.
    /// </summary>
    public DateTime? CompletedUtc { get; init; }

    /// <summary>
    /// Hedefin başarısızlık nedeni.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Hedef ile ilişkili world object kimliği.
    /// </summary>
    public string? TargetObjectId { get; init; }

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
/// Judge tarafından üretilen olay kaydıdır.
/// </summary>
public sealed record ScenarioJudgeEvent
{
    /// <summary>
    /// Olay kimliği.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Olay zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Olay tipi.
    /// Örnek: ObjectiveStarted, ObjectiveCompleted, Collision, NoGoZoneViolation.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Olay önem seviyesi.
    /// Örnek: Info, Warning, Critical.
    /// </summary>
    public string Severity { get; init; } = "Info";

    /// <summary>
    /// İlgili hedef kimliği.
    /// </summary>
    public string? ObjectiveId { get; init; }

    /// <summary>
    /// İlgili obje kimliği.
    /// </summary>
    public string? ObjectId { get; init; }

    /// <summary>
    /// Olay mesajı.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Araç X konumu.
    /// </summary>
    public double? VehicleX { get; init; }

    /// <summary>
    /// Araç Y konumu.
    /// </summary>
    public double? VehicleY { get; init; }

    /// <summary>
    /// Araç Z konumu.
    /// Denizaltı, VTOL veya 3D görevlerde derinlik/irtifa eksenini temsil eder.
    /// </summary>
    public double? VehicleZ { get; init; }

    /// <summary>
    /// Araç yaw açısı.
    /// </summary>
    public double? VehicleYawDeg { get; init; }

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
/// Judge tarafından tespit edilen ihlal kaydıdır.
/// </summary>
public sealed record ScenarioJudgeViolation
{
    /// <summary>
    /// İhlal kimliği.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// İhlal zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// İhlal tipi.
    /// Örnek: Collision, NoGoZone, WrongGateDirection, Timeout.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// İhlal önem seviyesi.
    /// </summary>
    public string Severity { get; init; } = "Warning";

    /// <summary>
    /// İlgili hedef kimliği.
    /// </summary>
    public string? ObjectiveId { get; init; }

    /// <summary>
    /// İlgili obje kimliği.
    /// </summary>
    public string? ObjectId { get; init; }

    /// <summary>
    /// Uygulanan ceza.
    /// </summary>
    public double PenaltyApplied { get; init; }

    /// <summary>
    /// İhlal mesajı.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Araç X konumu.
    /// </summary>
    public double? VehicleX { get; init; }

    /// <summary>
    /// Araç Y konumu.
    /// </summary>
    public double? VehicleY { get; init; }

    /// <summary>
    /// Araç Z konumu.
    /// Denizaltı, VTOL veya 3D görevlerde derinlik/irtifa eksenini temsil eder.
    /// </summary>
    public double? VehicleZ { get; init; }

    /// <summary>
    /// Araç yaw açısı.
    /// </summary>
    public double? VehicleYawDeg { get; init; }

    /// <summary>
    /// Ek numeric metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ek string alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}