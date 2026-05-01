namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafında bir HydronomEnvelope için hangi transport/routing davranışının
/// seçileceğini belirleyen basit routing policy sınıfıdır.
/// 
/// Bu sınıf gerçek gönderim yapmaz.
/// Sadece şu soruya cevap verir:
/// "Bu mesaj hangi haberleşme mantığıyla gönderilmeli?"
/// 
/// PDF'deki CommunicationRouter mantığının ilk küçük çekirdeğidir.
/// İleride bu sınıf:
/// - Link quality,
/// - Vehicle available transports,
/// - Payload boyutu,
/// - Telemetry profile,
/// - ACK/retry politikası,
/// - Emergency broadcast,
/// - LoRa/RF/Wi-Fi/Cellular öncelikleri
/// gibi verilerle genişletilecektir.
/// </summary>
public sealed class TransportRoutingPolicy
{
    /// <summary>
    /// Gelen envelope için route kararı üretir.
    /// 
    /// Öncelik sırası:
    /// 1. Emergency mesajlar tüm uygun bağlantılardan yayınlanır.
    /// 2. Envelope içinde TransportHints varsa onlar temel alınır.
    /// 3. MessageType özel kuralları uygulanır.
    /// 4. Hiçbiri yoksa varsayılan TCP/RF route seçilir.
    /// </summary>
    public TransportRouteDecision Decide(HydronomEnvelope envelope)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        if (IsEmergency(envelope))
            return CreateEmergencyDecision(envelope);

        if (HasExplicitHints(envelope.TransportHints))
            return CreateFromHints(envelope);

        return envelope.MessageType switch
        {
            "FleetHeartbeat" => CreateHeartbeatDecision(envelope),
            "FleetCommand" => CreateCommandDecision(envelope),
            "FleetCommandResult" => CreateCommandResultDecision(envelope),
            "TelemetryFrame" => CreateTelemetryDecision(envelope),
            "GroundWorldUpdate" => CreateGroundWorldDecision(envelope),

            _ => CreateDefaultDecision(envelope)
        };
    }

    /// <summary>
    /// Mesajın emergency/acil durum mesajı olup olmadığını belirler.
    /// 
    /// EmergencyStop gibi mesajlar tek bir kanala güvenmemelidir.
    /// Bu yüzden tüm uygun bağlantılardan yayınlanacak şekilde route edilir.
    /// </summary>
    private static bool IsEmergency(HydronomEnvelope envelope)
    {
        return envelope.Priority == MessagePriority.Emergency ||
               string.Equals(envelope.MessageType, "EmergencyStop", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Envelope içindeki TransportHints alanının gerçekten yönlendirici bilgi içerip içermediğini kontrol eder.
    /// </summary>
    private static bool HasExplicitHints(TransportHints hints)
    {
        return hints.BroadcastAllAvailableLinks ||
               hints.RequiresAck ||
               hints.MaxLatency is not null ||
               hints.Preferred.Count > 0 ||
               hints.Fallback.Count > 0;
    }

    /// <summary>
    /// Emergency mesajlar için route kararı üretir.
    /// 
    /// Bu mesajlar:
    /// - Tüm uygun bağlantılardan yayınlanır,
    /// - ACK bekler,
    /// - Çok düşük latency hedefler.
    /// </summary>
    private static TransportRouteDecision CreateEmergencyDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Emergency message must be broadcast over all available links.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.RfModem,
                TransportKind.LoRa,
                TransportKind.WebSocket,
                TransportKind.Cellular,
                TransportKind.Mesh
            },
            FallbackTransports = Array.Empty<TransportKind>(),
            BroadcastAllAvailableLinks = true,
            RequiresAck = true,
            Priority = MessagePriority.Emergency,
            MaxLatency = TimeSpan.FromMilliseconds(250)
        };
    }

    /// <summary>
    /// Envelope içindeki TransportHints bilgisine göre route kararı üretir.
    /// 
    /// Bu en esnek yoldur.
    /// Mesaj kendi preferred/fallback/ack/latency bilgisini taşıyorsa policy bunu dikkate alır.
    /// </summary>
    private static TransportRouteDecision CreateFromHints(HydronomEnvelope envelope)
    {
        var hints = envelope.TransportHints;

        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Route selected from envelope transport hints.",
            PrimaryTransports = hints.Preferred,
            FallbackTransports = hints.Fallback,
            BroadcastAllAvailableLinks = hints.BroadcastAllAvailableLinks,
            RequiresAck = hints.RequiresAck,
            Priority = envelope.Priority,
            MaxLatency = hints.MaxLatency
        };
    }

    /// <summary>
    /// FleetHeartbeat için route kararı üretir.
    /// 
    /// Heartbeat mesajları düzenli gelir.
    /// Bu yüzden düşük/orta bant genişliği yeterlidir.
    /// ACK zorunlu değildir.
    /// </summary>
    private static TransportRouteDecision CreateHeartbeatDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Heartbeat can use normal reliable links with low-bandwidth fallback.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.RfModem
            },
            FallbackTransports = new[]
            {
                TransportKind.LoRa
            },
            BroadcastAllAvailableLinks = false,
            RequiresAck = false,
            Priority = envelope.Priority,
            MaxLatency = TimeSpan.FromSeconds(2)
        };
    }

    /// <summary>
    /// FleetCommand için route kararı üretir.
    /// 
    /// Komut mesajları telemetry'den daha önemlidir.
    /// Genellikle ACK beklemelidir.
    /// </summary>
    private static TransportRouteDecision CreateCommandDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Fleet command should use reliable primary links and require ACK.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.RfModem
            },
            FallbackTransports = new[]
            {
                TransportKind.LoRa,
                TransportKind.Cellular
            },
            BroadcastAllAvailableLinks = false,
            RequiresAck = true,
            Priority = envelope.Priority,
            MaxLatency = TimeSpan.FromSeconds(1)
        };
    }

    /// <summary>
    /// FleetCommandResult için route kararı üretir.
    /// 
    /// Komut sonuçları Ground Station command history için önemlidir.
    /// Fakat çoğu durumda command kadar kritik değildir.
    /// </summary>
    private static TransportRouteDecision CreateCommandResultDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Command result should return over reliable available links.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.RfModem
            },
            FallbackTransports = new[]
            {
                TransportKind.LoRa
            },
            BroadcastAllAvailableLinks = false,
            RequiresAck = false,
            Priority = envelope.Priority,
            MaxLatency = TimeSpan.FromSeconds(2)
        };
    }

    /// <summary>
    /// TelemetryFrame için route kararı üretir.
    /// 
    /// Full telemetry yüksek bant genişliği ister.
    /// Bu yüzden TCP/WebSocket/Cellular gibi kanallar önceliklidir.
    /// LoRa fallback olarak verilmez; çünkü büyük telemetry için uygun değildir.
    /// </summary>
    private static TransportRouteDecision CreateTelemetryDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Telemetry prefers high-bandwidth links.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.WebSocket,
                TransportKind.Cellular
            },
            FallbackTransports = new[]
            {
                TransportKind.RfModem
            },
            BroadcastAllAvailableLinks = false,
            RequiresAck = false,
            Priority = envelope.Priority,
            MaxLatency = TimeSpan.FromSeconds(3)
        };
    }

    /// <summary>
    /// GroundWorldUpdate için route kararı üretir.
    /// 
    /// Ortak dünya modeli ve harita güncellemeleri genellikle daha büyük veri taşır.
    /// Bu yüzden yüksek bant genişlikli kanallar tercih edilir.
    /// </summary>
    private static TransportRouteDecision CreateGroundWorldDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Ground world updates prefer high-bandwidth links.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp,
                TransportKind.WebSocket,
                TransportKind.Cellular
            },
            FallbackTransports = Array.Empty<TransportKind>(),
            BroadcastAllAvailableLinks = false,
            RequiresAck = false,
            Priority = envelope.Priority,
            MaxLatency = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// Özel kuralı olmayan mesajlar için varsayılan route kararı üretir.
    /// </summary>
    private static TransportRouteDecision CreateDefaultDecision(HydronomEnvelope envelope)
    {
        return new TransportRouteDecision
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            Reason = "Default route selected for unknown or generic message type.",
            PrimaryTransports = new[]
            {
                TransportKind.Tcp
            },
            FallbackTransports = new[]
            {
                TransportKind.RfModem,
                TransportKind.LoRa
            },
            BroadcastAllAvailableLinks = false,
            RequiresAck = envelope.TransportHints.RequiresAck,
            Priority = envelope.Priority,
            MaxLatency = envelope.TransportHints.MaxLatency
        };
    }
}