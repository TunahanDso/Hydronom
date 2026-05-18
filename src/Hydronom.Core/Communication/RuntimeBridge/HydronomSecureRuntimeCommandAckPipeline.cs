using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed class HydronomSecureRuntimeCommandAckPipeline
{
    private readonly HydronomRuntimeCommandAckEnvelopeAdapter _adapter = new();
    private readonly BinaryHydronomCodec _binaryCodec = new();
    private readonly HmacHydronomSecurityProvider _securityProvider;
    private readonly HydronomSecurityProfile _securityProfile;
    private readonly bool _enableSecurity;
    private readonly string _sessionId;

    public HydronomSecureRuntimeCommandAckPipeline(
        string hmacSecretKey,
        HydronomSecurityProfile? securityProfile = null,
        bool enableSecurity = true,
        string sessionId = "")
    {
        _securityProvider = new HmacHydronomSecurityProvider(hmacSecretKey);
        _securityProfile = securityProfile ?? HydronomSecurityProfile.Race;
        _enableSecurity = enableSecurity;
        _sessionId = sessionId;
    }

    public HydronomRuntimeCommandAckPacket BuildOutgoingAckPacket(
        HydronomRuntimeCommandAck ack,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(ack);

        var envelope = _adapter.ToEnvelope(
            ack,
            sessionId: _sessionId,
            correlationId: correlationId ?? ack.CommandId);

        var protectedEnvelope = _enableSecurity
            ? _securityProvider.Protect(envelope, _securityProfile)
            : envelope;

        var encoded = _binaryCodec.Encode(protectedEnvelope);

        return HydronomRuntimeCommandAckPacket.Ready(
            ack,
            protectedEnvelope,
            encoded);
    }

    public HydronomRuntimeCommandAckReceiveResult ReadIncomingAckPacket(
        byte[] packetBytes)
    {
        ArgumentNullException.ThrowIfNull(packetBytes);

        CommunicationEnvelope envelope;

        try
        {
            envelope = _binaryCodec.Decode(new HydronomEncodedMessage
            {
                Type = HydronomMessageType.Unknown,
                Priority = HydronomMessagePriority.Normal,
                Flags = HydronomMessageFlags.None,
                Bytes = packetBytes,
                CodecName = _binaryCodec.CodecName
            });
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            return HydronomRuntimeCommandAckReceiveResult.Reject(
                $"BINARY_DECODE_FAILED: {ex.Message}");
        }

        HydronomSecurityResult? securityResult = null;

        if (_enableSecurity)
        {
            securityResult = _securityProvider.Verify(
                envelope,
                _securityProfile);

            if (!securityResult.Accepted)
            {
                return HydronomRuntimeCommandAckReceiveResult.Reject(
                    securityResult.Reason,
                    envelope,
                    securityResult);
            }
        }

        HydronomRuntimeCommandAck ack;

        try
        {
            ack = _adapter.FromEnvelope(envelope);
        }
        catch (InvalidDataException ex)
        {
            return HydronomRuntimeCommandAckReceiveResult.Reject(
                $"ACK_ENVELOPE_INVALID: {ex.Message}",
                envelope,
                securityResult);
        }

        return HydronomRuntimeCommandAckReceiveResult.Accept(
            ack,
            envelope,
            securityResult);
    }
}

public sealed record HydronomRuntimeCommandAckPacket
{
    public bool ReadyToSend { get; init; }

    public string Reason { get; init; } = "";

    public HydronomRuntimeCommandAck? Ack { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public HydronomEncodedMessage EncodedMessage { get; init; } =
        HydronomEncodedMessage.Empty;

    public int PayloadBytes => Envelope?.Payload.Length ?? 0;

    public int PacketBytes => EncodedMessage.SizeBytes;

    public static HydronomRuntimeCommandAckPacket Ready(
        HydronomRuntimeCommandAck ack,
        CommunicationEnvelope envelope,
        HydronomEncodedMessage encodedMessage)
    {
        return new HydronomRuntimeCommandAckPacket
        {
            ReadyToSend = true,
            Reason = "RUNTIME_COMMAND_ACK_PACKET_READY",
            Ack = ack,
            Envelope = envelope,
            EncodedMessage = encodedMessage
        };
    }
}

public sealed record HydronomRuntimeCommandAckReceiveResult
{
    public bool Accepted { get; init; }

    public string Reason { get; init; } = "";

    public HydronomRuntimeCommandAck? Ack { get; init; }

    public CommunicationEnvelope? Envelope { get; init; }

    public HydronomSecurityResult? SecurityResult { get; init; }

    public static HydronomRuntimeCommandAckReceiveResult Accept(
        HydronomRuntimeCommandAck ack,
        CommunicationEnvelope envelope,
        HydronomSecurityResult? securityResult)
    {
        return new HydronomRuntimeCommandAckReceiveResult
        {
            Accepted = true,
            Reason = "RUNTIME_COMMAND_ACK_ACCEPTED",
            Ack = ack,
            Envelope = envelope,
            SecurityResult = securityResult
        };
    }

    public static HydronomRuntimeCommandAckReceiveResult Reject(
        string reason,
        CommunicationEnvelope? envelope = null,
        HydronomSecurityResult? securityResult = null)
    {
        return new HydronomRuntimeCommandAckReceiveResult
        {
            Accepted = false,
            Reason = reason,
            Envelope = envelope,
            SecurityResult = securityResult
        };
    }
}