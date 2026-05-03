namespace Hydronom.Core.Communication;

/// <summary>
/// Bir Hydronom mesajÄ±nÄ±n hangi haberleÅŸme kanallarÄ±ndan gÃ¶nderilmesinin tercih edildiÄŸini
/// ve gÃ¶nderim davranÄ±ÅŸÄ±nÄ±n nasÄ±l olmasÄ± gerektiÄŸini tarif eder.
/// 
/// Bu sÄ±nÄ±f, HydronomEnvelope iÃ§inde kullanÄ±lÄ±r.
/// Yani mesajÄ±n kendisi ÅŸunu sÃ¶yleyebilir:
/// - Ben tercihen Wi-Fi/TCP ile gitmek istiyorum.
/// - BaÄŸlantÄ± kÃ¶tÃ¼leÅŸirse LoRa/RF fallback olabilir.
/// - Bu mesaj ACK beklemeli.
/// - Bu mesaj tÃ¼m uygun kanallardan yayÄ±nlanmalÄ±.
/// 
/// BÃ¶ylece CommunicationRouter mesajÄ± alÄ±p en uygun transport'u seÃ§ebilir.
/// </summary>
public sealed record TransportHints
{
    /// <summary>
    /// MesajÄ±n gÃ¶nderilmesi iÃ§in tercih edilen transport tÃ¼rleri.
    /// 
    /// Ã–rnek:
    /// - Full telemetry iÃ§in: Tcp, WebSocket, Cellular
    /// - Light telemetry iÃ§in: LoRa, RfModem
    /// - MissionCommand iÃ§in: Tcp, RfModem, LoRa
    /// 
    /// CommunicationRouter bu listeyi ilk tercih olarak deÄŸerlendirir.
    /// </summary>
    public IReadOnlyList<TransportKind> Preferred { get; init; } = Array.Empty<TransportKind>();

    /// <summary>
    /// Tercih edilen kanallar kullanÄ±lamazsa denenebilecek yedek transport tÃ¼rleri.
    /// 
    /// Ã–rnek:
    /// - Tcp yoksa RfModem
    /// - RfModem yoksa LoRa
    /// - WebSocket yoksa Tcp
    /// 
    /// Bu alan, Hydronom'un plug-and-play haberleÅŸme felsefesi iÃ§in Ã¶nemlidir.
    /// </summary>
    public IReadOnlyList<TransportKind> Fallback { get; init; } = Array.Empty<TransportKind>();

    /// <summary>
    /// MesajÄ±n alÄ±cÄ± tarafÄ±ndan onaylanmasÄ± gerekip gerekmediÄŸini belirtir.
    /// 
    /// true ise:
    /// - AlÄ±cÄ± mesajÄ± aldÄ±ÄŸÄ±nÄ± ACK ile bildirmelidir.
    /// - Ä°leride CommunicationRouter tekrar gÃ¶nderim / timeout mantÄ±ÄŸÄ± uygulayabilir.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - EmergencyStop: true
    /// - MissionCommand: true
    /// - Heartbeat: genelde false
    /// - Debug telemetry: false
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// MesajÄ±n mÃ¼mkÃ¼n olan tÃ¼m uygun baÄŸlantÄ±lardan yayÄ±nlanÄ±p yayÄ±nlanmayacaÄŸÄ±nÄ± belirtir.
    /// 
    /// true ise CommunicationRouter tek bir transport seÃ§mek yerine
    /// mesajÄ± kullanÄ±labilir tÃ¼m uygun kanallardan gÃ¶ndermeye Ã§alÄ±ÅŸabilir.
    /// 
    /// KullanÄ±m Ã¶rnekleri:
    /// - EmergencyStop
    /// - Critical safety broadcast
    /// - Filo genel uyarÄ±larÄ±
    /// 
    /// Normal telemetry ve standart komutlarda genellikle false kalÄ±r.
    /// </summary>
    public bool BroadcastAllAvailableLinks { get; init; }

    /// <summary>
    /// MesajÄ±n taÅŸÄ±nmasÄ± iÃ§in Ã¶nerilen maksimum gecikme sÃ¼resi.
    /// 
    /// Ã–rnek:
    /// - EmergencyStop iÃ§in Ã§ok dÃ¼ÅŸÃ¼k olmalÄ±.
    /// - Telemetry iÃ§in daha esnek olabilir.
    /// - Uzun analiz mesajlarÄ±nda daha yÃ¼ksek olabilir.
    /// 
    /// Bu alan ÅŸimdilik sadece metadata olarak kullanÄ±lÄ±r.
    /// Ä°leride routing policy ve QoS kararlarÄ±nda kullanÄ±labilir.
    /// </summary>
    public TimeSpan? MaxLatency { get; init; }

    /// <summary>
    /// VarsayÄ±lan boÅŸ transport hint'i.
    /// 
    /// Mesaj Ã¶zel bir transport tercihi belirtmiyorsa kullanÄ±labilir.
    /// CommunicationRouter bu durumda kendi varsayÄ±lan politikasÄ±na gÃ¶re karar verir.
    /// </summary>
    public static TransportHints None { get; } = new();
}
