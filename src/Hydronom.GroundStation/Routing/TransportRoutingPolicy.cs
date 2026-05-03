namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafÄ±nda bir HydronomEnvelope iÃ§in hangi transport/routing davranÄ±ÅŸÄ±nÄ±n
/// seÃ§ileceÄŸini belirleyen basit routing policy sÄ±nÄ±fÄ±dÄ±r.
/// 
/// Bu sÄ±nÄ±f gerÃ§ek gÃ¶nderim yapmaz.
/// Sadece ÅŸu soruya cevap verir:
/// "Bu mesaj hangi haberleÅŸme mantÄ±ÄŸÄ±yla gÃ¶nderilmeli?"
/// 
/// PDF'deki CommunicationRouter mantÄ±ÄŸÄ±nÄ±n ilk kÃ¼Ã§Ã¼k Ã§ekirdeÄŸidir.
/// Ä°leride bu sÄ±nÄ±f:
/// - Link quality,
/// - Vehicle available transports,
/// - Payload boyutu,
/// - Telemetry profile,
/// - ACK/retry politikasÄ±,
/// - Emergency broadcast,
/// - LoRa/RF/Wi-Fi/Cellular Ã¶ncelikleri
/// gibi verilerle geniÅŸletilecektir.
/// </summary>
public sealed class TransportRoutingPolicy
{
    /// <summary>
    /// Gelen envelope iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Ã–ncelik sÄ±rasÄ±:
    /// 1. Emergency mesajlar tÃ¼m uygun baÄŸlantÄ±lardan yayÄ±nlanÄ±r.
    /// 2. Envelope iÃ§inde TransportHints varsa onlar temel alÄ±nÄ±r.
    /// 3. MessageType Ã¶zel kurallarÄ± uygulanÄ±r.
    /// 4. HiÃ§biri yoksa varsayÄ±lan TCP/RF route seÃ§ilir.
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
    /// MesajÄ±n emergency/acil durum mesajÄ± olup olmadÄ±ÄŸÄ±nÄ± belirler.
    /// 
    /// EmergencyStop gibi mesajlar tek bir kanala gÃ¼venmemelidir.
    /// Bu yÃ¼zden tÃ¼m uygun baÄŸlantÄ±lardan yayÄ±nlanacak ÅŸekilde route edilir.
    /// </summary>
    private static bool IsEmergency(HydronomEnvelope envelope)
    {
        return envelope.Priority == MessagePriority.Emergency ||
               string.Equals(envelope.MessageType, "EmergencyStop", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Envelope iÃ§indeki TransportHints alanÄ±nÄ±n gerÃ§ekten yÃ¶nlendirici bilgi iÃ§erip iÃ§ermediÄŸini kontrol eder.
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
    /// Emergency mesajlar iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Bu mesajlar:
    /// - TÃ¼m uygun baÄŸlantÄ±lardan yayÄ±nlanÄ±r,
    /// - ACK bekler,
    /// - Ã‡ok dÃ¼ÅŸÃ¼k latency hedefler.
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
    /// Envelope iÃ§indeki TransportHints bilgisine gÃ¶re route kararÄ± Ã¼retir.
    /// 
    /// Bu en esnek yoldur.
    /// Mesaj kendi preferred/fallback/ack/latency bilgisini taÅŸÄ±yorsa policy bunu dikkate alÄ±r.
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
    /// FleetHeartbeat iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Heartbeat mesajlarÄ± dÃ¼zenli gelir.
    /// Bu yÃ¼zden dÃ¼ÅŸÃ¼k/orta bant geniÅŸliÄŸi yeterlidir.
    /// ACK zorunlu deÄŸildir.
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
    /// FleetCommand iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Komut mesajlarÄ± telemetry'den daha Ã¶nemlidir.
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
    /// FleetCommandResult iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Komut sonuÃ§larÄ± Ground Station command history iÃ§in Ã¶nemlidir.
    /// Fakat Ã§oÄŸu durumda command kadar kritik deÄŸildir.
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
    /// TelemetryFrame iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Full telemetry yÃ¼ksek bant geniÅŸliÄŸi ister.
    /// Bu yÃ¼zden TCP/WebSocket/Cellular gibi kanallar Ã¶nceliklidir.
    /// LoRa fallback olarak verilmez; Ã§Ã¼nkÃ¼ bÃ¼yÃ¼k telemetry iÃ§in uygun deÄŸildir.
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
    /// GroundWorldUpdate iÃ§in route kararÄ± Ã¼retir.
    /// 
    /// Ortak dÃ¼nya modeli ve harita gÃ¼ncellemeleri genellikle daha bÃ¼yÃ¼k veri taÅŸÄ±r.
    /// Bu yÃ¼zden yÃ¼ksek bant geniÅŸlikli kanallar tercih edilir.
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
    /// Ã–zel kuralÄ± olmayan mesajlar iÃ§in varsayÄ±lan route kararÄ± Ã¼retir.
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
