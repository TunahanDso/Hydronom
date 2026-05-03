namespace Hydronom.GroundStation.Ack;

using Hydronom.Core.Communication;

/// <summary>
/// Command ACK korelasyon kayÄ±tlarÄ±nÄ±n immutable snapshot modelidir.
/// 
/// Hydronom Ops tarafÄ±nda:
/// - command delivery trace,
/// - gerÃ§ek ACK durumu,
/// - command result gecikmesi,
/// - execution correlation
/// ekranlarÄ±nÄ± besleyebilir.
/// </summary>
public sealed record CommandAckCorrelationSnapshot
{
    public string CorrelationId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string ExecutionId { get; init; } = string.Empty;

    public string SourceNodeId { get; init; } = string.Empty;

    public string TargetNodeId { get; init; } = string.Empty;

    public TransportKind TransportKind { get; init; } = TransportKind.Unknown;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? AckReceivedUtc { get; init; }

    public DateTimeOffset? LastResultUtc { get; init; }

    public string LastStatus { get; init; } = "Unknown";

    public string LastProcessingStage { get; init; } = string.Empty;

    public string LastMessage { get; init; } = string.Empty;

    public bool IsAcked { get; init; }

    public bool IsCompleted { get; init; }

    public bool IsSuccessful { get; init; }

    public bool IsFailed { get; init; }

    public double? AckLatencyMs { get; init; }

    public double? LastResultLatencyMs { get; init; }
}
