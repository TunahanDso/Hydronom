namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// Bir HydronomEnvelope mesajının hangi transport davranışıyla gönderilmesi gerektiğini
/// tarif eden routing karar modelidir.
/// 
/// Bu sınıf gerçek gönderim yapmaz.
/// Sadece CommunicationRouter / TransportManager için karar üretir.
/// 
/// Örnek:
/// - EmergencyStop mesajı tüm bağlantılardan yayınlanmalı.
/// - Full telemetry yüksek bant genişlikli bağlantıdan gitmeli.
/// - Heartbeat düşük bant genişlikli güvenilir kanaldan gidebilir.
/// - LoRa varsa sadece küçük/light mesajlar seçilmeli.
/// </summary>
public sealed record TransportRouteDecision
{
    /// <summary>
    /// Kararın hangi mesaj için üretildiğini belirtir.
    /// 
    /// HydronomEnvelope.MessageId ile eşleşir.
    /// Loglama, debugging ve replay için kullanılır.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın mantıksal tipi.
    /// 
    /// Örnekler:
    /// - FleetHeartbeat
    /// - FleetCommand
    /// - FleetCommandResult
    /// - EmergencyStop
    /// - TelemetryFrame
    /// - GroundWorldUpdate
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Routing kararının açıklaması.
    /// 
    /// Hydronom Ops diagnostics ekranında veya loglarda gösterilebilir.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın gönderilmesi için seçilen birincil transport türleri.
    /// 
    /// CommunicationRouter bu listeyi sırayla deneyebilir.
    /// Örneğin:
    /// - Tcp, WebSocket
    /// - RfModem, LoRa
    /// - Cellular, Tcp
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Birincil transport'lar kullanılamazsa denenebilecek yedek transport türleri.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Mesajın mümkün olan tüm uygun bağlantılardan gönderilip gönderilmeyeceğini belirtir.
    /// 
    /// true ise CommunicationRouter tek kanal seçmek yerine
    /// tüm kullanılabilir kanallardan yayınlamaya çalışabilir.
    /// 
    /// Özellikle:
    /// - EmergencyStop
    /// - Critical safety broadcast
    /// - Filo genel güvenlik uyarıları
    /// için önemlidir.
    /// </summary>
    public bool BroadcastAllAvailableLinks { get; init; }

    /// <summary>
    /// Mesaj için ACK beklenip beklenmediğini belirtir.
    /// 
    /// true ise:
    /// - Alıcıdan onay beklenir.
    /// - Timeout / retry politikası uygulanabilir.
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// Mesajın öncelik seviyesi.
    /// 
    /// Route kararında envelope priority'si korunur.
    /// CommunicationRouter ileride bu bilgiyle queue sıralaması yapabilir.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Mesaj için önerilen maksimum gecikme.
    /// 
    /// EmergencyStop gibi kritik komutlarda düşük olmalıdır.
    /// Telemetry/diagnostic gibi mesajlarda daha esnek olabilir.
    /// </summary>
    public TimeSpan? MaxLatency { get; init; }

    /// <summary>
    /// Kararın temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından MessageId, MessageType ve bir transport davranışı belirlenmiş olmalıdır.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(MessageId) &&
        !string.IsNullOrWhiteSpace(MessageType) &&
        (BroadcastAllAvailableLinks ||
         PrimaryTransports.Count > 0 ||
         FallbackTransports.Count > 0);
}