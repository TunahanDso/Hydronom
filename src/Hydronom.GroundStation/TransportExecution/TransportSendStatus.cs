namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Bir transport gÃ¶nderim denemesinin sonucunu temsil eder.
/// 
/// Bu enum gerÃ§ek transport katmanÄ± geldiÄŸinde:
/// - baÅŸarÄ±lÄ± gÃ¶nderim,
/// - ACK alÄ±ndÄ±,
/// - timeout,
/// - baÄŸlantÄ± yok,
/// - hedef yok,
/// - transport hatasÄ±
/// gibi durumlarÄ± standartlaÅŸtÄ±rmak iÃ§in kullanÄ±lÄ±r.
/// </summary>
public enum TransportSendStatus
{
    Unknown = 0,

    /// <summary>
    /// GÃ¶nderim denemesi kaydedildi ama henÃ¼z sonuÃ§lanmadÄ±.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Mesaj transport katmanÄ±na baÅŸarÄ±yla verildi.
    /// ACK gerekmeyen mesajlar iÃ§in bu yeterli kabul edilebilir.
    /// </summary>
    Sent = 2,

    /// <summary>
    /// Mesaj gÃ¶nderildi ve karÅŸÄ± taraftan ACK alÄ±ndÄ±.
    /// </summary>
    Acked = 3,

    /// <summary>
    /// Mesaj gÃ¶nderildi ama beklenen sÃ¼rede ACK veya sonuÃ§ dÃ¶nmedi.
    /// </summary>
    Timeout = 4,

    /// <summary>
    /// Transport baÄŸlantÄ±sÄ± uygun olmadÄ±ÄŸÄ± iÃ§in gÃ¶nderim yapÄ±lamadÄ±.
    /// </summary>
    LinkUnavailable = 5,

    /// <summary>
    /// Route kararÄ± Ã¼retilemediÄŸi veya uygulanabilir transport bulunamadÄ±ÄŸÄ± iÃ§in gÃ¶nderim yapÄ±lamadÄ±.
    /// </summary>
    RouteUnavailable = 6,

    /// <summary>
    /// Transport katmanÄ±nda hata oluÅŸtu.
    /// </summary>
    Failed = 7
}
