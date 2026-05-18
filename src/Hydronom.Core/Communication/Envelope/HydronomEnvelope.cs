namespace Hydronom.Core.Communication.Envelope;

public sealed record HydronomEnvelope
{
    public string Protocol { get; init; } = "HYDRONOM";

    public ushort Version { get; init; } = 1;

    public HydronomMessageType Type { get; init; } = HydronomMessageType.Unknown;

    public HydronomMessagePriority Priority { get; init; } = HydronomMessagePriority.Normal;

    public HydronomMessageFlags Flags { get; init; } = HydronomMessageFlags.None;

    public ulong Sequence { get; init; }

    public long TimestampUnixMs { get; init; }

    public string SessionId { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string VehicleId { get; init; } = "";

    public string CorrelationId { get; init; } = "";

    public string ContentType { get; init; } = "application/json";

    public byte[] Payload { get; init; } = Array.Empty<byte>();

    public byte[]? SecurityTag { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    public int PayloadSizeBytes => Payload.Length;

    public bool RequiresAck => Flags.HasFlag(HydronomMessageFlags.RequiresAck);

    public bool IsCritical =>
        Priority is HydronomMessagePriority.Critical or HydronomMessagePriority.Emergency
        || Flags.HasFlag(HydronomMessageFlags.IsSafetyCritical);

    public static HydronomEnvelope Create(
        HydronomMessageType type,
        HydronomMessagePriority priority,
        string sourceId,
        string targetId,
        string vehicleId,
        ulong sequence,
        byte[] payload,
        HydronomMessageFlags flags = HydronomMessageFlags.None,
        string sessionId = "",
        string correlationId = "",
        string contentType = "application/json")
    {
        return new HydronomEnvelope
        {
            Type = type,
            Priority = priority,
            SourceId = sourceId,
            TargetId = targetId,
            VehicleId = vehicleId,
            Sequence = sequence,
            Payload = payload,
            Flags = flags,
            SessionId = sessionId,
            CorrelationId = correlationId,
            ContentType = contentType,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}