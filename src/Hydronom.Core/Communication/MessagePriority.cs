namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom mesajlarının önem / öncelik seviyesini temsil eder.
/// 
/// Fleet & Ground Station mimarisinde her mesaj aynı öneme sahip değildir.
/// Örneğin:
/// - Bir heartbeat mesajı düzenli ama düşük öncelikli olabilir.
/// - Bir görev komutu orta/yüksek öncelikli olabilir.
/// - Bir EmergencyStop mesajı ise sistemdeki en kritik mesajlardan biridir.
/// 
/// Bu enum sayesinde CommunicationRouter, TransportManager veya GroundStation
/// mesajları önceliğine göre sıralayabilir, farklı kanallardan gönderebilir
/// veya kritik mesajlar için ACK / tekrar gönderim gibi mekanizmalar uygulayabilir.
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// Öncelik bilinmiyor veya mesaj henüz sınıflandırılmadı.
    /// 
    /// Normalde üretim sisteminde mümkün olduğunca kullanılmamalıdır.
    /// Daha çok varsayılan değer veya eksik konfigürasyon durumları içindir.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Düşük öncelikli mesaj.
    /// 
    /// Kullanım örnekleri:
    /// - Periyodik düşük önem telemetry
    /// - Debug bilgileri
    /// - Uzun analiz özetleri
    /// - Gecikse de sistemi doğrudan riske sokmayacak mesajlar
    /// </summary>
    Low = 1,

    /// <summary>
    /// Normal öncelikli mesaj.
    /// 
    /// Kullanım örnekleri:
    /// - Standart vehicle status
    /// - Normal heartbeat
    /// - Genel telemetry
    /// - Fleet registry güncellemeleri
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Yüksek öncelikli mesaj.
    /// 
    /// Kullanım örnekleri:
    /// - Görev komutu
    /// - Araç rol değişimi
    /// - Bağlantı kalitesi kritik uyarısı
    /// - Önemli health uyarısı
    /// - Operatör tarafından gönderilen kontrol komutları
    /// </summary>
    High = 3,

    /// <summary>
    /// Kritik öncelikli mesaj.
    /// 
    /// Kullanım örnekleri:
    /// - Safety uyarıları
    /// - Çarpışma riski
    /// - Araç kaybı / bağlantı kopması
    /// - Failover gerektiren durumlar
    /// - Komutun gecikmesi halinde sistem güvenliğini etkileyebilecek olaylar
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Acil durum önceliği.
    /// 
    /// Kullanım örnekleri:
    /// - EmergencyStop
    /// - Kill switch
    /// - Tüm araçlara acil dur komutu
    /// - Operasyon iptali
    /// - İnsan, araç veya çevre güvenliği için anında uygulanması gereken mesajlar
    /// 
    /// Not:
    /// Bu seviyedeki mesajlar ileride CommunicationRouter tarafından
    /// mümkün olan tüm kanallardan yayınlanabilir:
    /// Wi-Fi + RF + LoRa + Serial + Mesh gibi.
    /// </summary>
    Emergency = 5
}