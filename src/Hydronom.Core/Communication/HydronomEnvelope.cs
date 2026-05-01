namespace Hydronom.Core.Communication;

/// <summary>
/// Hydronom sisteminde taşınan tüm üst seviye mesajların ortak zarfıdır.
/// 
/// Fleet & Ground Station mimarisinde mesajın TCP, WebSocket, LoRa, RF modem,
/// Serial veya başka bir transport üzerinden taşınması üst seviye sistemi ilgilendirmemelidir.
/// 
/// Bu sınıfın amacı:
/// - Mesajın kimden geldiğini belirtmek.
/// - Mesajın kime gittiğini belirtmek.
/// - Mesaj tipini standartlaştırmak.
/// - Öncelik bilgisini taşımak.
/// - Transport tercihlerini belirtmek.
/// - Gerçek mesaj içeriğini Payload içinde taşımaktır.
/// 
/// Böylece Hydronom mimarisinde şu prensip korunur:
/// "Hydronom mesaj üretir, transport katmanı mesajı taşır."
/// </summary>
public sealed record HydronomEnvelope
{
    /// <summary>
    /// Mesaj zarfı şema adı.
    /// 
    /// Bu alan ileride farklı envelope sürümleri oluşursa geriye dönük uyumluluk
    /// ve mesaj doğrulama için kullanılabilir.
    /// 
    /// Örnek:
    /// "hydronom.envelope.v1"
    /// </summary>
    public string Schema { get; init; } = "hydronom.envelope.v1";

    /// <summary>
    /// Mesajın benzersiz kimliği.
    /// 
    /// ACK, tekrar gönderim, loglama, replay ve debugging işlemlerinde kullanılır.
    /// 
    /// Varsayılan olarak GUID tabanlı üretilir.
    /// İleride yarışma/operasyon logları için daha okunabilir ID formatı da eklenebilir.
    /// </summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Mesajı üreten düğümün kimliği.
    /// 
    /// Örnekler:
    /// - "VEHICLE-ALPHA"
    /// - "VEHICLE-BETA"
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// 
    /// Bu alan FleetRegistry ve güvenlik/yetki kontrolü için kritiktir.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın hedef düğüm kimliği.
    /// 
    /// Örnekler:
    /// - Belirli bir araç için: "VEHICLE-ALPHA"
    /// - Yer istasyonu için: "GROUND-001"
    /// - Tüm filo için: "BROADCAST"
    /// 
    /// Broadcast mesajları ileride CommunicationRouter tarafından çoklu hedefe yönlendirilebilir.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın mantıksal tipi.
    /// 
    /// Örnekler:
    /// - "FleetHeartbeat"
    /// - "FleetStatus"
    /// - "MissionCommand"
    /// - "CommandResult"
    /// - "EmergencyStop"
    /// - "TelemetryFrame"
    /// 
    /// Bu alan, alıcı tarafta payload'ın hangi modele parse edileceğini belirlemek için kullanılır.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın öncelik seviyesi.
    /// 
    /// CommunicationRouter bu bilgiye göre mesajı sıraya alabilir,
    /// kritik mesajları tüm kanallardan yayınlayabilir veya ACK zorunluluğu getirebilir.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Mesajın oluşturulduğu UTC zaman damgası.
    /// 
    /// Kullanım alanları:
    /// - Eski mesajları reddetme
    /// - Replay attack kontrolü
    /// - Telemetry sıralama
    /// - Gecikme ölçümü
    /// - Log/replay sistemi
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Mesajın hangi transport tercihleriyle gönderilmesi gerektiğini belirtir.
    /// 
    /// Örneğin EmergencyStop mesajı tüm bağlantılardan yayınlanmak isteyebilir.
    /// Full telemetry ise yüksek bant genişlikli bağlantıları tercih edebilir.
    /// </summary>
    public TransportHints TransportHints { get; init; } = TransportHints.None;

    /// <summary>
    /// Mesajın gerçek içeriği.
    /// 
    /// Not:
    /// Bu alanı object olarak bırakıyoruz çünkü HydronomEnvelope farklı mesaj türlerini
    /// tek ortak zarf içinde taşıyacak.
    /// 
    /// Örnek payload modelleri:
    /// - FleetHeartbeat
    /// - FleetCommand
    /// - FleetCommandResult
    /// - VehicleNodeStatus
    /// 
    /// İleride JSON serialization tarafında type-safe yardımcı metotlar eklenebilir.
    /// </summary>
    public object? Payload { get; init; }

    /// <summary>
    /// Mesajın broadcast olup olmadığını hızlı kontrol etmek için yardımcı özellik.
    /// 
    /// TargetNodeId alanı "BROADCAST" ise bu mesaj tüm uygun düğümlere gönderilebilir.
    /// </summary>
    public bool IsBroadcast =>
        string.Equals(TargetNodeId, "BROADCAST", StringComparison.OrdinalIgnoreCase);
}