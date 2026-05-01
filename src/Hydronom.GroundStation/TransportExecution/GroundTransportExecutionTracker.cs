using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.LinkHealth;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Ground Station tarafında route execution / gönderim sonucu takibi yapan çekirdek sınıftır.
/// 
/// Bu sınıf gerçek transport gönderimi yapmaz.
/// Görevi:
/// - CommunicationRouter tarafından üretilmiş route sonucunu kaydetmek,
/// - Seçilen transport üzerinden gönderim denemesini takip etmek,
/// - TransportSendResult sonuçlarını saklamak,
/// - LinkHealthTracker'ı otomatik güncellemek,
/// - Diagnostics ve smoke test için snapshot üretmektir.
/// 
/// Gerçek ITransport implementasyonları geldiğinde bu sınıf:
/// router → transport send → result → link health
/// zincirinin merkezi olacaktır.
/// </summary>
public sealed class GroundTransportExecutionTracker
{
    private readonly Dictionary<string, RouteExecutionRecord> _recordsByExecutionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RouteExecutionRecord> _recordsByMessageId = new(StringComparer.OrdinalIgnoreCase);

    private readonly LinkHealthTracker _linkHealthTracker;

    public GroundTransportExecutionTracker(LinkHealthTracker linkHealthTracker)
    {
        _linkHealthTracker = linkHealthTracker ?? throw new ArgumentNullException(nameof(linkHealthTracker));
    }

    /// <summary>
    /// Kayıtlı route execution sayısı.
    /// </summary>
    public int Count => _recordsByExecutionId.Count;

    /// <summary>
    /// Verilen route sonucundan execution kaydı başlatır.
    /// 
    /// Route edilemeyen sonuçlar da kaydedilir.
    /// Böylece diagnostics tarafında başarısız route kararları da görülebilir.
    /// </summary>
    public RouteExecutionRecord BeginExecution(
        HydronomEnvelope envelope,
        CommunicationRouteResult routeResult,
        DateTimeOffset? nowUtc = null)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        if (routeResult is null)
            throw new ArgumentNullException(nameof(routeResult));

        var createdUtc = nowUtc ?? DateTimeOffset.UtcNow;
        var record = new RouteExecutionRecord(envelope, routeResult, createdUtc);

        _recordsByExecutionId[record.ExecutionId] = record;

        if (!string.IsNullOrWhiteSpace(envelope.MessageId))
            _recordsByMessageId[envelope.MessageId] = record;

        if (!routeResult.CanRoute)
        {
            var failedResult = TransportSendResult.Failed(
                messageId: envelope.MessageId,
                targetNodeId: envelope.TargetNodeId,
                transportKind: TransportKind.Unknown,
                status: TransportSendStatus.RouteUnavailable,
                startedUtc: createdUtc,
                completedUtc: createdUtc,
                reason: routeResult.Reason);

            record.AddResult(failedResult);
        }

        return record;
    }

    /// <summary>
    /// Belirli execution için gönderim denemesi başladığını kaydeder.
    /// 
    /// Bu metot LinkHealthTracker üzerinde send sayacını artırır.
    /// </summary>
    public bool RecordSendAttempt(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset? nowUtc = null)
    {
        var record = GetRecord(executionId);

        if (record is null)
            return false;

        var now = nowUtc ?? DateTimeOffset.UtcNow;

        _linkHealthTracker.RecordSend(
            record.Envelope.TargetNodeId,
            transportKind,
            now.UtcDateTime);

        return true;
    }

    /// <summary>
    /// Başarılı gönderim sonucunu kaydeder.
    /// 
    /// Bu metot aynı zamanda LinkHealthTracker üzerinde route success metriğini günceller.
    /// </summary>
    public bool RecordSent(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        var record = GetRecord(executionId);

        if (record is null)
            return false;

        var result = TransportSendResult.Sent(
            record.Envelope.MessageId,
            record.Envelope.TargetNodeId,
            transportKind,
            startedUtc,
            completedUtc,
            latencyMs,
            reason);

        record.AddResult(result);

        _linkHealthTracker.RecordRouteSuccess(
            record.Envelope.TargetNodeId,
            transportKind,
            completedUtc.UtcDateTime,
            latencyMs);

        return true;
    }

    /// <summary>
    /// ACK alınmış gönderim sonucunu kaydeder.
    /// 
    /// Bu metot LinkHealthTracker üzerinde hem route success hem ACK metriğini günceller.
    /// </summary>
    public bool RecordAcked(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        double? latencyMs = null,
        string? reason = null)
    {
        var record = GetRecord(executionId);

        if (record is null)
            return false;

        var result = TransportSendResult.Acked(
            record.Envelope.MessageId,
            record.Envelope.TargetNodeId,
            transportKind,
            startedUtc,
            completedUtc,
            latencyMs,
            reason);

        record.AddResult(result);

        _linkHealthTracker.RecordRouteSuccess(
            record.Envelope.TargetNodeId,
            transportKind,
            completedUtc.UtcDateTime,
            latencyMs);

        _linkHealthTracker.RecordAck(
            record.Envelope.TargetNodeId,
            transportKind,
            completedUtc.UtcDateTime,
            latencyMs);

        return true;
    }

    /// <summary>
    /// Timeout sonucunu kaydeder.
    /// 
    /// Bu metot LinkHealthTracker üzerinde timeout metriğini günceller.
    /// </summary>
    public bool RecordTimeout(
        string executionId,
        TransportKind transportKind,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string? reason = null)
    {
        var record = GetRecord(executionId);

        if (record is null)
            return false;

        var result = TransportSendResult.Timeout(
            record.Envelope.MessageId,
            record.Envelope.TargetNodeId,
            transportKind,
            startedUtc,
            completedUtc,
            reason);

        record.AddResult(result);

        _linkHealthTracker.RecordTimeout(
            record.Envelope.TargetNodeId,
            transportKind,
            completedUtc.UtcDateTime);

        return true;
    }

    /// <summary>
    /// Başarısız gönderim sonucunu kaydeder.
    /// 
    /// Bu metot LinkHealthTracker üzerinde route failure metriğini günceller.
    /// </summary>
    public bool RecordFailure(
        string executionId,
        TransportKind transportKind,
        TransportSendStatus status,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string reason,
        string? errorMessage = null)
    {
        var record = GetRecord(executionId);

        if (record is null)
            return false;

        var result = TransportSendResult.Failed(
            record.Envelope.MessageId,
            record.Envelope.TargetNodeId,
            transportKind,
            status,
            startedUtc,
            completedUtc,
            reason,
            errorMessage);

        record.AddResult(result);

        _linkHealthTracker.RecordRouteFailure(
            record.Envelope.TargetNodeId,
            transportKind,
            completedUtc.UtcDateTime);

        return true;
    }

    /// <summary>
    /// Execution ID ile kayıt döndürür.
    /// </summary>
    public RouteExecutionRecord? GetRecord(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            return null;

        return _recordsByExecutionId.TryGetValue(executionId, out var record)
            ? record
            : null;
    }

    /// <summary>
    /// MessageId ile kayıt döndürür.
    /// </summary>
    public RouteExecutionRecord? GetRecordByMessageId(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return _recordsByMessageId.TryGetValue(messageId, out var record)
            ? record
            : null;
    }

    /// <summary>
    /// Tüm route execution kayıtlarının snapshot listesini döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetSnapshot()
    {
        return _recordsByExecutionId.Values
            .OrderByDescending(x => x.LastUpdatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    /// <summary>
    /// Pending durumdaki execution kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetPendingSnapshot()
    {
        return _recordsByExecutionId.Values
            .Where(x => !x.IsCompleted)
            .OrderByDescending(x => x.LastUpdatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    /// <summary>
    /// Başarısız execution kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetFailedSnapshot()
    {
        return _recordsByExecutionId.Values
            .Where(x => x.HasFailure)
            .OrderByDescending(x => x.LastUpdatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    /// <summary>
    /// Route execution kaydını snapshot'a dönüştürür.
    /// </summary>
    private static RouteExecutionSnapshot ToSnapshot(RouteExecutionRecord record)
    {
        return new RouteExecutionSnapshot
        {
            ExecutionId = record.ExecutionId,
            MessageId = record.Envelope.MessageId,
            MessageType = record.Envelope.MessageType,
            SourceNodeId = record.Envelope.SourceNodeId,
            TargetNodeId = record.Envelope.TargetNodeId,
            CanRoute = record.RouteResult.CanRoute,
            IsCompleted = record.IsCompleted,
            HasSuccess = record.HasSuccess,
            HasAck = record.HasAck,
            HasTimeout = record.HasTimeout,
            HasFailure = record.HasFailure,
            LastStatus = record.LastStatus,
            BestLatencyMs = record.BestLatencyMs,
            CandidateTransports = record.CandidateTransports,
            SendResults = record.SendResults,
            CreatedUtc = record.CreatedUtc,
            LastUpdatedUtc = record.LastUpdatedUtc
        };
    }
}