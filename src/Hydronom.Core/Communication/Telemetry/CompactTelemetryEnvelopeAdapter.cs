using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Telemetry;

public sealed class CompactTelemetryEnvelopeAdapter
{
    public const string CompactTelemetryContentType =
        "application/vnd.hydronom.compact-telemetry+binary";

    private readonly CompactTelemetryCodec _codec;

    public CompactTelemetryEnvelopeAdapter()
        : this(new CompactTelemetryCodec())
    {
    }

    public CompactTelemetryEnvelopeAdapter(CompactTelemetryCodec codec)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public CommunicationEnvelope ToEnvelope(
        CompactTelemetryFrame frame,
        string sourceId,
        string targetId,
        HydronomMessagePriority priority = HydronomMessagePriority.High,
        HydronomMessageFlags extraFlags = HydronomMessageFlags.None,
        string sessionId = "",
        string correlationId = "")
    {
        ArgumentNullException.ThrowIfNull(frame);

        var payload = _codec.Encode(frame);

        var flags = extraFlags;

        if (frame.FieldMask == CompactTelemetryField.All)
        {
            flags |= HydronomMessageFlags.IsSnapshot;
        }
        else
        {
            flags |= HydronomMessageFlags.IsDelta;
        }

        return CommunicationEnvelope.Create(
            type: HydronomMessageType.FusedState,
            priority: priority,
            sourceId: sourceId,
            targetId: targetId,
            vehicleId: frame.VehicleId,
            sequence: frame.Sequence,
            payload: payload,
            flags: flags,
            sessionId: sessionId,
            correlationId: correlationId,
            contentType: CompactTelemetryContentType);
    }

    public CompactTelemetryFrame FromEnvelope(CommunicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.ContentType != CompactTelemetryContentType)
        {
            throw new InvalidDataException(
                $"Envelope compact telemetry content type taşımıyor. ContentType={envelope.ContentType}");
        }

        if (envelope.Type != HydronomMessageType.FusedState &&
            envelope.Type != HydronomMessageType.VehicleState)
        {
            throw new InvalidDataException(
                $"Envelope telemetry mesaj tipi değil. Type={envelope.Type}");
        }

        var frame = _codec.Decode(envelope.Payload);

        if (!string.IsNullOrWhiteSpace(envelope.VehicleId) &&
            !string.Equals(frame.VehicleId, envelope.VehicleId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Envelope VehicleId ile compact telemetry VehicleId uyuşmuyor. Envelope={envelope.VehicleId}, Frame={frame.VehicleId}");
        }

        if (frame.Sequence != envelope.Sequence)
        {
            throw new InvalidDataException(
                $"Envelope Sequence ile compact telemetry Sequence uyuşmuyor. Envelope={envelope.Sequence}, Frame={frame.Sequence}");
        }

        return frame;
    }

    public CommunicationEnvelope BuildDeltaEnvelope(
        CompactTelemetryFrame? previous,
        CompactTelemetryFrame current,
        CompactTelemetryDeltaBuilder deltaBuilder,
        string sourceId,
        string targetId,
        HydronomMessagePriority priority = HydronomMessagePriority.High,
        HydronomMessageFlags extraFlags = HydronomMessageFlags.None,
        string sessionId = "",
        string correlationId = "")
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(deltaBuilder);

        var delta = deltaBuilder.BuildDelta(previous, current);

        return ToEnvelope(
            frame: delta,
            sourceId: sourceId,
            targetId: targetId,
            priority: priority,
            extraFlags: extraFlags,
            sessionId: sessionId,
            correlationId: correlationId);
    }
}