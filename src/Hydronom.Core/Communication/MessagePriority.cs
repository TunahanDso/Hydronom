namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom mesajlarÄ±nÄ±n Ã¶nem / Ã¶ncelik seviyesini temsil eder.
/// 
/// Fleet & Ground Station mimarisinde her mesaj aynÄ± Ã¶neme sahip deÄŸildir.
/// Ã–rneÄŸin:
/// - Bir heartbeat mesajÄ± dÃ¼zenli ama dÃ¼ÅŸÃ¼k Ã¶ncelikli olabilir.
/// - Bir gÃ¶rev komutu orta/yÃ¼ksek Ã¶ncelikli olabilir.
/// - Bir EmergencyStop mesajÄ± ise sistemdeki en kritik mesajlardan biridir.
/// 
/// Bu enum sayesinde CommunicationRouter, TransportManager veya GroundStation
/// mesajlarÄ± Ã¶nceliÄŸine gÃ¶re sÄ±ralayabilir, farklÄ± kanallardan gÃ¶nderebilir
/// veya kritik mesajlar iÃ§in ACK / tekrar gÃ¶nderim gibi mekanizmalar uygulayabilir.
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// Ã–ncelik bilinmiyor veya mesaj henÃ¼z sÄ±nÄ±flandÄ±rÄ±lmadÄ±.
    /// 
    /// Normalde Ã¼retim sisteminde mÃ¼mkÃ¼n olduÄŸunca kullanÄ±lmamalÄ±dÄ±r.
    /// Daha Ã§ok varsayÄ±lan deÄŸer veya eksik konfigÃ¼rasyon durumlarÄ± iÃ§indir.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// DÃ¼ÅŸÃ¼k Ã¶ncelikli mesaj.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - Periyodik dÃ¼ÅŸÃ¼k Ã¶nem telemetry
    /// - Debug bilgileri
    /// - Uzun analiz Ã¶zetleri
    /// - Gecikse de sistemi doÄŸrudan riske sokmayacak mesajlar
    /// </summary>
    Low = 1,

    /// <summary>
    /// Normal Ã¶ncelikli mesaj.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - Standart vehicle status
    /// - Normal heartbeat
    /// - Genel telemetry
    /// - Fleet registry gÃ¼ncellemeleri
    /// </summary>
    Normal = 2,

    /// <summary>
    /// YÃ¼ksek Ã¶ncelikli mesaj.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - GÃ¶rev komutu
    /// - AraÃ§ rol deÄŸiÅŸimi
    /// - BaÄŸlantÄ± kalitesi kritik uyarÄ±sÄ±
    /// - Ã–nemli health uyarÄ±sÄ±
    /// - OperatÃ¶r tarafÄ±ndan gÃ¶nderilen kontrol komutlarÄ±
    /// </summary>
    High = 3,

    /// <summary>
    /// Kritik Ã¶ncelikli mesaj.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - Safety uyarÄ±larÄ±
    /// - Ã‡arpÄ±ÅŸma riski
    /// - AraÃ§ kaybÄ± / baÄŸlantÄ± kopmasÄ±
    /// - Failover gerektiren durumlar
    /// - Komutun gecikmesi halinde sistem gÃ¼venliÄŸini etkileyebilecek olaylar
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Acil durum Ã¶nceliÄŸi.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - EmergencyStop
    /// - Kill switch
    /// - TÃ¼m araÃ§lara acil dur komutu
    /// - Operasyon iptali
    /// - Ä°nsan, araÃ§ veya Ã§evre gÃ¼venliÄŸi iÃ§in anÄ±nda uygulanmasÄ± gereken mesajlar
    /// 
    /// Not:
    /// Bu seviyedeki mesajlar ileride CommunicationRouter tarafÄ±ndan
    /// mÃ¼mkÃ¼n olan tÃ¼m kanallardan yayÄ±nlanabilir:
    /// Wi-Fi + RF + LoRa + Serial + Mesh gibi.
    /// </summary>
    Emergency = 5
}
