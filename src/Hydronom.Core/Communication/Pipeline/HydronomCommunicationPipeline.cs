using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Pipeline;

public sealed class HydronomCommunicationPipeline
{
    private readonly HydronomCommunicationPipelineOptions _options;
    private readonly CompactTelemetryDeltaBuilder _deltaBuilder;
    private readonly CompactTelemetryEnvelopeAdapter _telemetryAdapter;
    private readonly BinaryHydronomCodec _binaryCodec;
    private readonly HmacHydronomSecurityProvider _securityProvider;

    private CompactTelemetryFrame? _lastOutgoingTelemetryFrame;

    public HydronomCommunicationPipeline(HydronomCommunicationPipelineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _deltaBuilder = new CompactTelemetryDeltaBuilder(_options.DeltaOptions);
        _telemetryAdapter = new CompactTelemetryEnvelopeAdapter();
        _binaryCodec = new BinaryHydronomCodec();
        _securityProvider = new HmacHydronomSecurityProvider(_options.HmacSecretKey);
    }

    public HydronomOutgoingPacket BuildOutgoingTelemetryPacket(
        CompactTelemetryFrame currentFrame,
        HydronomMessagePriority priority = HydronomMessagePriority.High,
        HydronomMessageFlags extraFlags = HydronomMessageFlags.None,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(currentFrame);

        var frameToSend = currentFrame;

        if (_options.EnableDeltaTelemetry)
        {
            frameToSend = _deltaBuilder.BuildDelta(
                _lastOutgoingTelemetryFrame,
                currentFrame);
        }

        if (_options.RequireTelemetryChange &&
            frameToSend.FieldMask == CompactTelemetryField.None)
        {
            _lastOutgoingTelemetryFrame = currentFrame;

            return HydronomOutgoingPacket.Skipped(
                "NO_MEANINGFUL_TELEMETRY_CHANGE");
        }

        var envelope = _telemetryAdapter.ToEnvelope(
            frame: frameToSend,
            sourceId: _options.SourceId,
            targetId: _options.TargetId,
            priority: priority,
            extraFlags: extraFlags,
            sessionId: _options.SessionId,
            correlationId: correlationId ?? Guid.NewGuid().ToString("N"));

        var protectedEnvelope = ProtectIfNeeded(envelope);
        var encoded = _binaryCodec.Encode(protectedEnvelope);

        _lastOutgoingTelemetryFrame = currentFrame;

        return HydronomOutgoingPacket.Ready(
            frame: frameToSend,
            envelope: protectedEnvelope,
            encodedMessage: encoded,
            reason: frameToSend.FieldMask == CompactTelemetryField.All
                ? "FULL_TELEMETRY_PACKET_READY"
                : "DELTA_TELEMETRY_PACKET_READY");
    }

    public HydronomIncomingPacket ReadIncomingTelemetryPacket(
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
            return HydronomIncomingPacket.Reject(
                $"BINARY_DECODE_FAILED: {ex.Message}");
        }

        HydronomSecurityResult? securityResult = null;

        if (_options.EnableSecurity)
        {
            securityResult = _securityProvider.Verify(
                envelope,
                _options.SecurityProfile);

            if (!securityResult.Accepted)
            {
                return HydronomIncomingPacket.Reject(
                    securityResult.Reason,
                    securityResult,
                    envelope);
            }
        }

        CompactTelemetryFrame telemetryFrame;

        try
        {
            telemetryFrame = _telemetryAdapter.FromEnvelope(envelope);
        }
        catch (InvalidDataException ex)
        {
            return HydronomIncomingPacket.Reject(
                $"TELEMETRY_ENVELOPE_INVALID: {ex.Message}",
                securityResult,
                envelope);
        }

        return HydronomIncomingPacket.Accept(
            envelope,
            telemetryFrame,
            securityResult);
    }

    public void ResetOutgoingTelemetryBaseline()
    {
        _lastOutgoingTelemetryFrame = null;
    }

    public void SetOutgoingTelemetryBaseline(CompactTelemetryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _lastOutgoingTelemetryFrame = frame;
    }

    private CommunicationEnvelope ProtectIfNeeded(CommunicationEnvelope envelope)
    {
        if (!_options.EnableSecurity)
        {
            return envelope;
        }

        return _securityProvider.Protect(
            envelope,
            _options.SecurityProfile);
    }
}