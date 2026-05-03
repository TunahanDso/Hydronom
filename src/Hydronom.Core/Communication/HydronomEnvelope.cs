namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom sisteminde taÅŸÄ±nan tÃ¼m Ã¼st seviye mesajlarÄ±n ortak zarfÄ±dÄ±r.
/// 
/// Fleet & Ground Station mimarisinde mesajÄ±n TCP, WebSocket, LoRa, RF modem,
/// Serial veya baÅŸka bir transport Ã¼zerinden taÅŸÄ±nmasÄ± Ã¼st seviye sistemi ilgilendirmemelidir.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - MesajÄ±n kimden geldiÄŸini belirtmek.
/// - MesajÄ±n kime gittiÄŸini belirtmek.
/// - Mesaj tipini standartlaÅŸtÄ±rmak.
/// - Ã–ncelik bilgisini taÅŸÄ±mak.
/// - Transport tercihlerini belirtmek.
/// - GerÃ§ek mesaj iÃ§eriÄŸini Payload iÃ§inde taÅŸÄ±maktÄ±r.
/// 
/// BÃ¶ylece Hydronom mimarisinde ÅŸu prensip korunur:
/// "Hydronom mesaj Ã¼retir, transport katmanÄ± mesajÄ± taÅŸÄ±r."
/// </summary>
public sealed record HydronomEnvelope
{
    /// <summary>
    /// Mesaj zarfÄ± ÅŸema adÄ±.
    /// 
    /// Bu alan ileride farklÄ± envelope sÃ¼rÃ¼mleri oluÅŸursa geriye dÃ¶nÃ¼k uyumluluk
    /// ve mesaj doÄŸrulama iÃ§in kullanÄ±labilir.
    /// 
    /// Ã–rnek:
    /// "hydronom.envelope.v1"
    /// </summary>
    public string Schema { get; init; } = "hydronom.envelope.v1";

    /// <summary>
    /// MesajÄ±n benzersiz kimliÄŸi.
    /// 
    /// ACK, tekrar gÃ¶nderim, loglama, replay ve debugging iÅŸlemlerinde kullanÄ±lÄ±r.
    /// 
    /// VarsayÄ±lan olarak GUID tabanlÄ± Ã¼retilir.
    /// Ä°leride yarÄ±ÅŸma/operasyon loglarÄ± iÃ§in daha okunabilir ID formatÄ± da eklenebilir.
    /// </summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// MesajÄ± Ã¼reten dÃ¼ÄŸÃ¼mÃ¼n kimliÄŸi.
    /// 
    /// Ã–rnekler:
    /// - "VEHICLE-ALPHA"
    /// - "VEHICLE-BETA"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// 
    /// Bu alan FleetRegistry ve gÃ¼venlik/yetki kontrolÃ¼ iÃ§in kritiktir.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n hedef dÃ¼ÄŸÃ¼m kimliÄŸi.
    /// 
    /// Ã–rnekler:
    /// - Belirli bir araÃ§ iÃ§in: "VEHICLE-ALPHA"
    /// - Yer istasyonu iÃ§in: "GROUND-001"
    /// - TÃ¼m filo iÃ§in: "BROADCAST"
    /// 
    /// Broadcast mesajlarÄ± ileride CommunicationRouter tarafÄ±ndan Ã§oklu hedefe yÃ¶nlendirilebilir.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n mantÄ±ksal tipi.
    /// 
    /// Ã–rnekler:
    /// - "FleetHeartbeat"
    /// - "FleetStatus"
    /// - "MissionCommand"
    /// - "CommandResult"
    /// - "EmergencyStop"
    /// - "TelemetryFrame"
    /// 
    /// Bu alan, alÄ±cÄ± tarafta payload'Ä±n hangi modele parse edileceÄŸini belirlemek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n Ã¶ncelik seviyesi.
    /// 
    /// CommunicationRouter bu bilgiye gÃ¶re mesajÄ± sÄ±raya alabilir,
    /// kritik mesajlarÄ± tÃ¼m kanallardan yayÄ±nlayabilir veya ACK zorunluluÄŸu getirebilir.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// MesajÄ±n oluÅŸturulduÄŸu UTC zaman damgasÄ±.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Eski mesajlarÄ± reddetme
    /// - Replay attack kontrolÃ¼
    /// - Telemetry sÄ±ralama
    /// - Gecikme Ã¶lÃ§Ã¼mÃ¼
    /// - Log/replay sistemi
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// MesajÄ±n hangi transport tercihleriyle gÃ¶nderilmesi gerektiÄŸini belirtir.
    /// 
    /// Ã–rneÄŸin EmergencyStop mesajÄ± tÃ¼m baÄŸlantÄ±lardan yayÄ±nlanmak isteyebilir.
    /// Full telemetry ise yÃ¼ksek bant geniÅŸlikli baÄŸlantÄ±larÄ± tercih edebilir.
    /// </summary>
    public TransportHints TransportHints { get; init; } = TransportHints.None;

    /// <summary>
    /// MesajÄ±n gerÃ§ek iÃ§eriÄŸi.
    /// 
    /// Not:
    /// Bu alanÄ± object olarak bÄ±rakÄ±yoruz Ã§Ã¼nkÃ¼ HydronomEnvelope farklÄ± mesaj tÃ¼rlerini
    /// tek ortak zarf iÃ§inde taÅŸÄ±yacak.
    /// 
    /// Ã–rnek payload modelleri:
    /// - FleetHeartbeat
    /// - FleetCommand
    /// - FleetCommandResult
    /// - VehicleNodeStatus
    /// 
    /// Ä°leride JSON serialization tarafÄ±nda type-safe yardÄ±mcÄ± metotlar eklenebilir.
    /// </summary>
    public object? Payload { get; init; }

    /// <summary>
    /// MesajÄ±n broadcast olup olmadÄ±ÄŸÄ±nÄ± hÄ±zlÄ± kontrol etmek iÃ§in yardÄ±mcÄ± Ã¶zellik.
    /// 
    /// TargetNodeId alanÄ± "BROADCAST" ise bu mesaj tÃ¼m uygun dÃ¼ÄŸÃ¼mlere gÃ¶nderilebilir.
    /// </summary>
    public bool IsBroadcast =>
        string.Equals(TargetNodeId, "BROADCAST", StringComparison.OrdinalIgnoreCase);
}
