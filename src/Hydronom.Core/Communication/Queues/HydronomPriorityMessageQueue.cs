using Hydronom.Core.Communication.Envelope;

namespace Hydronom.Core.Communication.Queues;

public sealed class HydronomPriorityMessageQueue
{
    private readonly object _gate = new();

    private readonly Queue<HydronomQueuedMessage> _emergency = new();
    private readonly Queue<HydronomQueuedMessage> _critical = new();
    private readonly Queue<HydronomQueuedMessage> _high = new();
    private readonly Queue<HydronomQueuedMessage> _normal = new();
    private readonly Queue<HydronomQueuedMessage> _low = new();
    private readonly Queue<HydronomQueuedMessage> _bulk = new();

    private long _enqueuedMessages;
    private long _dequeuedMessages;
    private long _droppedMessages;
    private long _expiredMessages;
    private long _droppedLowPriorityMessages;
    private long _droppedBulkMessages;

    public int MaxEmergencyDepth { get; init; } = 512;

    public int MaxCriticalDepth { get; init; } = 1024;

    public int MaxHighDepth { get; init; } = 2048;

    public int MaxNormalDepth { get; init; } = 4096;

    public int MaxLowDepth { get; init; } = 2048;

    public int MaxBulkDepth { get; init; } = 1024;

    public bool Enqueue(HydronomQueuedMessage queuedMessage)
    {
        ArgumentNullException.ThrowIfNull(queuedMessage);

        lock (_gate)
        {
            var queue = SelectQueue(queuedMessage.Message.Priority);
            var maxDepth = GetMaxDepth(queuedMessage.Message.Priority);

            if (queue.Count >= maxDepth)
            {
                if (!TryMakeRoomFor(queuedMessage.Message.Priority))
                {
                    _droppedMessages++;

                    if (queuedMessage.Message.Priority == HydronomMessagePriority.Low)
                    {
                        _droppedLowPriorityMessages++;
                    }

                    if (queuedMessage.Message.Priority == HydronomMessagePriority.Bulk)
                    {
                        _droppedBulkMessages++;
                    }

                    return false;
                }
            }

            queue.Enqueue(queuedMessage);
            _enqueuedMessages++;
            return true;
        }
    }

    public bool TryDequeue(out HydronomQueuedMessage queuedMessage)
    {
        lock (_gate)
        {
            DropExpiredMessages(DateTimeOffset.UtcNow);

            if (TryDequeueFrom(_emergency, out queuedMessage) ||
                TryDequeueFrom(_critical, out queuedMessage) ||
                TryDequeueFrom(_high, out queuedMessage) ||
                TryDequeueFrom(_normal, out queuedMessage) ||
                TryDequeueFrom(_low, out queuedMessage) ||
                TryDequeueFrom(_bulk, out queuedMessage))
            {
                _dequeuedMessages++;
                return true;
            }

            queuedMessage = default!;
            return false;
        }
    }

    public IReadOnlyList<HydronomQueuedMessage> DequeueBatch(
        int maxMessages,
        int maxBytes)
    {
        if (maxMessages <= 0 || maxBytes <= 0)
        {
            return Array.Empty<HydronomQueuedMessage>();
        }

        var result = new List<HydronomQueuedMessage>(Math.Min(maxMessages, 64));
        var usedBytes = 0;

        lock (_gate)
        {
            DropExpiredMessages(DateTimeOffset.UtcNow);

            while (result.Count < maxMessages && usedBytes < maxBytes)
            {
                if (!TryPeekNext(out var next))
                {
                    break;
                }

                var size = Math.Max(1, next.Message.SizeBytes);

                if (result.Count > 0 && usedBytes + size > maxBytes)
                {
                    break;
                }

                if (!TryDequeue(out var dequeued))
                {
                    break;
                }

                result.Add(dequeued);
                usedBytes += size;
            }
        }

        return result;
    }

    public HydronomPriorityQueueSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new HydronomPriorityQueueSnapshot
            {
                EmergencyCount = _emergency.Count,
                CriticalCount = _critical.Count,
                HighCount = _high.Count,
                NormalCount = _normal.Count,
                LowCount = _low.Count,
                BulkCount = _bulk.Count,
                EnqueuedMessages = _enqueuedMessages,
                DequeuedMessages = _dequeuedMessages,
                DroppedMessages = _droppedMessages,
                ExpiredMessages = _expiredMessages,
                DroppedLowPriorityMessages = _droppedLowPriorityMessages,
                DroppedBulkMessages = _droppedBulkMessages
            };
        }
    }

    public void ClearLowPriorityTraffic()
    {
        lock (_gate)
        {
            _droppedMessages += _low.Count + _bulk.Count;
            _droppedLowPriorityMessages += _low.Count;
            _droppedBulkMessages += _bulk.Count;

            _low.Clear();
            _bulk.Clear();
        }
    }

    public void ClearAll()
    {
        lock (_gate)
        {
            _emergency.Clear();
            _critical.Clear();
            _high.Clear();
            _normal.Clear();
            _low.Clear();
            _bulk.Clear();
        }
    }

    private bool TryMakeRoomFor(HydronomMessagePriority incomingPriority)
    {
        // Acil ve kritik mesajlar geliyorsa düşük değerli trafik feda edilir.
        if (incomingPriority is HydronomMessagePriority.Emergency or HydronomMessagePriority.Critical)
        {
            if (_bulk.Count > 0)
            {
                _bulk.Dequeue();
                _droppedMessages++;
                _droppedBulkMessages++;
                return true;
            }

            if (_low.Count > 0)
            {
                _low.Dequeue();
                _droppedMessages++;
                _droppedLowPriorityMessages++;
                return true;
            }

            if (_normal.Count > 0)
            {
                _normal.Dequeue();
                _droppedMessages++;
                return true;
            }
        }

        // High mesajlar bulk/low trafiğin önüne geçebilir.
        if (incomingPriority == HydronomMessagePriority.High)
        {
            if (_bulk.Count > 0)
            {
                _bulk.Dequeue();
                _droppedMessages++;
                _droppedBulkMessages++;
                return true;
            }

            if (_low.Count > 0)
            {
                _low.Dequeue();
                _droppedMessages++;
                _droppedLowPriorityMessages++;
                return true;
            }
        }

        return false;
    }

    private void DropExpiredMessages(DateTimeOffset now)
    {
        DropExpiredFrom(_emergency, now);
        DropExpiredFrom(_critical, now);
        DropExpiredFrom(_high, now);
        DropExpiredFrom(_normal, now);
        DropExpiredFrom(_low, now);
        DropExpiredFrom(_bulk, now);
    }

    private void DropExpiredFrom(
        Queue<HydronomQueuedMessage> queue,
        DateTimeOffset now)
    {
        if (queue.Count == 0)
        {
            return;
        }

        var kept = new Queue<HydronomQueuedMessage>(queue.Count);

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();

            if (item.IsExpired(now))
            {
                _expiredMessages++;
                _droppedMessages++;

                if (item.Message.Priority == HydronomMessagePriority.Low)
                {
                    _droppedLowPriorityMessages++;
                }

                if (item.Message.Priority == HydronomMessagePriority.Bulk)
                {
                    _droppedBulkMessages++;
                }

                continue;
            }

            kept.Enqueue(item);
        }

        while (kept.Count > 0)
        {
            queue.Enqueue(kept.Dequeue());
        }
    }

    private static bool TryDequeueFrom(
        Queue<HydronomQueuedMessage> queue,
        out HydronomQueuedMessage queuedMessage)
    {
        if (queue.Count > 0)
        {
            queuedMessage = queue.Dequeue();
            return true;
        }

        queuedMessage = default!;
        return false;
    }

    private bool TryPeekNext(out HydronomQueuedMessage queuedMessage)
    {
        if (_emergency.Count > 0)
        {
            queuedMessage = _emergency.Peek();
            return true;
        }

        if (_critical.Count > 0)
        {
            queuedMessage = _critical.Peek();
            return true;
        }

        if (_high.Count > 0)
        {
            queuedMessage = _high.Peek();
            return true;
        }

        if (_normal.Count > 0)
        {
            queuedMessage = _normal.Peek();
            return true;
        }

        if (_low.Count > 0)
        {
            queuedMessage = _low.Peek();
            return true;
        }

        if (_bulk.Count > 0)
        {
            queuedMessage = _bulk.Peek();
            return true;
        }

        queuedMessage = default!;
        return false;
    }

    private Queue<HydronomQueuedMessage> SelectQueue(HydronomMessagePriority priority)
    {
        return priority switch
        {
            HydronomMessagePriority.Emergency => _emergency,
            HydronomMessagePriority.Critical => _critical,
            HydronomMessagePriority.High => _high,
            HydronomMessagePriority.Normal => _normal,
            HydronomMessagePriority.Low => _low,
            HydronomMessagePriority.Bulk => _bulk,
            _ => _normal
        };
    }

    private int GetMaxDepth(HydronomMessagePriority priority)
    {
        return priority switch
        {
            HydronomMessagePriority.Emergency => MaxEmergencyDepth,
            HydronomMessagePriority.Critical => MaxCriticalDepth,
            HydronomMessagePriority.High => MaxHighDepth,
            HydronomMessagePriority.Normal => MaxNormalDepth,
            HydronomMessagePriority.Low => MaxLowDepth,
            HydronomMessagePriority.Bulk => MaxBulkDepth,
            _ => MaxNormalDepth
        };
    }
}