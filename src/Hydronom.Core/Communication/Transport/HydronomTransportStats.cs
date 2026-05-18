namespace Hydronom.Core.Communication.Transport;

public sealed record HydronomTransportStats
{
    public string TransportId { get; init; } = "";

    public bool IsRunning { get; init; }

    public long SentPackets { get; init; }

    public long ReceivedPackets { get; init; }

    public long SentBytes { get; init; }

    public long ReceivedBytes { get; init; }

    public long DroppedPackets { get; init; }

    public int PendingReceivePackets { get; init; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.MinValue;

    public DateTimeOffset SnapshotAt { get; init; } = DateTimeOffset.UtcNow;
}