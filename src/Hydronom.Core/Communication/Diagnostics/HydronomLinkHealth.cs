namespace Hydronom.Core.Communication.Diagnostics;

public enum HydronomLinkHealthLevel : byte
{
    Unknown = 0,
    Excellent = 1,
    Good = 2,
    Weak = 3,
    Critical = 4,
    Lost = 5
}

public sealed record HydronomLinkHealth
{
    public string LinkId { get; init; } = "";

    public HydronomLinkHealthLevel Level { get; init; } = HydronomLinkHealthLevel.Unknown;

    public double LatencyMs { get; init; }

    public double JitterMs { get; init; }

    public double PacketLossRatio { get; init; }

    public double RxBytesPerSecond { get; init; }

    public double TxBytesPerSecond { get; init; }

    public int SendQueueDepth { get; init; }

    public int DroppedMessages { get; init; }

    public DateTimeOffset LastReceiveTime { get; init; } = DateTimeOffset.MinValue;

    public bool IsUsable =>
        Level is HydronomLinkHealthLevel.Excellent
            or HydronomLinkHealthLevel.Good
            or HydronomLinkHealthLevel.Weak;

    public bool IsDegraded =>
        Level is HydronomLinkHealthLevel.Weak
            or HydronomLinkHealthLevel.Critical;

    public bool IsLost => Level == HydronomLinkHealthLevel.Lost;
}