namespace Hydronom.GroundStation.Telemetry;

/// <summary>
/// Ground Station ile araç arasındaki telemetry veri yoğunluğu seviyesini temsil eder.
/// 
/// Fleet & Ground Station mimarisinde her bağlantı aynı veri miktarını taşıyamaz.
/// Örneğin:
/// - LoRa düşük bant genişliklidir.
/// - RF modem orta seviyede telemetry taşıyabilir.
/// - TCP/WebSocket/Cellular daha zengin telemetry için uygundur.
/// 
/// Bu enum, Adaptive Telemetry Profile sisteminin temelidir.
/// Amaç bağlantı durumuna göre otomatik telemetry seviyesi seçmektir.
/// </summary>
public enum TelemetryProfile
{
    /// <summary>
    /// Profil bilinmiyor veya henüz seçilmedi.
    /// 
    /// Normal üretim akışında mümkün olduğunca kullanılmamalıdır.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// En düşük veri yoğunluğuna sahip telemetry profili.
    /// 
    /// Kullanım alanları:
    /// - LoRa
    /// - Zayıf RF bağlantısı
    /// - Düşük bant genişlikli fallback durumları
    /// 
    /// İçerebilecek bilgiler:
    /// - vehicleId
    /// - position
    /// - heading
    /// - speed
    /// - battery
    /// - health
    /// - mission state
    /// </summary>
    Light = 1,

    /// <summary>
    /// Orta seviye telemetry profili.
    /// 
    /// Kullanım alanları:
    /// - RF modem
    /// - TCP bağlantısı zayıfladığında
    /// - Normal görev izleme
    /// 
    /// İçerebilecek bilgiler:
    /// - Light telemetry
    /// - sensor summary
    /// - obstacle summary
    /// - target summary
    /// - local analysis summary
    /// - actuator summary
    /// </summary>
    Normal = 2,

    /// <summary>
    /// En zengin telemetry profili.
    /// 
    /// Kullanım alanları:
    /// - TCP
    /// - WebSocket
    /// - Ethernet
    /// - Cellular / 4G / 5G
    /// - Geliştirme ve analiz ortamları
    /// 
    /// İçerebilecek bilgiler:
    /// - Normal telemetry
    /// - raw-ish fused data
    /// - map tiles
    /// - obstacle clouds
    /// - diagnostic logs
    /// - long analysis traces
    /// - AI reasoning summaries
    /// </summary>
    Full = 3
}