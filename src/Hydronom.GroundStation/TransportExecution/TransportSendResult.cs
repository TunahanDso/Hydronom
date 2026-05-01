using Hydronom.Core.Communication;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Tek bir transport üzerinden yapılan gönderim denemesinin sonucudur.
/// 
/// Örnek:
/// - VEHICLE-ALPHA-001 hedefine Tcp üzerinden FleetCommand gönderildi.
/// - 24 ms sonra ACK geldi.
/// - Sonuç: Acked.
/// </summary>
public sealed record TransportSendResult
{
    /// <summary>
    /// Gönderim sonucuna ait benzersiz kayıt ID'si.
    /// </summary>
    public string SendResultId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// İlgili HydronomEnvelope mesaj ID'si.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node ID.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gönderim için kullanılan transport türü.
    /// </summary>
    public TransportKind TransportKind { get; init; }

    /// <summary>
    /// Gönderim sonucu.
    /// </summary>
    public TransportSendStatus Status { get; init; } = TransportSendStatus.Unknown;

    /// <summary>
    /// Gönderimin başarılı kabul edilip edilmediği.
    /// </summary>
    public bool Success =>
        Status is TransportSendStatus.Sent or TransportSendStatus.Acked;

    /// <summary>
    /// Bu sonuç ACK alındığını temsil ediyor mu?
    /// </summary>
    public bool HasAck =>
        Status == TransportSendStatus.Acked;

    /// <summary>
    /// Bu sonuç timeout sayılıyor mu?
    /// </summary>
    public bool IsTimeout =>
        Status == TransportSendStatus.Timeout;

    /// <summary>
    /// Bu sonuç başarısız sayılıyor mu?
    /// </summary>
    public bool IsFailure =>
        Status is TransportSendStatus.Timeout
            or TransportSendStatus.LinkUnavailable
            or TransportSendStatus.RouteUnavailable
            or TransportSendStatus.Failed;

    /// <summary>
    /// Gönderim denemesinin başladığı UTC zaman.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gönderim sonucunun üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Ölçülen gecikme.
    /// ACK varsa round-trip latency gibi düşünülebilir.
    /// </summary>
    public double? LatencyMs { get; init; }

    /// <summary>
    /// İnsan-okunabilir açıklama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Transport seviyesindeki hata mesajı.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Ek metadata alanı.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Başarılı gönderim sonucu üretir.
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
    /// ACK alınmış gönderim sonucu üretir.
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
    /// Timeout sonucu üretir.
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
    /// Başarısız gönderim sonucu üretir.
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