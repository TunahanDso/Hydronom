using Hydronom.Core.Communication.Diagnostics;

namespace Hydronom.Core.Communication.Bandwidth;

public sealed record HydronomBandwidthBudget
{
    public HydronomLinkHealthLevel LinkLevel { get; init; } = HydronomLinkHealthLevel.Unknown;

    public int MaxMessagesPerTick { get; init; }

    public int MaxBytesPerTick { get; init; }

    public double StateTelemetryHz { get; init; }

    public double MissionTelemetryHz { get; init; }

    public double DiagnosticsHz { get; init; }

    public double WorldUpdateHz { get; init; }

    public bool AllowBulkTraffic { get; init; }

    public bool AllowVideoMetadata { get; init; }

    public bool DropLowPriorityTraffic { get; init; }

    public static HydronomBandwidthBudget ForLink(HydronomLinkHealthLevel level)
    {
        return level switch
        {
            HydronomLinkHealthLevel.Excellent => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 256,
                MaxBytesPerTick = 256 * 1024,
                StateTelemetryHz = 50.0,
                MissionTelemetryHz = 10.0,
                DiagnosticsHz = 5.0,
                WorldUpdateHz = 10.0,
                AllowBulkTraffic = true,
                AllowVideoMetadata = true,
                DropLowPriorityTraffic = false
            },

            HydronomLinkHealthLevel.Good => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 128,
                MaxBytesPerTick = 128 * 1024,
                StateTelemetryHz = 20.0,
                MissionTelemetryHz = 5.0,
                DiagnosticsHz = 2.0,
                WorldUpdateHz = 5.0,
                AllowBulkTraffic = true,
                AllowVideoMetadata = true,
                DropLowPriorityTraffic = false
            },

            HydronomLinkHealthLevel.Weak => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 64,
                MaxBytesPerTick = 32 * 1024,
                StateTelemetryHz = 10.0,
                MissionTelemetryHz = 2.0,
                DiagnosticsHz = 1.0,
                WorldUpdateHz = 2.0,
                AllowBulkTraffic = false,
                AllowVideoMetadata = true,
                DropLowPriorityTraffic = true
            },

            HydronomLinkHealthLevel.Critical => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 24,
                MaxBytesPerTick = 8 * 1024,
                StateTelemetryHz = 2.0,
                MissionTelemetryHz = 1.0,
                DiagnosticsHz = 0.2,
                WorldUpdateHz = 0.5,
                AllowBulkTraffic = false,
                AllowVideoMetadata = false,
                DropLowPriorityTraffic = true
            },

            HydronomLinkHealthLevel.Lost => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 0,
                MaxBytesPerTick = 0,
                StateTelemetryHz = 0.0,
                MissionTelemetryHz = 0.0,
                DiagnosticsHz = 0.0,
                WorldUpdateHz = 0.0,
                AllowBulkTraffic = false,
                AllowVideoMetadata = false,
                DropLowPriorityTraffic = true
            },

            _ => new HydronomBandwidthBudget
            {
                LinkLevel = level,
                MaxMessagesPerTick = 32,
                MaxBytesPerTick = 16 * 1024,
                StateTelemetryHz = 5.0,
                MissionTelemetryHz = 1.0,
                DiagnosticsHz = 0.5,
                WorldUpdateHz = 1.0,
                AllowBulkTraffic = false,
                AllowVideoMetadata = false,
                DropLowPriorityTraffic = true
            }
        };
    }
}