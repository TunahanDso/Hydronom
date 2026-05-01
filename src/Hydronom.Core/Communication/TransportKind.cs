namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom sisteminde kullanılabilecek haberleşme / taşıma kanalı türlerini temsil eder.
/// 
/// Bu enum, Fleet & Ground Station mimarisinin temel parçalarından biridir.
/// Amaç; TCP, UDP, WebSocket, Serial, LoRa, RF modem, MQTT gibi farklı haberleşme
/// yöntemlerini üst seviye sistemden soyutlamaktır.
/// 
/// Üst seviye Hydronom mimarisi şunu bilmek zorunda kalmamalıdır:
/// - Mesaj Wi-Fi üzerinden mi gitti?
/// - LoRa ile mi taşındı?
/// - RF modem mi kullandı?
/// - WebSocket üzerinden mi aktı?
/// 
/// Bunun yerine sistem sadece şunu bilir:
/// "Ben bir HydronomEnvelope göndereceğim."
/// 
/// Mesajın hangi fiziksel veya mantıksal kanaldan gönderileceğine
/// CommunicationRouter / TransportManager karar verir.
/// </summary>
public enum TransportKind
{
    /// <summary>
    /// Transport türü bilinmiyor veya henüz tespit edilmedi.
    /// 
    /// Genellikle:
    /// - Varsayılan değer olarak,
    /// - Hatalı/eksik konfigürasyonda,
    /// - Henüz sınıflandırılmamış bağlantılarda kullanılır.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// TCP tabanlı bağlantı.
    /// 
    /// Kullanım alanları:
    /// - Yer istasyonu ile araç arasında güvenilir veri aktarımı,
    /// - Hydronom Runtime ile Gateway arasında bağlantı,
    /// - NDJSON / JSON tabanlı mesaj akışı,
    /// - Geliştirme ve test ortamları.
    /// 
    /// Avantaj:
    /// - Güvenilir ve sıralı veri aktarımı sağlar.
    /// 
    /// Dezavantaj:
    /// - Bağlantı kopmalarında yeniden bağlantı yönetimi gerekir.
    /// </summary>
    Tcp = 1,

    /// <summary>
    /// UDP tabanlı bağlantı.
    /// 
    /// Kullanım alanları:
    /// - Düşük gecikmeli telemetry,
    /// - Video/stream benzeri hızlı veri aktarımı,
    /// - Paket kaybının tolere edilebildiği durumlar.
    /// 
    /// Avantaj:
    /// - TCP'ye göre daha düşük gecikme sunabilir.
    /// 
    /// Dezavantaj:
    /// - Paket teslim garantisi yoktur.
    /// - Paket sırası garanti edilmez.
    /// </summary>
    Udp = 2,

    /// <summary>
    /// WebSocket tabanlı bağlantı.
    /// 
    /// Kullanım alanları:
    /// - Hydronom Ops frontend ile Gateway arasında canlı veri akışı,
    /// - Tarayıcı tabanlı yer istasyonu arayüzü,
    /// - Gerçek zamanlı telemetry ve fleet dashboard güncellemeleri.
    /// 
    /// Avantaj:
    /// - Web uygulamalarıyla doğal uyumludur.
    /// - Çift yönlü iletişim sağlar.
    /// </summary>
    WebSocket = 3,

    /// <summary>
    /// Seri port tabanlı haberleşme.
    /// 
    /// Kullanım alanları:
    /// - STM32, Arduino, Jetson yardımcı kartları,
    /// - RF modemlerin seri arayüzleri,
    /// - USB-TTL dönüştürücüler,
    /// - Donanım testleri.
    /// 
    /// Not:
    /// Serial transport bazen doğrudan araç içi modül haberleşmesi için,
    /// bazen de RF/LoRa cihazına erişmek için alt kanal olarak kullanılabilir.
    /// </summary>
    Serial = 10,

    /// <summary>
    /// LoRa tabanlı düşük bant genişlikli, uzun menzilli haberleşme.
    /// 
    /// Kullanım alanları:
    /// - Light telemetry,
    /// - Mini konum paketi,
    /// - Heartbeat,
    /// - Basit görev komutları,
    /// - Acil durum sinyalleri.
    /// 
    /// Avantaj:
    /// - Uzun menzil.
    /// - Düşük güç tüketimi.
    /// 
    /// Dezavantaj:
    /// - Düşük veri hızı.
    /// - Büyük telemetry veya video için uygun değildir.
    /// </summary>
    LoRa = 11,

    /// <summary>
    /// RF modem tabanlı haberleşme.
    /// 
    /// Kullanım alanları:
    /// - Araç ↔ yer istasyonu bağlantısı,
    /// - Görev komutları,
    /// - Normal telemetry,
    /// - Daha klasik radyo modem altyapıları.
    /// 
    /// Not:
    /// RF modemler kullanılan modele göre seri port, USB veya farklı bir arayüz
    /// üzerinden sisteme bağlanabilir. Bu enum, fiziksel cihaz sınıfını temsil eder.
    /// </summary>
    RfModem = 12,

    /// <summary>
    /// MQTT tabanlı mesajlaşma.
    /// 
    /// Kullanım alanları:
    /// - Bulut bağlantısı,
    /// - Uzak monitoring,
    /// - IoT tarzı telemetry yayınlama,
    /// - Araç/yer istasyonu dışındaki servislerle entegrasyon.
    /// 
    /// Not:
    /// Gerçek zamanlı motor kontrolü için ana kanal olmamalıdır.
    /// Daha çok telemetry, status ve cloud integration için uygundur.
    /// </summary>
    Mqtt = 20,

    /// <summary>
    /// Hücresel ağ tabanlı haberleşme.
    /// 
    /// Örnekler:
    /// - 4G
    /// - 5G
    /// - LTE modem
    /// 
    /// Kullanım alanları:
    /// - Geniş alan operasyonları,
    /// - Uzak telemetry,
    /// - Harita/veri aktarımı,
    /// - Yer istasyonu dışından izleme.
    /// 
    /// Avantaj:
    /// - Altyapı varsa uzun mesafede kullanılabilir.
    /// 
    /// Dezavantaj:
    /// - Operatör bağımlılığı vardır.
    /// - Gecikme ve bağlantı kararlılığı değişken olabilir.
    /// </summary>
    Cellular = 21,

    /// <summary>
    /// Mesh ağ tabanlı haberleşme.
    /// 
    /// Kullanım alanları:
    /// - Araçların birbirini relay olarak kullanması,
    /// - Çoklu araç operasyonları,
    /// - Ground Station'a doğrudan ulaşamayan aracın başka araç üzerinden bağlanması,
    /// - Swarm / fleet görevleri.
    /// 
    /// Not:
    /// Hydronom Fleet mimarisinde ileride çok kritik hale gelebilecek transport türlerinden biridir.
    /// </summary>
    Mesh = 22,

    /// <summary>
    /// Dosya veya kayıt üzerinden tekrar oynatma transport'u.
    /// 
    /// Kullanım alanları:
    /// - Replay sistemi,
    /// - Test senaryoları,
    /// - Simülasyon,
    /// - Yarışma sonrası analiz,
    /// - Recorded telemetry ile debugging.
    /// 
    /// Gerçek bir fiziksel haberleşme kanalı değildir.
    /// Ama sistemin diğer transport'larla aynı arayüzden test edilmesini sağlar.
    /// </summary>
    FileReplay = 100,

    /// <summary>
    /// Mock / sahte transport.
    /// 
    /// Kullanım alanları:
    /// - Unit test,
    /// - Geliştirme ortamı,
    /// - Donanım yokken sistem akışını test etme,
    /// - Simülasyon modu.
    /// 
    /// Gerçek donanıma ihtiyaç duymadan CommunicationRouter,
    /// GroundStation ve Fleet katmanlarını test etmeyi sağlar.
    /// </summary>
    Mock = 101
}