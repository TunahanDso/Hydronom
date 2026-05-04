using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.Scenarios.Runtime;

namespace Hydronom.Core.Scenarios.Judging;

/// <summary>
/// ScenarioJudge için tek tick/değerlendirme girdisidir.
/// Senaryo tanımı, canlı koşu durumu, araç pozu ve opsiyonel sensör/safety özetlerini birlikte taşır.
/// </summary>
public sealed record ScenarioJudgeContext
{
    /// <summary>
    /// Değerlendirilecek senaryo tanımı.
    /// </summary>
    public ScenarioDefinition Scenario { get; init; } = new();

    /// <summary>
    /// Canlı senaryo koşu durumu.
    /// </summary>
    public ScenarioRunState RunState { get; init; } = new();

    /// <summary>
    /// Değerlendirmenin üretildiği UTC zaman.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

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
    /// Denizaltı için derinlik, VTOL için irtifa ekseni olarak yorumlanabilir.
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
    /// Denizaltı/VTOL görevlerinde dikey hareket analizi için önemlidir.
    /// </summary>
    public double VehicleVz { get; init; }

    /// <summary>
    /// Araç yaw rate değeri.
    /// </summary>
    public double VehicleYawRateDegPerSecond { get; init; }

    /// <summary>
    /// Araç yaklaşık yarıçapı.
    /// Çarpışma ve zone kontrolünde güvenli basit geometri olarak kullanılır.
    /// </summary>
    public double VehicleRadiusMeters { get; init; } = 0.5;

    /// <summary>
    /// Araç yüksekliği/derinlik hacmi için kaba Z toleransı.
    /// 3D çarpışma veya hedef kontrolünde kullanılabilir.
    /// </summary>
    public double VehicleVerticalToleranceMeters { get; init; } = 0.5;

    /// <summary>
    /// Runtime/state confidence değeri.
    /// 0.0 - 1.0 arası yorumlanabilir.
    /// </summary>
    public double StateConfidence { get; init; } = 1.0;

    /// <summary>
    /// Fusion confidence değeri.
    /// 0.0 - 1.0 arası yorumlanabilir.
    /// </summary>
    public double FusionConfidence { get; init; } = 1.0;

    /// <summary>
    /// GPS var ve sağlıklı mı?
    /// </summary>
    public bool GpsHealthy { get; init; } = true;

    /// <summary>
    /// IMU var ve sağlıklı mı?
    /// </summary>
    public bool ImuHealthy { get; init; } = true;

    /// <summary>
    /// LiDAR/Sonar/obstacle algısı sağlıklı mı?
    /// </summary>
    public bool ObstacleSensorHealthy { get; init; } = true;

    /// <summary>
    /// Sistem degraded modda mı çalışıyor?
    /// </summary>
    public bool IsDegradedMode { get; init; }

    /// <summary>
    /// Safety limiter bu tick içinde müdahale etti mi?
    /// </summary>
    public bool SafetyLimiterActive { get; init; }

    /// <summary>
    /// Emergency stop aktif mi?
    /// </summary>
    public bool EmergencyStopActive { get; init; }

    /// <summary>
    /// Çarpışma tespiti için harici olarak bildirilen obje kimlikleri.
    /// Sim raycast veya perception katmanı tarafından doldurulabilir.
    /// </summary>
    public IReadOnlyList<string> ReportedCollisionObjectIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// No-go zone ihlali için harici olarak bildirilen obje kimlikleri.
    /// Runtime world/safety katmanı tarafından doldurulabilir.
    /// </summary>
    public IReadOnlyList<string> ReportedNoGoZoneObjectIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Aktif arıza/fault olay kimlikleri.
    /// </summary>
    public IReadOnlyList<string> ActiveFaultIds { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Önceden kaydedilmiş judge olayları.
    /// Judge engine incremental çalışırken olay tekrarlarını önlemek için kullanılabilir.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeEvent> PreviousEvents { get; init; }
        = Array.Empty<ScenarioJudgeEvent>();

    /// <summary>
    /// Önceden kaydedilmiş judge ihlalleri.
    /// </summary>
    public IReadOnlyList<ScenarioJudgeViolation> PreviousViolations { get; init; }
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