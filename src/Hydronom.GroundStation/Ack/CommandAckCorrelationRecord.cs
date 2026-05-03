namespace Hydronom.GroundStation.Ack;

using Hydronom.Core.Communication;

/// <summary>
/// Bir FleetCommand ile onun route execution kaydÄ± arasÄ±ndaki ACK/result korelasyon kaydÄ±dÄ±r.
/// 
/// Bu kayÄ±t ÅŸunu baÄŸlar:
/// - CommandId
/// - MessageId
/// - ExecutionId
/// - Hedef araÃ§
/// - KullanÄ±lan transport
/// - Gelen FleetCommandResult durumu
/// 
/// AmaÃ§:
/// SendAsync baÅŸarÄ±lÄ± oldu diye ACK varsaymak yerine,
/// araÃ§tan gerÃ§ekten FleetCommandResult geldiÄŸinde ilgili route execution kaydÄ±nÄ± gÃ¼ncelleyebilmektir.
/// </summary>
public sealed class CommandAckCorrelationRecord
{
    /// <summary>
    /// Korelasyon kayÄ±t ID'si.
    /// </summary>
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// FleetCommand.CommandId.
    /// </summary>
    public string CommandId { get; init; } = string.Empty;

    /// <summary>
    /// HydronomEnvelope.MessageId.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// RouteExecutionRecord.ExecutionId.
    /// </summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>
    /// Komutun hedef node ID'si.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komutu gÃ¶nderen node ID'si.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶nderimde kullanÄ±lan veya tercih edilen transport tÃ¼rÃ¼.
    /// 
    /// Route Ã¼zerinde birden fazla candidate varsa ilk kullanÄ±lan/uygun transport burada tutulur.
    /// </summary>
    public TransportKind TransportKind { get; init; } = TransportKind.Unknown;

    /// <summary>
    /// Korelasyon kaydÄ±nÄ±n oluÅŸturulduÄŸu UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Ä°lk ACK/result alÄ±ndÄ±ÄŸÄ± UTC zaman.
    /// </summary>
    public DateTimeOffset? AckReceivedUtc { get; private set; }

    /// <summary>
    /// Son result alÄ±ndÄ±ÄŸÄ± UTC zaman.
    /// </summary>
    public DateTimeOffset? LastResultUtc { get; private set; }

    /// <summary>
    /// Son command result status deÄŸeri.
    /// 
    /// Ã–rnek:
    /// - Accepted
    /// - Applied
    /// - Rejected
    /// - Failed
    /// - Timeout
    /// </summary>
    public string LastStatus { get; private set; } = "Pending";

    /// <summary>
    /// Son processing stage.
    /// </summary>
    public string LastProcessingStage { get; private set; } = string.Empty;

    /// <summary>
    /// Son result mesajÄ±.
    /// </summary>
    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Bu komut iÃ§in araÃ§tan herhangi bir sonuÃ§ geldi mi?
    /// </summary>
    public bool IsAcked => AckReceivedUtc.HasValue;

    /// <summary>
    /// Komut sonuÃ§landÄ± mÄ±?
    /// </summary>
    public bool IsCompleted =>
        string.Equals(LastStatus, "Applied", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Expired", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Timeout", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Komut baÅŸarÄ±lÄ± sonuÃ§landÄ± mÄ±?
    /// </summary>
    public bool IsSuccessful =>
        string.Equals(LastStatus, "Accepted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Applied", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Completed", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Komut baÅŸarÄ±sÄ±z sonuÃ§landÄ± mÄ±?
    /// </summary>
    public bool IsFailed =>
        string.Equals(LastStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Expired", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Timeout", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ACK/result gecikmesi.
    /// </summary>
    public double? AckLatencyMs =>
        AckReceivedUtc.HasValue
            ? Math.Max(0, (AckReceivedUtc.Value - CreatedUtc).TotalMilliseconds)
            : null;

    /// <summary>
    /// Son result gecikmesi.
    /// </summary>
    public double? LastResultLatencyMs =>
        LastResultUtc.HasValue
            ? Math.Max(0, (LastResultUtc.Value - CreatedUtc).TotalMilliseconds)
            : null;

    /// <summary>
    /// FleetCommandResult bilgisini korelasyon kaydÄ±na uygular.
    /// </summary>
    public void ApplyResult(
        string status,
        string processingStage,
        string message,
        DateTimeOffset nowUtc)
    {
        if (!AckReceivedUtc.HasValue)
            AckReceivedUtc = nowUtc;

        LastResultUtc = nowUtc;
        LastStatus = string.IsNullOrWhiteSpace(status) ? "Unknown" : status;
        LastProcessingStage = processingStage ?? string.Empty;
        LastMessage = message ?? string.Empty;
    }
}
