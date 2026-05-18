namespace Hydronom.Core.Communication.Transport;

public sealed record HydronomTransportPacket
{
    public string TransportId { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string ChannelId { get; init; } = "default";

    public ulong Sequence { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public byte[] Bytes { get; init; } = Array.Empty<byte>();

    public int SizeBytes => Bytes.Length;

    public static HydronomTransportPacket Create(
        byte[] bytes,
        string sourceId,
        string targetId,
        string transportId = "",
        string channelId = "default",
        ulong sequence = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return new HydronomTransportPacket
        {
            TransportId = transportId,
            SourceId = sourceId,
            TargetId = targetId,
            ChannelId = string.IsNullOrWhiteSpace(channelId) ? "default" : channelId,
            Sequence = sequence,
            Bytes = bytes.ToArray(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}