using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed class HydronomRuntimeCommandAckEnvelopeAdapter
{
    public const string AckContentType = HydronomRuntimeCommandAckBinaryCodec.ContentType;

    private readonly HydronomRuntimeCommandAckBinaryCodec _codec;

    public HydronomRuntimeCommandAckEnvelopeAdapter()
        : this(new HydronomRuntimeCommandAckBinaryCodec())
    {
    }

    public HydronomRuntimeCommandAckEnvelopeAdapter(
        HydronomRuntimeCommandAckBinaryCodec codec)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public CommunicationEnvelope ToEnvelope(
        HydronomRuntimeCommandAck ack,
        string sessionId = "",
        string correlationId = "")
    {
        ArgumentNullException.ThrowIfNull(ack);

        var payload = _codec.Encode(ack);

        var flags = HydronomMessageFlags.IsAck;

        var isNack =
            ack.Rejected ||
            ack.Status is
                HydronomRuntimeCommandAckStatus.Failed or
                HydronomRuntimeCommandAckStatus.Timeout;

        if (isNack)
        {
            flags |= HydronomMessageFlags.IsNack;
        }

        if (IsSafetyCriticalAck(ack.CommandKind))
        {
            flags |= HydronomMessageFlags.IsSafetyCritical;
        }

        return CommunicationEnvelope.Create(
            type: isNack ? HydronomMessageType.Nack : HydronomMessageType.Ack,
            priority: MapAckPriority(ack),
            sourceId: ack.SourceId,
            targetId: ack.TargetId,
            vehicleId: ack.VehicleId,

            // DİKKAT:
            // Envelope sequence replay korumasında kullanılır.
            // Bir komut için birden fazla ACK üretilebileceğinden burada command.Sequence değil AckSequence kullanılır.
            sequence: ack.AckSequence,

            payload: payload,
            flags: flags,
            sessionId: sessionId,
            correlationId: string.IsNullOrWhiteSpace(correlationId)
                ? ack.CommandId
                : correlationId,
            contentType: AckContentType);
    }

    public HydronomRuntimeCommandAck FromEnvelope(
        CommunicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.ContentType != AckContentType)
        {
            throw new InvalidDataException(
                $"Envelope ACK binary content type taşımıyor. ContentType={envelope.ContentType}");
        }

        if (envelope.Type is not HydronomMessageType.Ack and not HydronomMessageType.Nack)
        {
            throw new InvalidDataException(
                $"Envelope ACK/NACK mesaj tipi değil. Type={envelope.Type}");
        }

        var ack = _codec.Decode(envelope.Payload);

        if (!string.Equals(ack.SourceId, envelope.SourceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"ACK SourceId ile envelope SourceId uyuşmuyor. Ack={ack.SourceId}, Envelope={envelope.SourceId}");
        }

        if (!string.Equals(ack.TargetId, envelope.TargetId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"ACK TargetId ile envelope TargetId uyuşmuyor. Ack={ack.TargetId}, Envelope={envelope.TargetId}");
        }

        if (!string.Equals(ack.VehicleId, envelope.VehicleId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"ACK VehicleId ile envelope VehicleId uyuşmuyor. Ack={ack.VehicleId}, Envelope={envelope.VehicleId}");
        }

        if (ack.AckSequence != envelope.Sequence)
        {
            throw new InvalidDataException(
                $"ACK AckSequence ile envelope Sequence uyuşmuyor. AckSequence={ack.AckSequence}, Envelope={envelope.Sequence}");
        }

        var decodedIsNack =
            ack.Rejected ||
            ack.Status is
                HydronomRuntimeCommandAckStatus.Failed or
                HydronomRuntimeCommandAckStatus.Timeout;

        if (envelope.Type == HydronomMessageType.Nack && !decodedIsNack)
        {
            throw new InvalidDataException(
                $"Envelope NACK ama payload ACK status rejected/failed/timeout değil. Status={ack.Status}");
        }

        if (envelope.Type == HydronomMessageType.Ack && decodedIsNack)
        {
            throw new InvalidDataException(
                $"Envelope ACK ama payload NACK durumunda. Status={ack.Status}");
        }

        return ack;
    }

    private static bool IsSafetyCriticalAck(HydronomCommandKind kind)
    {
        return kind is
            HydronomCommandKind.Arm or
            HydronomCommandKind.Disarm or
            HydronomCommandKind.EmergencyStop or
            HydronomCommandKind.ManualControl or
            HydronomCommandKind.MissionCommand or
            HydronomCommandKind.ScenarioCommand;
    }

    private static HydronomMessagePriority MapAckPriority(
        HydronomRuntimeCommandAck ack)
    {
        if (ack.CommandKind == HydronomCommandKind.EmergencyStop)
        {
            return HydronomMessagePriority.Emergency;
        }

        if (ack.Rejected ||
            ack.Status is
                HydronomRuntimeCommandAckStatus.Failed or
                HydronomRuntimeCommandAckStatus.Timeout or
                HydronomRuntimeCommandAckStatus.RejectedBySecurity or
                HydronomRuntimeCommandAckStatus.RejectedByAuthority or
                HydronomRuntimeCommandAckStatus.RejectedBySafetyGate)
        {
            return HydronomMessagePriority.Critical;
        }

        if (ack.CommandKind is
                HydronomCommandKind.Arm or
                HydronomCommandKind.Disarm or
                HydronomCommandKind.ManualControl)
        {
            return HydronomMessagePriority.Critical;
        }

        return HydronomMessagePriority.High;
    }
}