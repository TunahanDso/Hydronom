namespace Hydronom.Core.Communication;

using Hydronom.Core.Fleet;

/// <summary>
/// HydronomEnvelope Ã¼retimini standartlaÅŸtÄ±ran yardÄ±mcÄ± sÄ±nÄ±ftÄ±r.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - Her yerde tekrar tekrar envelope oluÅŸturma kodu yazmayÄ± engellemek.
/// - MessageType, SourceNodeId, TargetNodeId, Priority ve TransportHints alanlarÄ±nÄ±
///   tutarlÄ± ÅŸekilde doldurmak.
/// - FleetHeartbeat, FleetCommand ve FleetCommandResult gibi temel mesajlarÄ±
///   gÃ¼venli ve okunabilir ÅŸekilde zarflamaktÄ±r.
/// 
/// Fleet & Ground Station mimarisinde HydronomEnvelope sistemin ortak mesaj zarfÄ±dÄ±r.
/// Bu factory ise o zarfÄ±n doÄŸru ÅŸekilde Ã¼retilmesini kolaylaÅŸtÄ±rÄ±r.
/// </summary>
public static class HydronomEnvelopeFactory
{
    /// <summary>
    /// FleetHeartbeat payload'Ä± iÃ§in standart HydronomEnvelope Ã¼retir.
    /// 
    /// Heartbeat mesajlarÄ± genellikle araÃ§tan yer istasyonuna gider.
    /// VarsayÄ±lan hedef "GROUND-001" olarak bÄ±rakÄ±lmÄ±ÅŸtÄ±r; istenirse deÄŸiÅŸtirilebilir.
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
    /// FleetCommand payload'Ä± iÃ§in standart HydronomEnvelope Ã¼retir.
    /// 
    /// Komutun priority bilgisi FleetCommand iÃ§inden alÄ±nÄ±r.
    /// Emergency seviyesindeki komutlar iÃ§in transport hint otomatik olarak
    /// tÃ¼m baÄŸlantÄ±lardan yayÄ±nlanacak ÅŸekilde ayarlanÄ±r.
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
    /// FleetCommandResult payload'Ä± iÃ§in standart HydronomEnvelope Ã¼retir.
    /// 
    /// Bu mesaj araÃ§tan yer istasyonuna veya komutu gÃ¶nderen node'a dÃ¶nen cevaptÄ±r.
    /// BaÅŸarÄ±sÄ±z sonuÃ§lar yÃ¼ksek Ã¶ncelikli, baÅŸarÄ±lÄ± sonuÃ§lar normal Ã¶ncelikli gÃ¶nderilir.
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
    /// Genel amaÃ§lÄ± HydronomEnvelope Ã¼retir.
    /// 
    /// Ã–zel mesaj tipleri iÃ§in kullanÄ±labilir.
    /// Ã–rneÄŸin ileride:
    /// - FleetStatus
    /// - TelemetryFrame
    /// - GroundWorldUpdate
    /// - LinkQualityReport
    /// - CapabilityAnnouncement
    /// gibi mesajlar bu metotla hÄ±zlÄ±ca zarflanabilir.
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
