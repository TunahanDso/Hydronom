using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.LinkHealth;

/// <summary>
/// Diagnostics, Ops UI ve smoke test için immutable bağlantı özeti.
/// </summary>
public sealed record LinkHealthSnapshot(
    string VehicleId,
    TransportKind TransportKind,
    LinkHealthStatus Status,
    double QualityScore,
    double SuccessRate,
    double FailureRate,
    double TimeoutRate,
    double? LastLatencyMs,
    double? AverageLatencyMs,
    int SentCount,
    int SuccessCount,
    int FailureCount,
    int AckCount,
    int TimeoutCount,
    int LostPacketEstimateCount,
    DateTime LastSeenUtc,
    DateTime LastUpdatedUtc
);