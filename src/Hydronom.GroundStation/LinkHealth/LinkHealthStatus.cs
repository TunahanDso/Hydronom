namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Bir bağlantının operasyonel sağlık durumunu temsil eder.
/// Bu enum ileride routing, diagnostics ve telemetry profil seçimi için kullanılacaktır.
/// </summary>
public enum LinkHealthStatus
{
    Unknown = 0,

    /// <summary>
    /// Bağlantı iyi durumda. Gecikme düşük, başarı oranı yüksek.
    /// </summary>
    Good = 1,

    /// <summary>
    /// Bağlantı kullanılabilir ama zayıflama belirtileri var.
    /// Telemetry profili düşürülebilir.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Bağlantı kritik seviyede kötü.
    /// Sadece düşük bant genişlikli veya acil mesajlar tercih edilmeli.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// Bağlantı kayıp veya uzun süredir doğrulanmadı.
    /// </summary>
    Lost = 4
}