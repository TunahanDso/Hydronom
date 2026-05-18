using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Telemetry;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Pipeline;

public sealed record HydronomOutgoingPacket
{
    public bool ShouldSend { get; init; }

    public string Reason { get; init; } = "";

    public CompactTelemetryFrame? TelemetryFrame { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public HydronomEncodedMessage EncodedMessage { get; init; } = HydronomEncodedMessage.Empty;

    public int PayloadBytes => Envelope?.Payload.Length ?? 0;

    public int PacketBytes => EncodedMessage.SizeBytes;

    public static HydronomOutgoingPacket Skipped(string reason)
    {
        return new HydronomOutgoingPacket
        {
            ShouldSend = false,
            Reason = reason
        };
    }

    public static HydronomOutgoingPacket Ready(
        CompactTelemetryFrame frame,
        CommunicationEnvelope envelope,
        HydronomEncodedMessage encodedMessage,
        string reason)
    {
        return new HydronomOutgoingPacket
        {
            ShouldSend = true,
            Reason = reason,
            TelemetryFrame = frame,
            Envelope = envelope,
            EncodedMessage = encodedMessage
        };
    }
}