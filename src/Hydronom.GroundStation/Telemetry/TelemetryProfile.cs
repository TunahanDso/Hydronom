namespace Hydronom.GroundStation.Telemetry;

/// <summary>
/// Ground Station ile araÃ§ arasÄ±ndaki telemetry veri yoÄŸunluÄŸu seviyesini temsil eder.
/// 
/// Fleet & Ground Station mimarisinde her baÄŸlantÄ± aynÄ± veri miktarÄ±nÄ± taÅŸÄ±yamaz.
/// Ã–rneÄŸin:
/// - LoRa dÃ¼ÅŸÃ¼k bant geniÅŸliklidir.
/// - RF modem orta seviyede telemetry taÅŸÄ±yabilir.
/// - TCP/WebSocket/Cellular daha zengin telemetry iÃ§in uygundur.
/// 
/// Bu enum, Adaptive Telemetry Profile sisteminin temelidir.
/// AmaÃ§ baÄŸlantÄ± durumuna gÃ¶re otomatik telemetry seviyesi seÃ§mektir.
/// </summary>
public enum TelemetryProfile
{
    /// <summary>
    /// Profil bilinmiyor veya henÃ¼z seÃ§ilmedi.
    /// 
    /// Normal Ã¼retim akÄ±ÅŸÄ±nda mÃ¼mkÃ¼n olduÄŸunca kullanÄ±lmamalÄ±dÄ±r.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// En dÃ¼ÅŸÃ¼k veri yoÄŸunluÄŸuna sahip telemetry profili.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - LoRa
    /// - ZayÄ±f RF baÄŸlantÄ±sÄ±
    /// - DÃ¼ÅŸÃ¼k bant geniÅŸlikli fallback durumlarÄ±
    /// 
    /// Ä°Ã§erebilecek bilgiler:
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
    /// KullanÄ±m alanlarÄ±:
    /// - RF modem
    /// - TCP baÄŸlantÄ±sÄ± zayÄ±fladÄ±ÄŸÄ±nda
    /// - Normal gÃ¶rev izleme
    /// 
    /// Ä°Ã§erebilecek bilgiler:
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
    /// KullanÄ±m alanlarÄ±:
    /// - TCP
    /// - WebSocket
    /// - Ethernet
    /// - Cellular / 4G / 5G
    /// - GeliÅŸtirme ve analiz ortamlarÄ±
    /// 
    /// Ä°Ã§erebilecek bilgiler:
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
