using Hydronom.Core.Communication.Diagnostics;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Queues;

namespace Hydronom.Core.Communication.Bandwidth;

public sealed class HydronomAdaptiveBandwidthPolicy
{
    public HydronomBandwidthBudget CreateBudget(HydronomLinkHealth linkHealth)
    {
        ArgumentNullException.ThrowIfNull(linkHealth);

        var level = EstimateLevel(linkHealth);
        return HydronomBandwidthBudget.ForLink(level);
    }

    public bool ShouldSend(
        HydronomEncodedMessage message,
        HydronomBandwidthBudget budget)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(budget);

        if (budget.LinkLevel == HydronomLinkHealthLevel.Lost)
        {
            return message.Priority == HydronomMessagePriority.Emergency;
        }

        if (message.Priority is HydronomMessagePriority.Emergency
            or HydronomMessagePriority.Critical)
        {
            return true;
        }

        if (budget.DropLowPriorityTraffic &&
            message.Priority is HydronomMessagePriority.Low or HydronomMessagePriority.Bulk)
        {
            return false;
        }

        if (!budget.AllowBulkTraffic &&
            message.Priority == HydronomMessagePriority.Bulk)
        {
            return false;
        }

        return true;
    }

    public IReadOnlyList<HydronomQueuedMessage> SelectBatchForSend(
        HydronomPriorityMessageQueue queue,
        HydronomBandwidthBudget budget)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(budget);

        if (budget.DropLowPriorityTraffic)
        {
            queue.ClearLowPriorityTraffic();
        }

        if (budget.MaxMessagesPerTick <= 0 || budget.MaxBytesPerTick <= 0)
        {
            return Array.Empty<HydronomQueuedMessage>();
        }

        var rawBatch = queue.DequeueBatch(
            budget.MaxMessagesPerTick,
            budget.MaxBytesPerTick);

        var selected = new List<HydronomQueuedMessage>(rawBatch.Count);

        foreach (var item in rawBatch)
        {
            if (ShouldSend(item.Message, budget))
            {
                selected.Add(item);
            }
        }

        return selected;
    }

    private static HydronomLinkHealthLevel EstimateLevel(HydronomLinkHealth linkHealth)
    {
        if (linkHealth.Level != HydronomLinkHealthLevel.Unknown)
        {
            return linkHealth.Level;
        }

        if (linkHealth.PacketLossRatio >= 0.50 ||
            linkHealth.LatencyMs >= 2000 ||
            linkHealth.SendQueueDepth >= 4096)
        {
            return HydronomLinkHealthLevel.Critical;
        }

        if (linkHealth.PacketLossRatio >= 0.15 ||
            linkHealth.LatencyMs >= 800 ||
            linkHealth.JitterMs >= 300 ||
            linkHealth.SendQueueDepth >= 2048)
        {
            return HydronomLinkHealthLevel.Weak;
        }

        if (linkHealth.PacketLossRatio >= 0.03 ||
            linkHealth.LatencyMs >= 250 ||
            linkHealth.JitterMs >= 100 ||
            linkHealth.SendQueueDepth >= 512)
        {
            return HydronomLinkHealthLevel.Good;
        }

        return HydronomLinkHealthLevel.Excellent;
    }
}