using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.LinkHealth;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Ground Station tarafÄ±nda route execution / gÃ¶nderim sonucu takibi yapan Ã§ekirdek sÄ±nÄ±ftÄ±r.
/// 
/// Bu sÄ±nÄ±f gerÃ§ek transport gÃ¶nderimi yapmaz.
/// GÃ¶revi:
/// - CommunicationRouter tarafÄ±ndan Ã¼retilmiÅŸ route sonucunu kaydetmek,
/// - SeÃ§ilen transport Ã¼zerinden gÃ¶nderim denemesini takip etmek,
/// - TransportSendResult sonuÃ§larÄ±nÄ± saklamak,
/// - LinkHealthTracker'Ä± otomatik gÃ¼ncellemek,
/// - Diagnostics ve smoke test iÃ§in snapshot Ã¼retmektir.
/// 
/// GerÃ§ek ITransport implementasyonlarÄ± geldiÄŸinde bu sÄ±nÄ±f:
/// router â†’ transport send â†’ result â†’ link health
/// zincirinin merkezi olacaktÄ±r.
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
    /// KayÄ±tlÄ± route execution sayÄ±sÄ±.
    /// </summary>
    public int Count => _recordsByExecutionId.Count;

    /// <summary>
    /// Verilen route sonucundan execution kaydÄ± baÅŸlatÄ±r.
    /// 
    /// Route edilemeyen sonuÃ§lar da kaydedilir.
    /// BÃ¶ylece diagnostics tarafÄ±nda baÅŸarÄ±sÄ±z route kararlarÄ± da gÃ¶rÃ¼lebilir.
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
    /// Belirli execution iÃ§in gÃ¶nderim denemesi baÅŸladÄ±ÄŸÄ±nÄ± kaydeder.
    /// 
    /// Bu metot LinkHealthTracker Ã¼zerinde send sayacÄ±nÄ± artÄ±rÄ±r.
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
    /// BaÅŸarÄ±lÄ± gÃ¶nderim sonucunu kaydeder.
    /// 
    /// Bu metot aynÄ± zamanda LinkHealthTracker Ã¼zerinde route success metriÄŸini gÃ¼nceller.
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
    /// ACK alÄ±nmÄ±ÅŸ gÃ¶nderim sonucunu kaydeder.
    /// 
    /// Bu metot LinkHealthTracker Ã¼zerinde hem route success hem ACK metriÄŸini gÃ¼nceller.
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
    /// Bu metot LinkHealthTracker Ã¼zerinde timeout metriÄŸini gÃ¼nceller.
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
    /// BaÅŸarÄ±sÄ±z gÃ¶nderim sonucunu kaydeder.
    /// 
    /// Bu metot LinkHealthTracker Ã¼zerinde route failure metriÄŸini gÃ¼nceller.
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
    /// Execution ID ile kayÄ±t dÃ¶ndÃ¼rÃ¼r.
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
    /// MessageId ile kayÄ±t dÃ¶ndÃ¼rÃ¼r.
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
    /// TÃ¼m route execution kayÄ±tlarÄ±nÄ±n snapshot listesini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<RouteExecutionSnapshot> GetSnapshot()
    {
        return _recordsByExecutionId.Values
            .OrderByDescending(x => x.LastUpdatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    /// <summary>
    /// Pending durumdaki execution kayÄ±tlarÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// BaÅŸarÄ±sÄ±z execution kayÄ±tlarÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
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
    /// Route execution kaydÄ±nÄ± snapshot'a dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
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
