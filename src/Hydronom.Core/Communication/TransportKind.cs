namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom sisteminde kullanÄ±labilecek haberleÅŸme / taÅŸÄ±ma kanalÄ± tÃ¼rlerini temsil eder.
/// 
/// Bu enum, Fleet & Ground Station mimarisinin temel parÃ§alarÄ±ndan biridir.
/// AmaÃ§; TCP, UDP, WebSocket, Serial, LoRa, RF modem, MQTT gibi farklÄ± haberleÅŸme
/// yÃ¶ntemlerini Ã¼st seviye sistemden soyutlamaktÄ±r.
/// 
/// Ãœst seviye Hydronom mimarisi ÅŸunu bilmek zorunda kalmamalÄ±dÄ±r:
/// - Mesaj Wi-Fi Ã¼zerinden mi gitti?
/// - LoRa ile mi taÅŸÄ±ndÄ±?
/// - RF modem mi kullandÄ±?
/// - WebSocket Ã¼zerinden mi aktÄ±?
/// 
/// Bunun yerine sistem sadece ÅŸunu bilir:
/// "Ben bir HydronomEnvelope gÃ¶ndereceÄŸim."
/// 
/// MesajÄ±n hangi fiziksel veya mantÄ±ksal kanaldan gÃ¶nderileceÄŸine
/// CommunicationRouter / TransportManager karar verir.
/// </summary>
public enum TransportKind
{
    /// <summary>
    /// Transport tÃ¼rÃ¼ bilinmiyor veya henÃ¼z tespit edilmedi.
    /// 
    /// Genellikle:
    /// - VarsayÄ±lan deÄŸer olarak,
    /// - HatalÄ±/eksik konfigÃ¼rasyonda,
    /// - HenÃ¼z sÄ±nÄ±flandÄ±rÄ±lmamÄ±ÅŸ baÄŸlantÄ±larda kullanÄ±lÄ±r.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// TCP tabanlÄ± baÄŸlantÄ±.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Yer istasyonu ile araÃ§ arasÄ±nda gÃ¼venilir veri aktarÄ±mÄ±,
    /// - Hydronom Runtime ile Gateway arasÄ±nda baÄŸlantÄ±,
    /// - NDJSON / JSON tabanlÄ± mesaj akÄ±ÅŸÄ±,
    /// - GeliÅŸtirme ve test ortamlarÄ±.
    /// 
    /// Avantaj:
    /// - GÃ¼venilir ve sÄ±ralÄ± veri aktarÄ±mÄ± saÄŸlar.
    /// 
    /// Dezavantaj:
    /// - BaÄŸlantÄ± kopmalarÄ±nda yeniden baÄŸlantÄ± yÃ¶netimi gerekir.
    /// </summary>
    Tcp = 1,

    /// <summary>
    /// UDP tabanlÄ± baÄŸlantÄ±.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - DÃ¼ÅŸÃ¼k gecikmeli telemetry,
    /// - Video/stream benzeri hÄ±zlÄ± veri aktarÄ±mÄ±,
    /// - Paket kaybÄ±nÄ±n tolere edilebildiÄŸi durumlar.
    /// 
    /// Avantaj:
    /// - TCP'ye gÃ¶re daha dÃ¼ÅŸÃ¼k gecikme sunabilir.
    /// 
    /// Dezavantaj:
    /// - Paket teslim garantisi yoktur.
    /// - Paket sÄ±rasÄ± garanti edilmez.
    /// </summary>
    Udp = 2,

    /// <summary>
    /// WebSocket tabanlÄ± baÄŸlantÄ±.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Hydronom Ops frontend ile Gateway arasÄ±nda canlÄ± veri akÄ±ÅŸÄ±,
    /// - TarayÄ±cÄ± tabanlÄ± yer istasyonu arayÃ¼zÃ¼,
    /// - GerÃ§ek zamanlÄ± telemetry ve fleet dashboard gÃ¼ncellemeleri.
    /// 
    /// Avantaj:
    /// - Web uygulamalarÄ±yla doÄŸal uyumludur.
    /// - Ã‡ift yÃ¶nlÃ¼ iletiÅŸim saÄŸlar.
    /// </summary>
    WebSocket = 3,

    /// <summary>
    /// Seri port tabanlÄ± haberleÅŸme.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - STM32, Arduino, Jetson yardÄ±mcÄ± kartlarÄ±,
    /// - RF modemlerin seri arayÃ¼zleri,
    /// - USB-TTL dÃ¶nÃ¼ÅŸtÃ¼rÃ¼cÃ¼ler,
    /// - DonanÄ±m testleri.
    /// 
    /// Not:
    /// Serial transport bazen doÄŸrudan araÃ§ iÃ§i modÃ¼l haberleÅŸmesi iÃ§in,
    /// bazen de RF/LoRa cihazÄ±na eriÅŸmek iÃ§in alt kanal olarak kullanÄ±labilir.
    /// </summary>
    Serial = 10,

    /// <summary>
    /// LoRa tabanlÄ± dÃ¼ÅŸÃ¼k bant geniÅŸlikli, uzun menzilli haberleÅŸme.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Light telemetry,
    /// - Mini konum paketi,
    /// - Heartbeat,
    /// - Basit gÃ¶rev komutlarÄ±,
    /// - Acil durum sinyalleri.
    /// 
    /// Avantaj:
    /// - Uzun menzil.
    /// - DÃ¼ÅŸÃ¼k gÃ¼Ã§ tÃ¼ketimi.
    /// 
    /// Dezavantaj:
    /// - DÃ¼ÅŸÃ¼k veri hÄ±zÄ±.
    /// - BÃ¼yÃ¼k telemetry veya video iÃ§in uygun deÄŸildir.
    /// </summary>
    LoRa = 11,

    /// <summary>
    /// RF modem tabanlÄ± haberleÅŸme.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - AraÃ§ â†” yer istasyonu baÄŸlantÄ±sÄ±,
    /// - GÃ¶rev komutlarÄ±,
    /// - Normal telemetry,
    /// - Daha klasik radyo modem altyapÄ±larÄ±.
    /// 
    /// Not:
    /// RF modemler kullanÄ±lan modele gÃ¶re seri port, USB veya farklÄ± bir arayÃ¼z
    /// Ã¼zerinden sisteme baÄŸlanabilir. Bu enum, fiziksel cihaz sÄ±nÄ±fÄ±nÄ± temsil eder.
    /// </summary>
    RfModem = 12,

    /// <summary>
    /// MQTT tabanlÄ± mesajlaÅŸma.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Bulut baÄŸlantÄ±sÄ±,
    /// - Uzak monitoring,
    /// - IoT tarzÄ± telemetry yayÄ±nlama,
    /// - AraÃ§/yer istasyonu dÄ±ÅŸÄ±ndaki servislerle entegrasyon.
    /// 
    /// Not:
    /// GerÃ§ek zamanlÄ± motor kontrolÃ¼ iÃ§in ana kanal olmamalÄ±dÄ±r.
    /// Daha Ã§ok telemetry, status ve cloud integration iÃ§in uygundur.
    /// </summary>
    Mqtt = 20,

    /// <summary>
    /// HÃ¼cresel aÄŸ tabanlÄ± haberleÅŸme.
    /// 
    /// Ã–rnekler:
    /// - 4G
    /// - 5G
    /// - LTE modem
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - GeniÅŸ alan operasyonlarÄ±,
    /// - Uzak telemetry,
    /// - Harita/veri aktarÄ±mÄ±,
    /// - Yer istasyonu dÄ±ÅŸÄ±ndan izleme.
    /// 
    /// Avantaj:
    /// - AltyapÄ± varsa uzun mesafede kullanÄ±labilir.
    /// 
    /// Dezavantaj:
    /// - OperatÃ¶r baÄŸÄ±mlÄ±lÄ±ÄŸÄ± vardÄ±r.
    /// - Gecikme ve baÄŸlantÄ± kararlÄ±lÄ±ÄŸÄ± deÄŸiÅŸken olabilir.
    /// </summary>
    Cellular = 21,

    /// <summary>
    /// Mesh aÄŸ tabanlÄ± haberleÅŸme.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - AraÃ§larÄ±n birbirini relay olarak kullanmasÄ±,
    /// - Ã‡oklu araÃ§ operasyonlarÄ±,
    /// - Ground Station'a doÄŸrudan ulaÅŸamayan aracÄ±n baÅŸka araÃ§ Ã¼zerinden baÄŸlanmasÄ±,
    /// - Swarm / fleet gÃ¶revleri.
    /// 
    /// Not:
    /// Hydronom Fleet mimarisinde ileride Ã§ok kritik hale gelebilecek transport tÃ¼rlerinden biridir.
    /// </summary>
    Mesh = 22,

    /// <summary>
    /// Dosya veya kayÄ±t Ã¼zerinden tekrar oynatma transport'u.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Replay sistemi,
    /// - Test senaryolarÄ±,
    /// - SimÃ¼lasyon,
    /// - YarÄ±ÅŸma sonrasÄ± analiz,
    /// - Recorded telemetry ile debugging.
    /// 
    /// GerÃ§ek bir fiziksel haberleÅŸme kanalÄ± deÄŸildir.
    /// Ama sistemin diÄŸer transport'larla aynÄ± arayÃ¼zden test edilmesini saÄŸlar.
    /// </summary>
    FileReplay = 100,

    /// <summary>
    /// Mock / sahte transport.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Unit test,
    /// - GeliÅŸtirme ortamÄ±,
    /// - DonanÄ±m yokken sistem akÄ±ÅŸÄ±nÄ± test etme,
    /// - SimÃ¼lasyon modu.
    /// 
    /// GerÃ§ek donanÄ±ma ihtiyaÃ§ duymadan CommunicationRouter,
    /// GroundStation ve Fleet katmanlarÄ±nÄ± test etmeyi saÄŸlar.
    /// </summary>
    Mock = 101
}
