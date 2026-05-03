using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Bir route kararÄ±nÄ±n gÃ¶nderim aÅŸamasÄ±ndaki takip kaydÄ±dÄ±r.
/// 
/// CommunicationRouter route kararÄ±nÄ± Ã¼retir.
/// GroundTransportExecutionTracker ise bu route kararÄ±nÄ±n gÃ¶nderim sonucunu takip eder.
/// </summary>
public sealed class RouteExecutionRecord
{
    private readonly List<TransportSendResult> _sendResults = new();

    public RouteExecutionRecord(
        HydronomEnvelope envelope,
        CommunicationRouteResult routeResult,
        DateTimeOffset createdUtc)
    {
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        RouteResult = routeResult ?? throw new ArgumentNullException(nameof(routeResult));
        CreatedUtc = createdUtc;
        LastUpdatedUtc = createdUtc;
    }

    /// <summary>
    /// Route execution kayÄ±t ID'si.
    /// </summary>
    public string ExecutionId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// GÃ¶nderilmeye Ã§alÄ±ÅŸÄ±lan envelope.
    /// </summary>
    public HydronomEnvelope Envelope { get; }

    /// <summary>
    /// Bu envelope iÃ§in Ã¼retilmiÅŸ route sonucu.
    /// </summary>
    public CommunicationRouteResult RouteResult { get; }

    /// <summary>
    /// KayÄ±t oluÅŸturulma zamanÄ±.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Son gÃ¼ncellenme zamanÄ±.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; private set; }

    /// <summary>
    /// Route execution tamamlandÄ± mÄ±?
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// KullanÄ±lan veya denenmesi planlanan transport tÃ¼rleri.
    /// </summary>
    public IReadOnlyList<TransportKind> CandidateTransports =>
        RouteResult.PrimaryTransports
            .Concat(RouteResult.FallbackTransports)
            .Distinct()
            .ToArray();

    /// <summary>
    /// GÃ¶nderim sonuÃ§larÄ±.
    /// </summary>
    public IReadOnlyList<TransportSendResult> SendResults => _sendResults.ToArray();

    /// <summary>
    /// BaÅŸarÄ±lÄ± gÃ¶nderim sonucu var mÄ±?
    /// </summary>
    public bool HasSuccess => _sendResults.Any(x => x.Success);

    /// <summary>
    /// ACK alÄ±nmÄ±ÅŸ sonuÃ§ var mÄ±?
    /// </summary>
    public bool HasAck => _sendResults.Any(x => x.HasAck);

    /// <summary>
    /// Timeout sonucu var mÄ±?
    /// </summary>
    public bool HasTimeout => _sendResults.Any(x => x.IsTimeout);

    /// <summary>
    /// BaÅŸarÄ±sÄ±z sonuÃ§ var mÄ±?
    /// </summary>
    public bool HasFailure => _sendResults.Any(x => x.IsFailure);

    /// <summary>
    /// Son durum.
    /// </summary>
    public TransportSendStatus LastStatus =>
        _sendResults.Count == 0
            ? TransportSendStatus.Pending
            : _sendResults[^1].Status;

    /// <summary>
    /// En iyi Ã¶lÃ§Ã¼len latency deÄŸeri.
    /// </summary>
    public double? BestLatencyMs =>
        _sendResults
            .Where(x => x.LatencyMs.HasValue)
            .Select(x => x.LatencyMs!.Value)
            .DefaultIfEmpty()
            .Min() is var value && value > 0
                ? value
                : null;

    /// <summary>
    /// Execution kaydÄ±na transport sonucu ekler.
    /// </summary>
    public void AddResult(TransportSendResult result)
    {
        if (result is null)
            return;

        _sendResults.Add(result);
        LastUpdatedUtc = result.CompletedUtc;

        if (result.Success || result.IsFailure)
            IsCompleted = true;
    }

    /// <summary>
    /// Execution kaydÄ±nÄ± manuel tamamlanmÄ±ÅŸ iÅŸaretler.
    /// </summary>
    public void MarkCompleted(DateTimeOffset nowUtc)
    {
        IsCompleted = true;
        LastUpdatedUtc = nowUtc;
    }
}
