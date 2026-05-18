using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Pipeline;

public sealed record HydronomIncomingPacket
{
    public bool Accepted { get; init; }

    public string Reason { get; init; } = "";

    public HydronomSecurityResult? SecurityResult { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public CompactTelemetryFrame? TelemetryFrame { get; init; }

    public static HydronomIncomingPacket Reject(
        string reason,
        HydronomSecurityResult? securityResult = null,
        CommunicationEnvelope? envelope = null)
    {
        return new HydronomIncomingPacket
        {
            Accepted = false,
            Reason = reason,
            SecurityResult = securityResult,
            Envelope = envelope
        };
    }

    public static HydronomIncomingPacket Accept(
        CommunicationEnvelope envelope,
        CompactTelemetryFrame telemetryFrame,
        HydronomSecurityResult? securityResult)
    {
        return new HydronomIncomingPacket
        {
            Accepted = true,
            Reason = "ACCEPTED",
            SecurityResult = securityResult,
            Envelope = envelope,
            TelemetryFrame = telemetryFrame
        };
    }
}