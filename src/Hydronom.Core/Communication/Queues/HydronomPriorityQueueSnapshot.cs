namespace Hydronom.Core.Communication.Queues;

public sealed record HydronomPriorityQueueSnapshot
{
    public int EmergencyCount { get; init; }

    public int CriticalCount { get; init; }

    public int HighCount { get; init; }

    public int NormalCount { get; init; }

    public int LowCount { get; init; }

    public int BulkCount { get; init; }

    public long EnqueuedMessages { get; init; }

    public long DequeuedMessages { get; init; }

    public long DroppedMessages { get; init; }

    public long ExpiredMessages { get; init; }

    public long DroppedLowPriorityMessages { get; init; }

    public long DroppedBulkMessages { get; init; }

    public int TotalCount =>
        EmergencyCount +
        CriticalCount +
        HighCount +
        NormalCount +
        LowCount +
        BulkCount;

    public bool HasPending => TotalCount > 0;
}