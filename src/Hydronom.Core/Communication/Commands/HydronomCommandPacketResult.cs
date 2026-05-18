using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Commands;

public sealed record HydronomCommandPacketResult
{
    public bool Accepted { get; init; }

    public string Reason { get; init; } = "";

    public HydronomCommandFrame? Command { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public HydronomEncodedMessage EncodedMessage { get; init; } = HydronomEncodedMessage.Empty;

    public HydronomSecurityResult? SecurityResult { get; init; }

    public int PacketBytes => EncodedMessage.SizeBytes;

    public int PayloadBytes => Envelope?.Payload.Length ?? 0;

    public static HydronomCommandPacketResult Ready(
        HydronomCommandFrame command,
        CommunicationEnvelope envelope,
        HydronomEncodedMessage encodedMessage)
    {
        return new HydronomCommandPacketResult
        {
            Accepted = true,
            Reason = "COMMAND_PACKET_READY",
            Command = command,
            Envelope = envelope,
            EncodedMessage = encodedMessage
        };
    }

    public static HydronomCommandPacketResult Accept(
        HydronomCommandFrame command,
        CommunicationEnvelope envelope,
        HydronomSecurityResult? securityResult)
    {
        return new HydronomCommandPacketResult
        {
            Accepted = true,
            Reason = "COMMAND_ACCEPTED",
            Command = command,
            Envelope = envelope,
            SecurityResult = securityResult
        };
    }

    public static HydronomCommandPacketResult Reject(
        string reason,
        CommunicationEnvelope? envelope = null,
        HydronomSecurityResult? securityResult = null)
    {
        return new HydronomCommandPacketResult
        {
            Accepted = false,
            Reason = reason,
            Envelope = envelope,
            SecurityResult = securityResult
        };
    }
}