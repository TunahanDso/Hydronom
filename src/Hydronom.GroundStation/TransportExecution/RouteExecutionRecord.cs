using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

namespace Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Bir route kararının gönderim aşamasındaki takip kaydıdır.
/// 
/// CommunicationRouter route kararını üretir.
/// GroundTransportExecutionTracker ise bu route kararının gönderim sonucunu takip eder.
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
    /// Route execution kayıt ID'si.
    /// </summary>
    public string ExecutionId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gönderilmeye çalışılan envelope.
    /// </summary>
    public HydronomEnvelope Envelope { get; }

    /// <summary>
    /// Bu envelope için üretilmiş route sonucu.
    /// </summary>
    public CommunicationRouteResult RouteResult { get; }

    /// <summary>
    /// Kayıt oluşturulma zamanı.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Son güncellenme zamanı.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; private set; }

    /// <summary>
    /// Route execution tamamlandı mı?
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Kullanılan veya denenmesi planlanan transport türleri.
    /// </summary>
    public IReadOnlyList<TransportKind> CandidateTransports =>
        RouteResult.PrimaryTransports
            .Concat(RouteResult.FallbackTransports)
            .Distinct()
            .ToArray();

    /// <summary>
    /// Gönderim sonuçları.
    /// </summary>
    public IReadOnlyList<TransportSendResult> SendResults => _sendResults.ToArray();

    /// <summary>
    /// Başarılı gönderim sonucu var mı?
    /// </summary>
    public bool HasSuccess => _sendResults.Any(x => x.Success);

    /// <summary>
    /// ACK alınmış sonuç var mı?
    /// </summary>
    public bool HasAck => _sendResults.Any(x => x.HasAck);

    /// <summary>
    /// Timeout sonucu var mı?
    /// </summary>
    public bool HasTimeout => _sendResults.Any(x => x.IsTimeout);

    /// <summary>
    /// Başarısız sonuç var mı?
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
    /// En iyi ölçülen latency değeri.
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
    /// Execution kaydına transport sonucu ekler.
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
    /// Execution kaydını manuel tamamlanmış işaretler.
    /// </summary>
    public void MarkCompleted(DateTimeOffset nowUtc)
    {
        IsCompleted = true;
        LastUpdatedUtc = nowUtc;
    }
}