namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// Bir HydronomEnvelope mesajÄ±nÄ±n hangi transport davranÄ±ÅŸÄ±yla gÃ¶nderilmesi gerektiÄŸini
/// tarif eden routing karar modelidir.
/// 
/// Bu sÄ±nÄ±f gerÃ§ek gÃ¶nderim yapmaz.
/// Sadece CommunicationRouter / TransportManager iÃ§in karar Ã¼retir.
/// 
/// Ã–rnek:
/// - EmergencyStop mesajÄ± tÃ¼m baÄŸlantÄ±lardan yayÄ±nlanmalÄ±.
/// - Full telemetry yÃ¼ksek bant geniÅŸlikli baÄŸlantÄ±dan gitmeli.
/// - Heartbeat dÃ¼ÅŸÃ¼k bant geniÅŸlikli gÃ¼venilir kanaldan gidebilir.
/// - LoRa varsa sadece kÃ¼Ã§Ã¼k/light mesajlar seÃ§ilmeli.
/// </summary>
public sealed record TransportRouteDecision
{
    /// <summary>
    /// KararÄ±n hangi mesaj iÃ§in Ã¼retildiÄŸini belirtir.
    /// 
    /// HydronomEnvelope.MessageId ile eÅŸleÅŸir.
    /// Loglama, debugging ve replay iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n mantÄ±ksal tipi.
    /// 
    /// Ã–rnekler:
    /// - FleetHeartbeat
    /// - FleetCommand
    /// - FleetCommandResult
    /// - EmergencyStop
    /// - TelemetryFrame
    /// - GroundWorldUpdate
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Routing kararÄ±nÄ±n aÃ§Ä±klamasÄ±.
    /// 
    /// Hydronom Ops diagnostics ekranÄ±nda veya loglarda gÃ¶sterilebilir.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n gÃ¶nderilmesi iÃ§in seÃ§ilen birincil transport tÃ¼rleri.
    /// 
    /// CommunicationRouter bu listeyi sÄ±rayla deneyebilir.
    /// Ã–rneÄŸin:
    /// - Tcp, WebSocket
    /// - RfModem, LoRa
    /// - Cellular, Tcp
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Birincil transport'lar kullanÄ±lamazsa denenebilecek yedek transport tÃ¼rleri.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// MesajÄ±n mÃ¼mkÃ¼n olan tÃ¼m uygun baÄŸlantÄ±lardan gÃ¶nderilip gÃ¶nderilmeyeceÄŸini belirtir.
    /// 
    /// true ise CommunicationRouter tek kanal seÃ§mek yerine
    /// tÃ¼m kullanÄ±labilir kanallardan yayÄ±nlamaya Ã§alÄ±ÅŸabilir.
    /// 
    /// Ã–zellikle:
    /// - EmergencyStop
    /// - Critical safety broadcast
    /// - Filo genel gÃ¼venlik uyarÄ±larÄ±
    /// iÃ§in Ã¶nemlidir.
    /// </summary>
    public bool BroadcastAllAvailableLinks { get; init; }

    /// <summary>
    /// Mesaj iÃ§in ACK beklenip beklenmediÄŸini belirtir.
    /// 
    /// true ise:
    /// - AlÄ±cÄ±dan onay beklenir.
    /// - Timeout / retry politikasÄ± uygulanabilir.
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// MesajÄ±n Ã¶ncelik seviyesi.
    /// 
    /// Route kararÄ±nda envelope priority'si korunur.
    /// CommunicationRouter ileride bu bilgiyle queue sÄ±ralamasÄ± yapabilir.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Mesaj iÃ§in Ã¶nerilen maksimum gecikme.
    /// 
    /// EmergencyStop gibi kritik komutlarda dÃ¼ÅŸÃ¼k olmalÄ±dÄ±r.
    /// Telemetry/diagnostic gibi mesajlarda daha esnek olabilir.
    /// </summary>
    public TimeSpan? MaxLatency { get; init; }

    /// <summary>
    /// KararÄ±n temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan MessageId, MessageType ve bir transport davranÄ±ÅŸÄ± belirlenmiÅŸ olmalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(MessageId) &&
        !string.IsNullOrWhiteSpace(MessageType) &&
        (BroadcastAllAvailableLinks ||
         PrimaryTransports.Count > 0 ||
         FallbackTransports.Count > 0);
}
