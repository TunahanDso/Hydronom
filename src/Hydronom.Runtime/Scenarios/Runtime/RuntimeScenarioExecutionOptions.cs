namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Runtime scenario execution host çalışma ayarlarıdır.
/// Bu ayarlar gerçek runtime loop içinde scenario hedeflerinin nasıl takip edileceğini belirler.
/// </summary>
public sealed record RuntimeScenarioExecutionOptions
{
    /// <summary>
    /// Scenario başladığında ilk hedef otomatik olarak task manager'a yüklensin mi?
    /// </summary>
    public bool AutoApplyFirstTarget { get; init; } = true;

    /// <summary>
    /// Objective tamamlandığında sıradaki hedef otomatik olarak task manager'a yüklensin mi?
    /// </summary>
    public bool AutoAdvanceObjectives { get; init; } = true;

    /// <summary>
    /// Scenario tamamlandığında aktif task temizlensin mi?
    /// </summary>
    public bool ClearTaskOnCompletion { get; init; } = true;

    /// <summary>
    /// Scenario fail/timeout/abort olduğunda aktif task temizlensin mi?
    /// </summary>
    public bool ClearTaskOnStop { get; init; } = true;

    /// <summary>
    /// Runtime state hedef toleransına girdiğinde objective tamamlandı kabul edilsin mi?
    /// Judge raporu ayrıca çalışır; bu alan task geçişini hızlandıran deterministic tracker içindir.
    /// </summary>
    public bool UseDistanceTrackerForAdvance { get; init; } = true;

    /// <summary>
    /// Objective tracker için minimum settle süresi.
    /// Araç tolerans içine bir frame girip çıkarsa hemen objective tamamlanmasın diye kullanılır.
    /// </summary>
    public double SettleSeconds { get; init; } = 0.35;

    /// <summary>
    /// Objective tamamlanması için hız limiti.
    /// Negatif veya 0 verilirse hız kontrolü devre dışı kabul edilir.
    /// </summary>
    public double MaxArrivalSpeedMps { get; init; } = 0.75;

    /// <summary>
    /// Objective tamamlanması için yaw rate limiti.
    /// Negatif veya 0 verilirse yaw rate kontrolü devre dışı kabul edilir.
    /// </summary>
    public double MaxArrivalYawRateDegPerSec { get; init; } = 25.0;

    /// <summary>
    /// Objective tamamlanması için yatay/3D mesafe toleransı fallback değeri.
    /// Target üzerinde tolerans yoksa kullanılır.
    /// </summary>
    public double DefaultToleranceMeters { get; init; } = 1.0;

    /// <summary>
    /// Scenario maksimum çalışma süresi.
    /// ScenarioDefinition içinde süre limiti varsa öncelik onda olabilir.
    /// Null ise host kendi timeout kararı vermez.
    /// </summary>
    public double? MaxDurationSecondsOverride { get; init; }

    /// <summary>
    /// Her tick sonrası judge raporu güncellensin mi?
    /// </summary>
    public bool EvaluateJudgeEveryTick { get; init; } = true;

    /// <summary>
    /// Geçersiz değerleri güvenli hale getirir.
    /// </summary>
    public RuntimeScenarioExecutionOptions Sanitized()
    {
        return this with
        {
            SettleSeconds = double.IsFinite(SettleSeconds) && SettleSeconds >= 0.0
                ? SettleSeconds
                : 0.35,

            MaxArrivalSpeedMps = double.IsFinite(MaxArrivalSpeedMps)
                ? MaxArrivalSpeedMps
                : 0.75,

            MaxArrivalYawRateDegPerSec = double.IsFinite(MaxArrivalYawRateDegPerSec)
                ? MaxArrivalYawRateDegPerSec
                : 25.0,

            DefaultToleranceMeters =
                double.IsFinite(DefaultToleranceMeters) && DefaultToleranceMeters > 0.0
                    ? DefaultToleranceMeters
                    : 1.0,

            MaxDurationSecondsOverride =
                MaxDurationSecondsOverride.HasValue &&
                double.IsFinite(MaxDurationSecondsOverride.Value) &&
                MaxDurationSecondsOverride.Value > 0.0
                    ? MaxDurationSecondsOverride
                    : null
        };
    }
}