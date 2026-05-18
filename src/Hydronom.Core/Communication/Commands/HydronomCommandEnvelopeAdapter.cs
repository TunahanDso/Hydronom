using Hydronom.Core.Communication.Envelope;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

namespace Hydronom.Core.Communication.Commands;

public sealed class HydronomCommandEnvelopeAdapter
{
    public const string CommandContentType = HydronomCommandBinaryCodec.ContentType;

    private readonly HydronomCommandBinaryCodec _codec;

    public HydronomCommandEnvelopeAdapter()
        : this(new HydronomCommandBinaryCodec())
    {
    }

    public HydronomCommandEnvelopeAdapter(HydronomCommandBinaryCodec codec)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public CommunicationEnvelope ToEnvelope(
        HydronomCommandFrame command,
        string sessionId = "",
        string correlationId = "")
    {
        ArgumentNullException.ThrowIfNull(command);

        var payload = _codec.Encode(command);
        var flags = HydronomMessageFlags.IsOperatorCommand;

        if (command.RequiresAck)
        {
            flags |= HydronomMessageFlags.RequiresAck;
        }

        if (command.SafetyCritical)
        {
            flags |= HydronomMessageFlags.IsSafetyCritical;
        }

        if (command.Authority == HydronomCommandAuthority.AutonomousRuntime)
        {
            flags &= ~HydronomMessageFlags.IsOperatorCommand;
            flags |= HydronomMessageFlags.IsAutonomousCommand;
        }

        var messageType = MapCommandToMessageType(command.Kind);
        var priority = MapCommandToPriority(command.Kind);

        return CommunicationEnvelope.Create(
            type: messageType,
            priority: priority,
            sourceId: command.SourceId,
            targetId: command.TargetId,
            vehicleId: command.VehicleId,
            sequence: command.Sequence,
            payload: payload,
            flags: flags,
            sessionId: sessionId,
            correlationId: string.IsNullOrWhiteSpace(correlationId)
                ? command.CommandId
                : correlationId,
            contentType: CommandContentType);
    }

    public HydronomCommandFrame FromEnvelope(CommunicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.ContentType != CommandContentType)
        {
            throw new InvalidDataException(
                $"Envelope command binary content type taşımıyor. ContentType={envelope.ContentType}");
        }

        var command = _codec.Decode(envelope.Payload);

        if (!string.Equals(command.SourceId, envelope.SourceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Command SourceId ile envelope SourceId uyuşmuyor. Command={command.SourceId}, Envelope={envelope.SourceId}");
        }

        if (!string.Equals(command.TargetId, envelope.TargetId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Command TargetId ile envelope TargetId uyuşmuyor. Command={command.TargetId}, Envelope={envelope.TargetId}");
        }

        if (!string.Equals(command.VehicleId, envelope.VehicleId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Command VehicleId ile envelope VehicleId uyuşmuyor. Command={command.VehicleId}, Envelope={envelope.VehicleId}");
        }

        if (command.Sequence != envelope.Sequence)
        {
            throw new InvalidDataException(
                $"Command Sequence ile envelope Sequence uyuşmuyor. Command={command.Sequence}, Envelope={envelope.Sequence}");
        }

        var expectedType = MapCommandToMessageType(command.Kind);

        if (envelope.Type != expectedType)
        {
            throw new InvalidDataException(
                $"Command kind ile envelope message type uyuşmuyor. Kind={command.Kind}, EnvelopeType={envelope.Type}, Expected={expectedType}");
        }

        return command;
    }

    private static HydronomMessageType MapCommandToMessageType(HydronomCommandKind kind)
    {
        return kind switch
        {
            HydronomCommandKind.Arm => HydronomMessageType.Arm,
            HydronomCommandKind.Disarm => HydronomMessageType.Disarm,
            HydronomCommandKind.EmergencyStop => HydronomMessageType.EmergencyStop,
            HydronomCommandKind.ManualControl => HydronomMessageType.ManualControl,
            HydronomCommandKind.AuthorityClaim => HydronomMessageType.AuthorityClaim,
            _ => HydronomMessageType.MissionCommand
        };
    }

    private static HydronomMessagePriority MapCommandToPriority(HydronomCommandKind kind)
    {
        return kind switch
        {
            HydronomCommandKind.EmergencyStop => HydronomMessagePriority.Emergency,

            HydronomCommandKind.Arm
                or HydronomCommandKind.Disarm
                or HydronomCommandKind.ManualControl
                or HydronomCommandKind.AuthorityClaim
                or HydronomCommandKind.AuthorityRelease => HydronomMessagePriority.Critical,

            HydronomCommandKind.MissionCommand
                or HydronomCommandKind.ScenarioCommand
                or HydronomCommandKind.SetMode
                or HydronomCommandKind.SetTarget
                or HydronomCommandKind.PauseMission
                or HydronomCommandKind.ResumeMission
                or HydronomCommandKind.AbortMission => HydronomMessagePriority.High,

            HydronomCommandKind.RequestStatus
                or HydronomCommandKind.RequestSnapshot => HydronomMessagePriority.Normal,

            _ => HydronomMessagePriority.Normal
        };
    }
}