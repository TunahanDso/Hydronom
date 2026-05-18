using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Commands;

public sealed class HydronomSecureCommandPipeline
{
    private readonly HydronomCommandEnvelopeAdapter _adapter = new();
    private readonly BinaryHydronomCodec _binaryCodec = new();
    private readonly HmacHydronomSecurityProvider _securityProvider;
    private readonly HydronomSecurityProfile _securityProfile;
    private readonly bool _enableSecurity;
    private readonly string _sessionId;

    public HydronomSecureCommandPipeline(
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

    public HydronomCommandPacketResult BuildOutgoingCommandPacket(
        HydronomCommandFrame command,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        var envelope = _adapter.ToEnvelope(
            command,
            sessionId: _sessionId,
            correlationId: correlationId ?? command.CommandId);

        var protectedEnvelope = _enableSecurity
            ? _securityProvider.Protect(envelope, _securityProfile)
            : envelope;

        var encoded = _binaryCodec.Encode(protectedEnvelope);

        return HydronomCommandPacketResult.Ready(
            command,
            protectedEnvelope,
            encoded);
    }

    public HydronomCommandPacketResult ReadIncomingCommandPacket(byte[] packetBytes)
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
            return HydronomCommandPacketResult.Reject(
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
                return HydronomCommandPacketResult.Reject(
                    securityResult.Reason,
                    envelope,
                    securityResult);
            }
        }

        HydronomCommandFrame command;

        try
        {
            command = _adapter.FromEnvelope(envelope);
        }
        catch (InvalidDataException ex)
        {
            return HydronomCommandPacketResult.Reject(
                $"COMMAND_ENVELOPE_INVALID: {ex.Message}",
                envelope,
                securityResult);
        }

        return HydronomCommandPacketResult.Accept(
            command,
            envelope,
            securityResult);
    }
}