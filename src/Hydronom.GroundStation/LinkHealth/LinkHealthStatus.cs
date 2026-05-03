namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Bir baÄŸlantÄ±nÄ±n operasyonel saÄŸlÄ±k durumunu temsil eder.
/// Bu enum ileride routing, diagnostics ve telemetry profil seÃ§imi iÃ§in kullanÄ±lacaktÄ±r.
/// </summary>
public enum LinkHealthStatus
{
    Unknown = 0,

    /// <summary>
    /// BaÄŸlantÄ± iyi durumda. Gecikme dÃ¼ÅŸÃ¼k, baÅŸarÄ± oranÄ± yÃ¼ksek.
    /// </summary>
    Good = 1,

    /// <summary>
    /// BaÄŸlantÄ± kullanÄ±labilir ama zayÄ±flama belirtileri var.
    /// Telemetry profili dÃ¼ÅŸÃ¼rÃ¼lebilir.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// BaÄŸlantÄ± kritik seviyede kÃ¶tÃ¼.
    /// Sadece dÃ¼ÅŸÃ¼k bant geniÅŸlikli veya acil mesajlar tercih edilmeli.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// BaÄŸlantÄ± kayÄ±p veya uzun sÃ¼redir doÄŸrulanmadÄ±.
    /// </summary>
    Lost = 4
}
