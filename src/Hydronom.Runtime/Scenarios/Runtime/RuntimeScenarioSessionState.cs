namespace Hydronom.Runtime.Scenarios.Runtime;

/// <summary>
/// Runtime scenario oturumunun yaşam döngüsü durumudur.
/// Bu enum, scenario'nun gerçek runtime tick akışı içinde hangi aşamada olduğunu açıkça belirtir.
/// </summary>
public enum RuntimeScenarioSessionState
{
    /// <summary>
    /// Oturum oluşturuldu ama henüz başlatılmadı.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Oturum çalışıyor; aktif objective runtime task olarak yürütülüyor.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Oturum geçici olarak duraklatıldı.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Tüm objective'ler tamamlandı ve scenario başarıyla bitti.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Scenario başarısız oldu.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Scenario süre limitini aştı.
    /// </summary>
    TimedOut = 5,

    /// <summary>
    /// Scenario dış müdahale ile iptal edildi.
    /// </summary>
    Aborted = 6
}