namespace Hydronom.Core.Communication;

using Hydronom.Core.Fleet;

/// <summary>
/// HydronomEnvelope üretimini standartlaştıran yardımcı sınıftır.
/// 
/// Bu sınıfın amacı:
/// - Her yerde tekrar tekrar envelope oluşturma kodu yazmayı engellemek.
/// - MessageType, SourceNodeId, TargetNodeId, Priority ve TransportHints alanlarını
///   tutarlı şekilde doldurmak.
/// - FleetHeartbeat, FleetCommand ve FleetCommandResult gibi temel mesajları
///   güvenli ve okunabilir şekilde zarflamaktır.
/// 
/// Fleet & Ground Station mimarisinde HydronomEnvelope sistemin ortak mesaj zarfıdır.
/// Bu factory ise o zarfın doğru şekilde üretilmesini kolaylaştırır.
/// </summary>
public static class HydronomEnvelopeFactory
{
    /// <summary>
    /// FleetHeartbeat payload'ı için standart HydronomEnvelope üretir.
    /// 
    /// Heartbeat mesajları genellikle araçtan yer istasyonuna gider.
    /// Varsayılan hedef "GROUND-001" olarak bırakılmıştır; istenirse değiştirilebilir.
    /// </summary>
    public static HydronomEnvelope CreateHeartbeat(
        FleetHeartbeat heartbeat,
        string targetNodeId = "GROUND-001")
    {
        return new HydronomEnvelope
        {
            SourceNodeId = heartbeat.Identity.NodeId,
            TargetNodeId = targetNodeId,
            MessageType = "FleetHeartbeat",
            Priority = MessagePriority.Normal,
            TransportHints = new TransportHints
            {
                Preferred = new[] { TransportKind.Tcp, TransportKind.RfModem },
                Fallback = new[] { TransportKind.LoRa },
                RequiresAck = false
            },
            Payload = heartbeat
        };
    }

    /// <summary>
    /// FleetCommand payload'ı için standart HydronomEnvelope üretir.
    /// 
    /// Komutun priority bilgisi FleetCommand içinden alınır.
    /// Emergency seviyesindeki komutlar için transport hint otomatik olarak
    /// tüm bağlantılardan yayınlanacak şekilde ayarlanır.
    /// </summary>
    public static HydronomEnvelope CreateCommand(FleetCommand command)
    {
        var isEmergency = command.Priority == MessagePriority.Emergency ||
                          string.Equals(command.CommandType, "EmergencyStop", StringComparison.OrdinalIgnoreCase);

        return new HydronomEnvelope
        {
            SourceNodeId = command.SourceNodeId,
            TargetNodeId = command.TargetNodeId,
            MessageType = "FleetCommand",
            Priority = command.Priority,
            TransportHints = isEmergency
                ? new TransportHints
                {
                    Preferred = new[]
                    {
                        TransportKind.Tcp,
                        TransportKind.RfModem,
                        TransportKind.LoRa,
                        TransportKind.WebSocket
                    },
                    BroadcastAllAvailableLinks = true,
                    RequiresAck = true,
                    MaxLatency = TimeSpan.FromMilliseconds(250)
                }
                : new TransportHints
                {
                    Preferred = new[] { TransportKind.Tcp, TransportKind.RfModem },
                    Fallback = new[] { TransportKind.LoRa },
                    RequiresAck = command.RequiresResult
                },
            Payload = command
        };
    }

    /// <summary>
    /// FleetCommandResult payload'ı için standart HydronomEnvelope üretir.
    /// 
    /// Bu mesaj araçtan yer istasyonuna veya komutu gönderen node'a dönen cevaptır.
    /// Başarısız sonuçlar yüksek öncelikli, başarılı sonuçlar normal öncelikli gönderilir.
    /// </summary>
    public static HydronomEnvelope CreateCommandResult(FleetCommandResult result)
    {
        return new HydronomEnvelope
        {
            SourceNodeId = result.SourceNodeId,
            TargetNodeId = result.TargetNodeId,
            MessageType = "FleetCommandResult",
            Priority = result.Success ? MessagePriority.Normal : MessagePriority.High,
            TransportHints = new TransportHints
            {
                Preferred = new[] { TransportKind.Tcp, TransportKind.RfModem },
                Fallback = new[] { TransportKind.LoRa },
                RequiresAck = false
            },
            Payload = result
        };
    }

    /// <summary>
    /// Genel amaçlı HydronomEnvelope üretir.
    /// 
    /// Özel mesaj tipleri için kullanılabilir.
    /// Örneğin ileride:
    /// - FleetStatus
    /// - TelemetryFrame
    /// - GroundWorldUpdate
    /// - LinkQualityReport
    /// - CapabilityAnnouncement
    /// gibi mesajlar bu metotla hızlıca zarflanabilir.
    /// </summary>
    public static HydronomEnvelope Create(
        string sourceNodeId,
        string targetNodeId,
        string messageType,
        object? payload,
        MessagePriority priority = MessagePriority.Normal,
        TransportHints? transportHints = null)
    {
        return new HydronomEnvelope
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            MessageType = messageType,
            Priority = priority,
            TransportHints = transportHints ?? TransportHints.None,
            Payload = payload
        };
    }
}