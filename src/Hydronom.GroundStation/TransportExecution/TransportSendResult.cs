using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Tek bir transport Ã¼zerinden yapÄ±lan gÃ¶nderim denemesinin sonucudur.
/// 
/// Ã–rnek:
/// - VEHICLE-ALPHA-001 hedefine Tcp Ã¼zerinden FleetCommand gÃ¶nderildi.
/// - 24 ms sonra ACK geldi.
/// - SonuÃ§: Acked.
/// </summary>
public sealed record TransportSendResult
{
    /// <summary>
    /// GÃ¶nderim sonucuna ait benzersiz kayÄ±t ID'si.
    /// </summary>
    public string SendResultId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Ä°lgili HydronomEnvelope mesaj ID'si.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node ID.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶nderim iÃ§in kullanÄ±lan transport tÃ¼rÃ¼.
    /// </summary>
    public TransportKind TransportKind { get; init; }

    /// <summary>
    /// GÃ¶nderim sonucu.
    /// </summary>
    public TransportSendStatus Status { get; init; } = TransportSendStatus.Unknown;

    /// <summary>
    /// GÃ¶nderimin baÅŸarÄ±lÄ± kabul edilip edilmediÄŸi.
    /// </summary>
    public bool Success =>
        Status is TransportSendStatus.Sent or TransportSendStatus.Acked;

    /// <summary>
    /// Bu sonuÃ§ ACK alÄ±ndÄ±ÄŸÄ±nÄ± temsil ediyor mu?
    /// </summary>
    public bool HasAck =>
        Status == TransportSendStatus.Acked;

    /// <summary>
    /// Bu sonuÃ§ timeout sayÄ±lÄ±yor mu?
    /// </summary>
    public bool IsTimeout =>
        Status == TransportSendStatus.Timeout;

    /// <summary>
    /// Bu sonuÃ§ baÅŸarÄ±sÄ±z sayÄ±lÄ±yor mu?
    /// </summary>
    public bool IsFailure =>
        Status is TransportSendStatus.Timeout
            or TransportSendStatus.LinkUnavailable
            or TransportSendStatus.RouteUnavailable
            or TransportSendStatus.Failed;

    /// <summary>
    /// GÃ¶nderim denemesinin baÅŸladÄ±ÄŸÄ± UTC zaman.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// GÃ¶nderim sonucunun Ã¼retildiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Ã–lÃ§Ã¼len gecikme.
    /// ACK varsa round-trip latency gibi dÃ¼ÅŸÃ¼nÃ¼lebilir.
    /// </summary>
    public double? LatencyMs { get; init; }

    /// <summary>
    /// Ä°nsan-okunabilir aÃ§Ä±klama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Transport seviyesindeki hata mesajÄ±.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Ek metadata alanÄ±.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// BaÅŸarÄ±lÄ± gÃ¶nderim sonucu Ã¼retir.
    /// </summary>
    public static TransportSendResult Sent(
        string messageId,
        string targetNodeId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        return new TransportSendResult
        {
            MessageId = messageId ?? string.Empty,
            TargetNodeId = targetNodeId ?? string.Empty,
            TransportKind = transportKind,
            Status = TransportSendStatus.Sent,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            LatencyMs = latencyMs,
            Reason = reason ?? "Message was sent through selected transport."
        };
    }

    /// <summary>
    /// ACK alÄ±nmÄ±ÅŸ gÃ¶nderim sonucu Ã¼retir.
    /// </summary>
    public static TransportSendResult Acked(
        string messageId,
        string targetNodeId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        return new TransportSendResult
        {
            MessageId = messageId ?? string.Empty,
            TargetNodeId = targetNodeId ?? string.Empty,
            TransportKind = transportKind,
            Status = TransportSendStatus.Acked,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            LatencyMs = latencyMs,
            Reason = reason ?? "Message was sent and ACK was received."
        };
    }

    /// <summary>
    /// Timeout sonucu Ã¼retir.
    /// </summary>
    public static TransportSendResult Timeout(
        string messageId,
        string targetNodeId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string? reason = null)
    {
        return new TransportSendResult
        {
            MessageId = messageId ?? string.Empty,
            TargetNodeId = targetNodeId ?? string.Empty,
            TransportKind = transportKind,
            Status = TransportSendStatus.Timeout,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            Reason = reason ?? "Message timed out while waiting for send result or ACK."
        };
    }

    /// <summary>
    /// BaÅŸarÄ±sÄ±z gÃ¶nderim sonucu Ã¼retir.
    /// </summary>
    public static TransportSendResult Failed(
        string messageId,
        string targetNodeId,
        TransportKind transportKind,
        TransportSendStatus status,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string reason,
        string? errorMessage = null)
    {
        if (status is TransportSendStatus.Sent or TransportSendStatus.Acked)
            status = TransportSendStatus.Failed;

        return new TransportSendResult
        {
            MessageId = messageId ?? string.Empty,
            TargetNodeId = targetNodeId ?? string.Empty,
            TransportKind = transportKind,
            Status = status,
            StartedUtc = startedUtc,
            CompletedUtc = completedUtc,
            Reason = reason,
            ErrorMessage = errorMessage
        };
    }
}
