namespace Hydronom.Core.Scenarios.Models;

/// <summary>
/// Hydronom senaryo tanımı.
/// JSON parkur dosyasının bellekteki karşılığıdır.
/// </summary>
public sealed record ScenarioDefinition
{
    /// <summary>
    /// Senaryo kimliği.
    /// Örnek: teknofest_usv_pool_2026_v1
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir senaryo adı.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo açıklaması.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo sürümü.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Senaryonun yarışma, test veya simülasyon ailesi.
    /// Örnek: teknofest_usv, hydrocontest, pool_validation, internal_lab.
    /// </summary>
    public string ScenarioFamily { get; init; } = "generic";

    /// <summary>
    /// Koordinat sistemi açıklaması.
    /// Şimdilik local_metric varsayılır.
    /// </summary>
    public string CoordinateFrame { get; init; } = "local_metric";

    /// <summary>
    /// Senaryonun çalışma modu.
    /// Örnek: simulation, replay, pool_test, field_test.
    /// </summary>
    public string RunMode { get; init; } = "simulation";

    /// <summary>
    /// Ana araç kimliği.
    /// </summary>
    public string VehicleId { get; init; } = "hydronom-main";

    /// <summary>
    /// Araç platform tipi.
    /// Örnek: surface_vessel, underwater_vehicle, rover, vtol.
    /// </summary>
    public string VehiclePlatform { get; init; } = "surface_vessel";

    /// <summary>
    /// Başlangıç X konumu.
    /// </summary>
    public double StartX { get; init; }

    /// <summary>
    /// Başlangıç Y konumu.
    /// </summary>
    public double StartY { get; init; }

    /// <summary>
    /// Başlangıç Z konumu.
    /// </summary>
    public double StartZ { get; init; }

    /// <summary>
    /// Başlangıç roll açısı.
    /// </summary>
    public double StartRollDeg { get; init; }

    /// <summary>
    /// Başlangıç pitch açısı.
    /// </summary>
    public double StartPitchDeg { get; init; }

    /// <summary>
    /// Başlangıç yaw/heading açısı.
    /// </summary>
    public double StartYawDeg { get; init; }

    /// <summary>
    /// Senaryo için önerilen maksimum çalışma süresi.
    /// Sıfır veya negatifse süre sınırı yok kabul edilir.
    /// </summary>
    public double TimeLimitSeconds { get; init; }

    /// <summary>
    /// Senaryonun başarı için minimum skor eşiği.
    /// </summary>
    public double MinimumSuccessScore { get; init; } = 100.0;

    /// <summary>
    /// Senaryodaki dünya/parkur objeleri.
    /// </summary>
    public IReadOnlyList<ScenarioWorldObjectDefinition> Objects { get; init; }
        = Array.Empty<ScenarioWorldObjectDefinition>();

    /// <summary>
    /// Senaryo görev hedefleri.
    /// Örnek: gate geç, hedef bölgeye ulaş, no-go zone'a girme.
    /// </summary>
    public IReadOnlyList<ScenarioMissionObjectiveDefinition> Objectives { get; init; }
        = Array.Empty<ScenarioMissionObjectiveDefinition>();

    /// <summary>
    /// Senaryo sırasında takip edilecek judge/scoring kuralları.
    /// </summary>
    public ScenarioJudgeDefinition Judge { get; init; } = new();

    /// <summary>
    /// Sensör arızası, gecikme, noise veya bağlantı kaybı gibi test olayları.
    /// Bu alan Digital Proving Ground içinde "bu koşulda ne yaptı?" analizini besler.
    /// </summary>
    public IReadOnlyList<ScenarioFaultInjectionDefinition> FaultInjections { get; init; }
        = Array.Empty<ScenarioFaultInjectionDefinition>();

    /// <summary>
    /// Genel senaryo metadata alanı.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasObjects => Objects.Count > 0;

    public bool HasObjectives => Objectives.Count > 0;

    public bool HasFaultInjections => FaultInjections.Count > 0;

    public bool HasTimeLimit => TimeLimitSeconds > 0.0;
}

/// <summary>
/// Senaryo içindeki görev hedefi tanımı.
/// </summary>
public sealed record ScenarioMissionObjectiveDefinition
{
    /// <summary>
    /// Hedef kimliği.
    /// Örnek: pass_gate_1, reach_target_zone, avoid_no_go_zone.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Hedef tipi.
    /// Örnek: pass_gate, reach_zone, avoid_zone, inspect_object, hold_position.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir başlık.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Hedef açıklaması.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Hedefin bağlı olduğu world object kimliği.
    /// Örnek: gate_1, target_zone_a.
    /// </summary>
    public string? TargetObjectId { get; init; }

    /// <summary>
    /// Görev sırası.
    /// Düşük değer önce çalışır.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Bu hedef zorunlu mu?
    /// Zorunlu hedef başarısızsa senaryo başarısız sayılabilir.
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    /// Bu hedef tamamlandığında verilecek skor.
    /// </summary>
    public double ScoreValue { get; init; } = 10.0;

    /// <summary>
    /// Hedefe ulaşma toleransı.
    /// Örneğin target zone merkezine 1.5 metre yaklaşmak yeterli olabilir.
    /// </summary>
    public double ToleranceMeters { get; init; } = 1.0;

    /// <summary>
    /// Hedef için maksimum süre.
    /// Sıfır veya negatifse hedefe özel süre sınırı yok kabul edilir.
    /// </summary>
    public double TimeLimitSeconds { get; init; }

    /// <summary>
    /// Ek hedef parametreleri.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasTimeLimit => TimeLimitSeconds > 0.0;
}

/// <summary>
/// Senaryo judge/scoring ayarları.
/// </summary>
public sealed record ScenarioJudgeDefinition
{
    /// <summary>
    /// Judge aktif mi?
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Çarpışma kontrolü aktif mi?
    /// </summary>
    public bool CheckCollisions { get; init; } = true;

    /// <summary>
    /// No-go zone ihlal kontrolü aktif mi?
    /// </summary>
    public bool CheckNoGoZones { get; init; } = true;

    /// <summary>
    /// Görev hedefleri sırayla mı tamamlanmalı?
    /// </summary>
    public bool EnforceObjectiveOrder { get; init; } = true;

    /// <summary>
    /// Zorunlu hedef başarısızsa senaryo başarısız sayılsın mı?
    /// </summary>
    public bool FailOnRequiredObjectiveFailure { get; init; } = true;

    /// <summary>
    /// Çarpışma senaryoyu doğrudan başarısız yapsın mı?
    /// </summary>
    public bool FailOnCollision { get; init; } = true;

    /// <summary>
    /// No-go zone ihlali senaryoyu doğrudan başarısız yapsın mı?
    /// </summary>
    public bool FailOnNoGoZoneViolation { get; init; } = true;

    /// <summary>
    /// Çarpışma başına skor cezası.
    /// </summary>
    public double CollisionPenalty { get; init; } = 25.0;

    /// <summary>
    /// No-go zone ihlali başına skor cezası.
    /// </summary>
    public double NoGoZonePenalty { get; init; } = 50.0;

    /// <summary>
    /// Sensör kaybı, stale telemetry veya degraded mode başına varsayılan skor cezası.
    /// </summary>
    public double DegradedOperationPenalty { get; init; } = 5.0;

    /// <summary>
    /// Ek judge parametreleri.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Senaryoya bilinçli arıza veya zorluk enjekte etmek için kullanılan tanım.
/// </summary>
public sealed record ScenarioFaultInjectionDefinition
{
    /// <summary>
    /// Arıza kimliği.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Arıza tipi.
    /// Örnek: sensor_loss, sensor_stale, gps_dropout, lidar_noise, communication_loss.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Hedef sistem/sensör.
    /// Örnek: gps0, imu0, lidar0, gateway_link.
    /// </summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// Arızanın başlayacağı zaman.
    /// </summary>
    public double StartAtSeconds { get; init; }

    /// <summary>
    /// Arıza süresi.
    /// Sıfır veya negatifse tek seferlik olay kabul edilir.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Arıza şiddeti.
    /// 0.0 - 1.0 arası yorumlanabilir.
    /// </summary>
    public double Severity { get; init; } = 1.0;

    /// <summary>
    /// Ek arıza parametreleri.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasDuration => DurationSeconds > 0.0;
}