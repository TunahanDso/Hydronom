namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom Fleet & Ground Station mimarisinde tÃ¼m haberleÅŸme kanallarÄ±nÄ±n
/// ortak davranÄ±ÅŸ sÃ¶zleÅŸmesini temsil eder.
/// 
/// Bu arayÃ¼zÃ¼n amacÄ±:
/// - TCP, WebSocket, Serial, LoRa, RF modem, MQTT, Cellular, Mesh gibi farklÄ±
///   haberleÅŸme yÃ¶ntemlerini tek bir ortak model altÄ±nda toplamak.
/// - Ãœst seviye Hydronom sisteminin "mesaj nasÄ±l taÅŸÄ±ndÄ±?" detayÄ±nÄ± bilmesini engellemek.
/// - CommunicationRouter ve TransportManager gibi modÃ¼llerin farklÄ± transport'larÄ±
///   plug-and-play ÅŸekilde kullanabilmesini saÄŸlamaktÄ±r.
/// 
/// Yani Ã¼st seviye sistem sadece HydronomEnvelope Ã¼retir.
/// Bu envelope'un hangi kanaldan gÃ¶nderileceÄŸine transport katmanÄ± karar verir.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Transport instance'Ä±nÄ±n okunabilir adÄ±.
    /// 
    /// Ã–rnekler:
    /// - "tcp-main"
    /// - "websocket-ops"
    /// - "lora-long-range"
    /// - "rf-915mhz"
    /// - "serial-stm32"
    /// 
    /// Bu isim:
    /// - Loglarda,
    /// - Link kalite takibinde,
    /// - Diagnostics ekranlarÄ±nda,
    /// - CommunicationRouter kararlarÄ±nda kullanÄ±labilir.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Transport'un tÃ¼rÃ¼.
    /// 
    /// Ã–rnek:
    /// - TransportKind.Tcp
    /// - TransportKind.WebSocket
    /// - TransportKind.LoRa
    /// - TransportKind.RfModem
    /// 
    /// Bu bilgi routing policy iÃ§in Ã¶nemlidir.
    /// Ã–rneÄŸin:
    /// - Full telemetry yÃ¼ksek bant geniÅŸlikli kanala yÃ¶nlendirilebilir.
    /// - Light telemetry LoRa/RF Ã¼zerinden gÃ¶nderilebilir.
    /// - EmergencyStop tÃ¼m uygun transport'lardan yayÄ±nlanabilir.
    /// </summary>
    TransportKind Kind { get; }

    /// <summary>
    /// Transport'un ÅŸu anda baÄŸlÄ± veya kullanÄ±labilir olup olmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// true:
    /// - Mesaj gÃ¶nderimi yapÄ±labilir.
    /// - ReceiveAsync Ã¼zerinden mesaj alÄ±nabilir.
    /// 
    /// false:
    /// - Transport kopmuÅŸ olabilir.
    /// - DonanÄ±m bulunamamÄ±ÅŸ olabilir.
    /// - BaÄŸlantÄ± henÃ¼z kurulmamÄ±ÅŸ olabilir.
    /// 
    /// CommunicationRouter bu deÄŸeri kullanarak aktif kanallarÄ± seÃ§ebilir.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Transport baÄŸlantÄ±sÄ±nÄ± baÅŸlatÄ±r.
    /// 
    /// TCP iÃ§in:
    /// - Socket baÄŸlantÄ±sÄ± aÃ§abilir.
    /// 
    /// WebSocket iÃ§in:
    /// - WebSocket endpoint'e baÄŸlanabilir.
    /// 
    /// Serial / LoRa / RF iÃ§in:
    /// - Seri portu aÃ§abilir.
    /// - Cihaz handshake'i yapabilir.
    /// 
    /// Mock / FileReplay iÃ§in:
    /// - SimÃ¼lasyon veya replay kaynaÄŸÄ±nÄ± hazÄ±rlayabilir.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transport baÄŸlantÄ±sÄ±nÄ± dÃ¼zgÃ¼n ÅŸekilde kapatÄ±r.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Uygulama kapanÄ±ÅŸÄ±,
    /// - Link deÄŸiÅŸimi,
    /// - DonanÄ±m hot-reload,
    /// - BaÄŸlantÄ± resetleme,
    /// - Test teardown iÅŸlemleri.
    /// 
    /// Not:
    /// Disconnect sonrasÄ±nda IsConnected false dÃ¶nmelidir.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir HydronomEnvelope mesajÄ±nÄ± bu transport Ã¼zerinden gÃ¶nderir.
    /// 
    /// Transport implementasyonu burada kendi detaylarÄ±nÄ± uygular:
    /// - TCP ise JSON/NDJSON olarak yazar.
    /// - WebSocket ise socket frame gÃ¶nderir.
    /// - LoRa ise payload boyutuna gÃ¶re paketleyebilir.
    /// - RF modem ise seri protokol Ã¼zerinden aktarabilir.
    /// - FileReplay ise dosyaya yazabilir veya simÃ¼le edebilir.
    /// 
    /// Ãœst seviye sistem bu detaylarÄ± bilmez.
    /// </summary>
    Task SendAsync(
        HydronomEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu transport Ã¼zerinden gelen HydronomEnvelope mesajlarÄ±nÄ± asenkron olarak Ã¼retir.
    /// 
    /// IAsyncEnumerable kullanmamÄ±zÄ±n sebebi:
    /// - Transport sÃ¼rekli mesaj Ã¼retebilir.
    /// - Gateway veya Runtime bunu await foreach ile dinleyebilir.
    /// - BaÄŸlantÄ± kopana veya cancellation istenene kadar akÄ±ÅŸ devam edebilir.
    /// 
    /// Ã–rnek kullanÄ±m:
    /// await foreach (var envelope in transport.ReceiveAsync(ct))
    /// {
    ///     router.Handle(envelope);
    /// }
    /// </summary>
    IAsyncEnumerable<HydronomEnvelope> ReceiveAsync(
        CancellationToken cancellationToken = default);
}
