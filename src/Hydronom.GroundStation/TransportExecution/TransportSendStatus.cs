namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Bir transport gönderim denemesinin sonucunu temsil eder.
/// 
/// Bu enum gerçek transport katmanı geldiğinde:
/// - başarılı gönderim,
/// - ACK alındı,
/// - timeout,
/// - bağlantı yok,
/// - hedef yok,
/// - transport hatası
/// gibi durumları standartlaştırmak için kullanılır.
/// </summary>
public enum TransportSendStatus
{
    Unknown = 0,

    /// <summary>
    /// Gönderim denemesi kaydedildi ama henüz sonuçlanmadı.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Mesaj transport katmanına başarıyla verildi.
    /// ACK gerekmeyen mesajlar için bu yeterli kabul edilebilir.
    /// </summary>
    Sent = 2,

    /// <summary>
    /// Mesaj gönderildi ve karşı taraftan ACK alındı.
    /// </summary>
    Acked = 3,

    /// <summary>
    /// Mesaj gönderildi ama beklenen sürede ACK veya sonuç dönmedi.
    /// </summary>
    Timeout = 4,

    /// <summary>
    /// Transport bağlantısı uygun olmadığı için gönderim yapılamadı.
    /// </summary>
    LinkUnavailable = 5,

    /// <summary>
    /// Route kararı üretilemediği veya uygulanabilir transport bulunamadığı için gönderim yapılamadı.
    /// </summary>
    RouteUnavailable = 6,

    /// <summary>
    /// Transport katmanında hata oluştu.
    /// </summary>
    Failed = 7
}