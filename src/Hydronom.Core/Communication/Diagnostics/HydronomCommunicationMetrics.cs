namespace Hydronom.Core.Communication.Diagnostics;

public sealed record HydronomCommunicationMetrics
{
    public string NodeId { get; init; } = "";

    public long SentMessages { get; init; }

    public long ReceivedMessages { get; init; }

    public long SentBytes { get; init; }

    public long ReceivedBytes { get; init; }

    public long DroppedLowPriorityMessages { get; init; }

    public long DroppedExpiredMessages { get; init; }

    public long SecurityRejectedMessages { get; init; }

    public long ReplayRejectedMessages { get; init; }

    public long DecodeFailedMessages { get; init; }

    public double CompressionRatio { get; init; } = 1.0;

    public DateTimeOffset Since { get; init; } = DateTimeOffset.UtcNow;
}