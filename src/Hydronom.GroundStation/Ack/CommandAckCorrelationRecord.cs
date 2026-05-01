namespace Hydronom.GroundStation.Ack;

using Hydronom.Core.Communication;

/// <summary>
/// Bir FleetCommand ile onun route execution kaydı arasındaki ACK/result korelasyon kaydıdır.
/// 
/// Bu kayıt şunu bağlar:
/// - CommandId
/// - MessageId
/// - ExecutionId
/// - Hedef araç
/// - Kullanılan transport
/// - Gelen FleetCommandResult durumu
/// 
/// Amaç:
/// SendAsync başarılı oldu diye ACK varsaymak yerine,
/// araçtan gerçekten FleetCommandResult geldiğinde ilgili route execution kaydını güncelleyebilmektir.
/// </summary>
public sealed class CommandAckCorrelationRecord
{
    /// <summary>
    /// Korelasyon kayıt ID'si.
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
    /// Komutu gönderen node ID'si.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gönderimde kullanılan veya tercih edilen transport türü.
    /// 
    /// Route üzerinde birden fazla candidate varsa ilk kullanılan/uygun transport burada tutulur.
    /// </summary>
    public TransportKind TransportKind { get; init; } = TransportKind.Unknown;

    /// <summary>
    /// Korelasyon kaydının oluşturulduğu UTC zaman.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// İlk ACK/result alındığı UTC zaman.
    /// </summary>
    public DateTimeOffset? AckReceivedUtc { get; private set; }

    /// <summary>
    /// Son result alındığı UTC zaman.
    /// </summary>
    public DateTimeOffset? LastResultUtc { get; private set; }

    /// <summary>
    /// Son command result status değeri.
    /// 
    /// Örnek:
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
    /// Son result mesajı.
    /// </summary>
    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Bu komut için araçtan herhangi bir sonuç geldi mi?
    /// </summary>
    public bool IsAcked => AckReceivedUtc.HasValue;

    /// <summary>
    /// Komut sonuçlandı mı?
    /// </summary>
    public bool IsCompleted =>
        string.Equals(LastStatus, "Applied", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Expired", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Timeout", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Komut başarılı sonuçlandı mı?
    /// </summary>
    public bool IsSuccessful =>
        string.Equals(LastStatus, "Accepted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Applied", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LastStatus, "Completed", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Komut başarısız sonuçlandı mı?
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
    /// FleetCommandResult bilgisini korelasyon kaydına uygular.
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