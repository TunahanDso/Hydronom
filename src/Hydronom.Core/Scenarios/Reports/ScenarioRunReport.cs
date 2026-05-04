using Hydronom.Core.Scenarios.Judging;
using Hydronom.Core.Scenarios.Runtime;

namespace Hydronom.Core.Scenarios.Reports;

/// <summary>
/// Digital Proving Ground içinde bir senaryo koşusu tamamlandıktan sonra üretilen final rapordur.
/// Bu rapor "Hydronom bu parkurda kaç kez başarılı oldu, nerede saçmaladı,
/// hangi sensör kaybında ne yaptı, hangi görev aşamasında kaldı?" sorularını cevaplamak için kullanılır.
/// </summary>
public sealed record ScenarioRunReport
{
    /// <summary>
    /// Rapor kimliği.
    /// </summary>
    public string ReportId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo adı.
    /// </summary>
    public string ScenarioName { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo ailesi.
    /// Örnek: teknofest_usv, hydrocontest, pool_validation, internal_lab.
    /// </summary>
    public string ScenarioFamily { get; init; } = "generic";

    /// <summary>
    /// Tekil koşu kimliği.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

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
    /// Raporun üretildiği UTC zaman.
    /// </summary>
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;

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
    /// Final koşu durumu.
    /// Örnek: Completed, Failed, Timeout, Aborted.
    /// </summary>
    public string FinalStatus { get; init; } = ScenarioRunStatus.NotStarted;

    /// <summary>
    /// Judge final sonucu.
    /// </summary>
    public string JudgeStatus { get; init; } = ScenarioJudgeStatus.NotStarted;

    /// <summary>
    /// Koşu başarılı mı?
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Koşu başarısız mı?
    /// </summary>
    public bool IsFailure { get; init; }

    /// <summary>
    /// Koşu zaman aşımına uğradı mı?
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// Koşu iptal edildi mi?
    /// </summary>
    public bool IsAborted { get; init; }

    /// <summary>
    /// Başarısızlık veya iptal nedeni.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Toplam skor.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Toplam ceza.
    /// </summary>
    public double Penalty { get; init; }

    /// <summary>
    /// Net skor.
    /// </summary>
    public double NetScore => Score - Penalty;

    /// <summary>
    /// Minimum başarı skoru.
    /// </summary>
    public double MinimumSuccessScore { get; init; }

    /// <summary>
    /// Görev tamamlanma oranı.
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
    /// Başarısız görev hedefi sayısı.
    /// </summary>
    public int FailedObjectiveCount { get; init; }

    /// <summary>
    /// Koşu bittiğinde aktif olan görev hedefi.
    /// Eğer sistem bir aşamada takıldıysa burası o aşamayı gösterir.
    /// </summary>
    public string? FinalObjectiveId { get; init; }

    /// <summary>
    /// Koşu bittiğinde aktif olan görev hedefi başlığı.
    /// </summary>
    public string? FinalObjectiveTitle { get; init; }

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
    /// Maksimum araç hızı.
    /// 3D hız büyüklüğü olarak yorumlanır.
    /// </summary>
    public double MaxSpeedMetersPerSecond { get; init; }

    /// <summary>
    /// Ortalama araç hızı.
    /// </summary>
    public double AverageSpeedMetersPerSecond { get; init; }

    /// <summary>
    /// Maksimum yaw rate.
    /// </summary>
    public double MaxYawRateDegPerSecond { get; init; }

    /// <summary>
    /// Kat edilen yaklaşık toplam 3D mesafe.
    /// </summary>
    public double TotalTravelDistanceMeters { get; init; }

    /// <summary>
    /// Kat edilen yaklaşık yatay XY mesafe.
    /// </summary>
    public double TotalHorizontalTravelDistanceMeters { get; init; }

    /// <summary>
    /// Toplam dikey Z hareket miktarı.
    /// Denizaltı/VTOL görevlerinde derinlik/irtifa hareketini analiz eder.
    /// </summary>
    public double TotalVerticalTravelDistanceMeters { get; init; }

    /// <summary>
    /// Koşu sonundaki araç X konumu.
    /// </summary>
    public double FinalVehicleX { get; init; }

    /// <summary>
    /// Koşu sonundaki araç Y konumu.
    /// </summary>
    public double FinalVehicleY { get; init; }

    /// <summary>
    /// Koşu sonundaki araç Z konumu.
    /// Denizaltı için derinlik, VTOL için irtifa eksenini temsil edebilir.
    /// </summary>
    public double FinalVehicleZ { get; init; }

    /// <summary>
    /// Koşu sonundaki araç yaw açısı.
    /// </summary>
    public double FinalVehicleYawDeg { get; init; }

    /// <summary>
    /// Kısa insan-okunabilir rapor özeti.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Judge final sonucu.
    /// </summary>
    public ScenarioJudgeResult? JudgeResult { get; init; }

    /// <summary>
    /// Son canlı koşu durumu.
    /// </summary>
    public ScenarioRunState? FinalRunState { get; init; }

    /// <summary>
    /// Görev hedefi bazlı durumlar.
    /// </summary>
    public IReadOnlyList<ScenarioObjectiveJudgeState> Objectives { get; init; }
        = Array.Empty<ScenarioObjectiveJudgeState>();

    /// <summary>
    /// Koşu boyunca kaydedilen judge olayları.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeEvent> Events { get; init; }
        = Array.Empty<ScenarioJudgeEvent>();

    /// <summary>
    /// Koşu boyunca kaydedilen ihlaller.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeViolation> Violations { get; init; }
        = Array.Empty<ScenarioJudgeViolation>();

    /// <summary>
    /// Koşu boyunca yaşanan fault/degraded olayları.
    /// </summary>
    public IReadOnlyList<ScenarioRunFaultEvent> FaultEvents { get; init; }
        = Array.Empty<ScenarioRunFaultEvent>();

    /// <summary>
    /// Özet numeric metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ek string alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Senaryo koşusunda yaşanan fault/degraded olay kaydıdır.
/// </summary>
public sealed record ScenarioRunFaultEvent
{
    /// <summary>
    /// Olay kimliği.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Olayın bağlı olduğu senaryo kimliği.
    /// </summary>
    public string ScenarioId { get; init; } = string.Empty;

    /// <summary>
    /// Olayın bağlı olduğu koşu kimliği.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Olay zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Olay tipi.
    /// Örnek: SensorLoss, SensorStale, GpsDropout, LidarNoise, CommunicationLoss.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Hedef sistem/sensör.
    /// Örnek: gps0, imu0, lidar0, gateway_link.
    /// </summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// Olay önem seviyesi.
    /// </summary>
    public string Severity { get; init; } = "Warning";

    /// <summary>
    /// Olay mesajı.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Olay sırasında araç X konumu.
    /// </summary>
    public double? VehicleX { get; init; }

    /// <summary>
    /// Olay sırasında araç Y konumu.
    /// </summary>
    public double? VehicleY { get; init; }

    /// <summary>
    /// Olay sırasında araç Z konumu.
    /// Denizaltı/VTOL gibi 3D platformlarda derinlik/irtifa takibi için kullanılır.
    /// </summary>
    public double? VehicleZ { get; init; }

    /// <summary>
    /// Olay sırasında aktif görev hedefi.
    /// </summary>
    public string? ObjectiveId { get; init; }

    /// <summary>
    /// Olay sonucunda safety/degraded davranışı tetiklendi mi?
    /// </summary>
    public bool TriggeredDegradedMode { get; init; }

    /// <summary>
    /// Olay sonucunda safety limiter müdahale etti mi?
    /// </summary>
    public bool TriggeredSafetyIntervention { get; init; }

    /// <summary>
    /// Uygulanan ceza.
    /// </summary>
    public double PenaltyApplied { get; init; }

    /// <summary>
    /// Ek numeric metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ek string alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}