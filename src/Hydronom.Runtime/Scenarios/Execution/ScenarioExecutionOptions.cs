namespace Hydronom.Runtime.Scenarios.Execution;

/// <summary>
/// ScenarioKinematicExecutor için çalışma ayarlarıdır.
/// Bu ayarlar Digital Proving Ground içinde basit kinematic scenario icrası için kullanılır.
/// </summary>
public sealed record ScenarioExecutionOptions
{
    /// <summary>
    /// Koşu kimliği.
    /// Boşsa otomatik üretilir.
    /// </summary>
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Simülasyon tick süresi.
    /// </summary>
    public double DtSeconds { get; init; } = 0.1;

    /// <summary>
    /// Maksimum simülasyon süresi.
    /// Scenario içindeki TimeLimitSeconds pozitifse daha küçük olan değer kullanılabilir.
    /// </summary>
    public double MaxDurationSeconds { get; init; } = 1200.0;

    /// <summary>
    /// Basit kinematic executor için yatay hız.
    /// </summary>
    public double CruiseSpeedMetersPerSecond { get; init; } = 1.2;

    /// <summary>
    /// Z ekseni hareket hızı.
    /// Denizaltı/VTOL gibi platformlarda kullanılır.
    /// Surface vessel için genelde Z hedefi 0 olur.
    /// </summary>
    public double VerticalSpeedMetersPerSecond { get; init; } = 0.4;

    /// <summary>
    /// Objective tamamlandı kabul etmek için varsayılan tolerans.
    /// Objective veya target özel toleransı varsa o öncelikli olur.
    /// </summary>
    public double DefaultToleranceMeters { get; init; } = 1.5;

    /// <summary>
    /// Araç yaklaşık yarıçapı.
    /// Judge context içinde çarpışma/zone kontrolleri için taşınır.
    /// </summary>
    public double VehicleRadiusMeters { get; init; } = 0.5;

    /// <summary>
    /// Araç dikey toleransı.
    /// Denizaltı/VTOL gibi 3D görevlerde kullanılır.
    /// </summary>
    public double VehicleVerticalToleranceMeters { get; init; } = 0.5;

    /// <summary>
    /// Başlangıç zamanı.
    /// Boşsa DateTime.UtcNow kullanılır.
    /// </summary>
    public DateTime? StartedUtc { get; init; }

    /// <summary>
    /// Her tick state confidence değeri.
    /// </summary>
    public double StateConfidence { get; init; } = 1.0;

    /// <summary>
    /// Her tick fusion confidence değeri.
    /// </summary>
    public double FusionConfidence { get; init; } = 1.0;

    /// <summary>
    /// GPS sağlıklı kabul edilsin mi?
    /// </summary>
    public bool GpsHealthy { get; init; } = true;

    /// <summary>
    /// IMU sağlıklı kabul edilsin mi?
    /// </summary>
    public bool ImuHealthy { get; init; } = true;

    /// <summary>
    /// Engel sensörü sağlıklı kabul edilsin mi?
    /// </summary>
    public bool ObstacleSensorHealthy { get; init; } = true;

    /// <summary>
    /// Executor degraded modda başlasın mı?
    /// </summary>
    public bool IsDegradedMode { get; init; }

    /// <summary>
    /// Her tick safety limiter aktif bildirilsin mi?
    /// </summary>
    public bool SafetyLimiterActive { get; init; }

    /// <summary>
    /// Emergency stop aktif bildirilsin mi?
    /// </summary>
    public bool EmergencyStopActive { get; init; }

    /// <summary>
    /// Debug amaçlı timeline örnekleri rapora eklensin mi?
    /// </summary>
    public bool KeepTimelineSamples { get; init; } = true;

    /// <summary>
    /// Saklanacak maksimum timeline örneği.
    /// </summary>
    public int MaxStoredTimelineSamples { get; init; } = 5000;

    /// <summary>
    /// Pozitif olmayan veya geçersiz ayarları güvenli hale getirir.
    /// </summary>
    public ScenarioExecutionOptions Sanitized()
    {
        return this with
        {
            RunId = string.IsNullOrWhiteSpace(RunId) ? Guid.NewGuid().ToString("N") : RunId.Trim(),
            DtSeconds = SafePositive(DtSeconds, 0.1, 0.001, 2.0),
            MaxDurationSeconds = SafePositive(MaxDurationSeconds, 1200.0, 1.0, 7200.0),
            CruiseSpeedMetersPerSecond = SafePositive(CruiseSpeedMetersPerSecond, 1.2, 0.01, 20.0),
            VerticalSpeedMetersPerSecond = SafePositive(VerticalSpeedMetersPerSecond, 0.4, 0.01, 10.0),
            DefaultToleranceMeters = SafePositive(DefaultToleranceMeters, 1.5, 0.01, 100.0),
            VehicleRadiusMeters = SafePositive(VehicleRadiusMeters, 0.5, 0.01, 20.0),
            VehicleVerticalToleranceMeters = SafePositive(VehicleVerticalToleranceMeters, 0.5, 0.01, 50.0),
            StateConfidence = Clamp01(StateConfidence),
            FusionConfidence = Clamp01(FusionConfidence),
            MaxStoredTimelineSamples = MaxStoredTimelineSamples <= 0 ? 5000 : MaxStoredTimelineSamples
        };
    }

    private static double SafePositive(double value, double fallback, double min, double max)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return fallback;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 1.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}