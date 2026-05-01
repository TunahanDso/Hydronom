namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom Fleet & Ground Station mimarisinde tüm haberleşme kanallarının
/// ortak davranış sözleşmesini temsil eder.
/// 
/// Bu arayüzün amacı:
/// - TCP, WebSocket, Serial, LoRa, RF modem, MQTT, Cellular, Mesh gibi farklı
///   haberleşme yöntemlerini tek bir ortak model altında toplamak.
/// - Üst seviye Hydronom sisteminin "mesaj nasıl taşındı?" detayını bilmesini engellemek.
/// - CommunicationRouter ve TransportManager gibi modüllerin farklı transport'ları
///   plug-and-play şekilde kullanabilmesini sağlamaktır.
/// 
/// Yani üst seviye sistem sadece HydronomEnvelope üretir.
/// Bu envelope'un hangi kanaldan gönderileceğine transport katmanı karar verir.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Transport instance'ının okunabilir adı.
    /// 
    /// Örnekler:
    /// - "tcp-main"
    /// - "websocket-ops"
    /// - "lora-long-range"
    /// - "rf-915mhz"
    /// - "serial-stm32"
    /// 
    /// Bu isim:
    /// - Loglarda,
    /// - Link kalite takibinde,
    /// - Diagnostics ekranlarında,
    /// - CommunicationRouter kararlarında kullanılabilir.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Transport'un türü.
    /// 
    /// Örnek:
    /// - TransportKind.Tcp
    /// - TransportKind.WebSocket
    /// - TransportKind.LoRa
    /// - TransportKind.RfModem
    /// 
    /// Bu bilgi routing policy için önemlidir.
    /// Örneğin:
    /// - Full telemetry yüksek bant genişlikli kanala yönlendirilebilir.
    /// - Light telemetry LoRa/RF üzerinden gönderilebilir.
    /// - EmergencyStop tüm uygun transport'lardan yayınlanabilir.
    /// </summary>
    TransportKind Kind { get; }

    /// <summary>
    /// Transport'un şu anda bağlı veya kullanılabilir olup olmadığını belirtir.
    /// 
    /// true:
    /// - Mesaj gönderimi yapılabilir.
    /// - ReceiveAsync üzerinden mesaj alınabilir.
    /// 
    /// false:
    /// - Transport kopmuş olabilir.
    /// - Donanım bulunamamış olabilir.
    /// - Bağlantı henüz kurulmamış olabilir.
    /// 
    /// CommunicationRouter bu değeri kullanarak aktif kanalları seçebilir.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Transport bağlantısını başlatır.
    /// 
    /// TCP için:
    /// - Socket bağlantısı açabilir.
    /// 
    /// WebSocket için:
    /// - WebSocket endpoint'e bağlanabilir.
    /// 
    /// Serial / LoRa / RF için:
    /// - Seri portu açabilir.
    /// - Cihaz handshake'i yapabilir.
    /// 
    /// Mock / FileReplay için:
    /// - Simülasyon veya replay kaynağını hazırlayabilir.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transport bağlantısını düzgün şekilde kapatır.
    /// 
    /// Kullanım alanları:
    /// - Uygulama kapanışı,
    /// - Link değişimi,
    /// - Donanım hot-reload,
    /// - Bağlantı resetleme,
    /// - Test teardown işlemleri.
    /// 
    /// Not:
    /// Disconnect sonrasında IsConnected false dönmelidir.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir HydronomEnvelope mesajını bu transport üzerinden gönderir.
    /// 
    /// Transport implementasyonu burada kendi detaylarını uygular:
    /// - TCP ise JSON/NDJSON olarak yazar.
    /// - WebSocket ise socket frame gönderir.
    /// - LoRa ise payload boyutuna göre paketleyebilir.
    /// - RF modem ise seri protokol üzerinden aktarabilir.
    /// - FileReplay ise dosyaya yazabilir veya simüle edebilir.
    /// 
    /// Üst seviye sistem bu detayları bilmez.
    /// </summary>
    Task SendAsync(
        HydronomEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu transport üzerinden gelen HydronomEnvelope mesajlarını asenkron olarak üretir.
    /// 
    /// IAsyncEnumerable kullanmamızın sebebi:
    /// - Transport sürekli mesaj üretebilir.
    /// - Gateway veya Runtime bunu await foreach ile dinleyebilir.
    /// - Bağlantı kopana veya cancellation istenene kadar akış devam edebilir.
    /// 
    /// Örnek kullanım:
    /// await foreach (var envelope in transport.ReceiveAsync(ct))
    /// {
    ///     router.Handle(envelope);
    /// }
    /// </summary>
    IAsyncEnumerable<HydronomEnvelope> ReceiveAsync(
        CancellationToken cancellationToken = default);
}