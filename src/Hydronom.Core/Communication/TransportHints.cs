namespace Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom mesajının hangi haberleşme kanallarından gönderilmesinin tercih edildiğini
/// ve gönderim davranışının nasıl olması gerektiğini tarif eder.
/// 
/// Bu sınıf, HydronomEnvelope içinde kullanılır.
/// Yani mesajın kendisi şunu söyleyebilir:
/// - Ben tercihen Wi-Fi/TCP ile gitmek istiyorum.
/// - Bağlantı kötüleşirse LoRa/RF fallback olabilir.
/// - Bu mesaj ACK beklemeli.
/// - Bu mesaj tüm uygun kanallardan yayınlanmalı.
/// 
/// Böylece CommunicationRouter mesajı alıp en uygun transport'u seçebilir.
/// </summary>
public sealed record TransportHints
{
    /// <summary>
    /// Mesajın gönderilmesi için tercih edilen transport türleri.
    /// 
    /// Örnek:
    /// - Full telemetry için: Tcp, WebSocket, Cellular
    /// - Light telemetry için: LoRa, RfModem
    /// - MissionCommand için: Tcp, RfModem, LoRa
    /// 
    /// CommunicationRouter bu listeyi ilk tercih olarak değerlendirir.
    /// </summary>
    public IReadOnlyList<TransportKind> Preferred { get; init; } = Array.Empty<TransportKind>();

    /// <summary>
    /// Tercih edilen kanallar kullanılamazsa denenebilecek yedek transport türleri.
    /// 
    /// Örnek:
    /// - Tcp yoksa RfModem
    /// - RfModem yoksa LoRa
    /// - WebSocket yoksa Tcp
    /// 
    /// Bu alan, Hydronom'un plug-and-play haberleşme felsefesi için önemlidir.
    /// </summary>
    public IReadOnlyList<TransportKind> Fallback { get; init; } = Array.Empty<TransportKind>();

    /// <summary>
    /// Mesajın alıcı tarafından onaylanması gerekip gerekmediğini belirtir.
    /// 
    /// true ise:
    /// - Alıcı mesajı aldığını ACK ile bildirmelidir.
    /// - İleride CommunicationRouter tekrar gönderim / timeout mantığı uygulayabilir.
    /// 
    /// Kullanım örnekleri:
    /// - EmergencyStop: true
    /// - MissionCommand: true
    /// - Heartbeat: genelde false
    /// - Debug telemetry: false
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// Mesajın mümkün olan tüm uygun bağlantılardan yayınlanıp yayınlanmayacağını belirtir.
    /// 
    /// true ise CommunicationRouter tek bir transport seçmek yerine
    /// mesajı kullanılabilir tüm uygun kanallardan göndermeye çalışabilir.
    /// 
    /// Kullanım örnekleri:
    /// - EmergencyStop
    /// - Critical safety broadcast
    /// - Filo genel uyarıları
    /// 
    /// Normal telemetry ve standart komutlarda genellikle false kalır.
    /// </summary>
    public bool BroadcastAllAvailableLinks { get; init; }

    /// <summary>
    /// Mesajın taşınması için önerilen maksimum gecikme süresi.
    /// 
    /// Örnek:
    /// - EmergencyStop için çok düşük olmalı.
    /// - Telemetry için daha esnek olabilir.
    /// - Uzun analiz mesajlarında daha yüksek olabilir.
    /// 
    /// Bu alan şimdilik sadece metadata olarak kullanılır.
    /// İleride routing policy ve QoS kararlarında kullanılabilir.
    /// </summary>
    public TimeSpan? MaxLatency { get; init; }

    /// <summary>
    /// Varsayılan boş transport hint'i.
    /// 
    /// Mesaj özel bir transport tercihi belirtmiyorsa kullanılabilir.
    /// CommunicationRouter bu durumda kendi varsayılan politikasına göre karar verir.
    /// </summary>
    public static TransportHints None { get; } = new();
}