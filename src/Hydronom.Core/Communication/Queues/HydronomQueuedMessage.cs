using Hydronom.Core.Communication.Envelope;

namespace Hydronom.Core.Communication.Queues;

public sealed record HydronomQueuedMessage
{
    public HydronomEncodedMessage Message { get; init; } = HydronomEncodedMessage.Empty;

    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; init; }

    public string ChannelId { get; init; } = "default";

    public string Reason { get; init; } = "";

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= now;
    }

    public static HydronomQueuedMessage Create(
        HydronomEncodedMessage message,
        string channelId = "default",
        TimeSpan? ttl = null,
        string reason = "")
    {
        var now = DateTimeOffset.UtcNow;

        return new HydronomQueuedMessage
        {
            Message = message,
            ChannelId = string.IsNullOrWhiteSpace(channelId) ? "default" : channelId,
            EnqueuedAt = now,
            ExpiresAt = ttl.HasValue ? now.Add(ttl.Value) : null,
            Reason = reason
        };
    }
}