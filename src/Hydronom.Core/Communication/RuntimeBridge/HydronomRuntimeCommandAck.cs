using Hydronom.Core.Communication.Commands;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed record HydronomRuntimeCommandAck
{
    private static long _ackSequenceSeed;

    public string AckId { get; init; } = Guid.NewGuid().ToString("N");

    // ACK paketinin kendi haberleşme sequence değeridir.
    // Komutun orijinal Sequence değeri ayrı olarak Sequence alanında korunur.
    public ulong AckSequence { get; init; } =
        unchecked((ulong)Interlocked.Increment(ref _ackSequenceSeed));

    public string CommandId { get; init; } = "";

    public string IntentId { get; init; } = "";

    public HydronomCommandKind CommandKind { get; init; } =
        HydronomCommandKind.Unknown;

    public HydronomRuntimeCommandIntentKind IntentKind { get; init; } =
        HydronomRuntimeCommandIntentKind.Unknown;

    public HydronomRuntimeCommandAckStatus Status { get; init; } =
        HydronomRuntimeCommandAckStatus.Unknown;

    public bool Accepted =>
        Status is HydronomRuntimeCommandAckStatus.Received
            or HydronomRuntimeCommandAckStatus.Accepted
            or HydronomRuntimeCommandAckStatus.QueuedForSafetyGate
            or HydronomRuntimeCommandAckStatus.QueuedForExecution
            or HydronomRuntimeCommandAckStatus.Applied;

    public bool Rejected =>
        Status is HydronomRuntimeCommandAckStatus.Rejected
            or HydronomRuntimeCommandAckStatus.RejectedByDecode
            or HydronomRuntimeCommandAckStatus.RejectedBySecurity
            or HydronomRuntimeCommandAckStatus.RejectedByAuthority
            or HydronomRuntimeCommandAckStatus.RejectedByRuntimeBridge
            or HydronomRuntimeCommandAckStatus.RejectedBySafetyGate;

    public bool Terminal =>
        Status is HydronomRuntimeCommandAckStatus.Applied
            or HydronomRuntimeCommandAckStatus.Rejected
            or HydronomRuntimeCommandAckStatus.RejectedByDecode
            or HydronomRuntimeCommandAckStatus.RejectedBySecurity
            or HydronomRuntimeCommandAckStatus.RejectedByAuthority
            or HydronomRuntimeCommandAckStatus.RejectedByRuntimeBridge
            or HydronomRuntimeCommandAckStatus.RejectedBySafetyGate
            or HydronomRuntimeCommandAckStatus.Failed
            or HydronomRuntimeCommandAckStatus.Timeout;

    public string Reason { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string VehicleId { get; init; } = "";

    public string OperatorId { get; init; } = "";

    // Komutun orijinal sequence değeridir.
    public ulong Sequence { get; init; }

    public long CommandTimestampUnixMs { get; init; }

    public long AckTimestampUnixMs { get; init; } =
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public IReadOnlyList<string> Issues { get; init; } =
        Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    public static HydronomRuntimeCommandAck Create(
        HydronomRuntimeCommandIntent intent,
        HydronomRuntimeCommandAckStatus status,
        string reason,
        IReadOnlyList<string>? issues = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(intent);

        return new HydronomRuntimeCommandAck
        {
            AckSequence = NextAckSequence(),
            CommandId = intent.CommandId,
            IntentId = intent.IntentId,
            CommandKind = intent.SourceCommandKind,
            IntentKind = intent.Kind,
            Status = status,
            Reason = reason,
            SourceId = intent.TargetId,
            TargetId = intent.SourceId,
            VehicleId = intent.VehicleId,
            OperatorId = intent.OperatorId,
            Sequence = intent.Sequence,
            CommandTimestampUnixMs = intent.TimestampUnixMs,
            AckTimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Issues = issues ?? Array.Empty<string>(),
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    public static HydronomRuntimeCommandAck CreateRejectedFromCommand(
        HydronomCommandFrame? command,
        HydronomRuntimeCommandAckStatus status,
        string reason,
        IReadOnlyList<string>? issues = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new HydronomRuntimeCommandAck
        {
            AckSequence = NextAckSequence(),
            CommandId = command?.CommandId ?? "",
            IntentId = "",
            CommandKind = command?.Kind ?? HydronomCommandKind.Unknown,
            IntentKind = HydronomRuntimeCommandIntentKind.Unknown,
            Status = status,
            Reason = reason,
            SourceId = command?.TargetId ?? "",
            TargetId = command?.SourceId ?? "",
            VehicleId = command?.VehicleId ?? "",
            OperatorId = command?.OperatorId ?? "",
            Sequence = command?.Sequence ?? 0,
            CommandTimestampUnixMs = command?.TimestampUnixMs ?? 0,
            AckTimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Issues = issues ?? Array.Empty<string>(),
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    private static ulong NextAckSequence()
    {
        return unchecked((ulong)Interlocked.Increment(ref _ackSequenceSeed));
    }
}