using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Route execution kayıtlarının immutable özetidir.
/// Diagnostics, smoke test ve Hydronom Ops için kullanılabilir.
/// </summary>
public sealed record RouteExecutionSnapshot
{
    public string ExecutionId { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public string SourceNodeId { get; init; } = string.Empty;

    public string TargetNodeId { get; init; } = string.Empty;

    public bool CanRoute { get; init; }

    public bool IsCompleted { get; init; }

    public bool HasSuccess { get; init; }

    public bool HasAck { get; init; }

    public bool HasTimeout { get; init; }

    public bool HasFailure { get; init; }

    public TransportSendStatus LastStatus { get; init; }

    public double? BestLatencyMs { get; init; }

    public IReadOnlyList<TransportKind> CandidateTransports { get; init; } =
        Array.Empty<TransportKind>();

    public IReadOnlyList<TransportSendResult> SendResults { get; init; } =
        Array.Empty<TransportSendResult>();

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; }
}