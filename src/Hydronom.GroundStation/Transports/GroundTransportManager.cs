namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;
using Hydronom.GroundStation.TransportExecution;

/// <summary>
/// Ground Station tarafında route kararını gerçek transport gönderimine bağlayan manager sınıfıdır.
/// 
/// Bu sınıf:
/// - Envelope için route sonucu üretmez, dışarıdan route sonucu alır.
/// - Route sonucundaki candidate transport'lara göre registry'den transport bulur.
/// - ITransport.SendAsync çağırır.
/// - Send sonucunu GroundTransportExecutionTracker'a işler.
/// - Timeout / exception durumlarını standart TransportSendResult akışına dönüştürür.
/// 
/// Böylece mevcut ITransport arayüzünü değiştirmeden gerçek gönderim zinciri kurulmuş olur.
/// </summary>
public sealed class GroundTransportManager
{
    private readonly GroundTransportRegistry _registry;
    private readonly GroundTransportExecutionTracker _executionTracker;

    public GroundTransportManager(
        GroundTransportRegistry registry,
        GroundTransportExecutionTracker executionTracker)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executionTracker = executionTracker ?? throw new ArgumentNullException(nameof(executionTracker));
    }

    /// <summary>
    /// Kayıtlı transport registry.
    /// </summary>
    public GroundTransportRegistry Registry => _registry;

    /// <summary>
    /// Envelope ve route sonucundan gönderim planı üretir.
    /// </summary>
    public GroundTransportSendPlan BuildPlan(
        HydronomEnvelope envelope,
        CommunicationRouteResult routeResult,
        GroundTransportSendRequest request)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        if (routeResult is null)
            throw new ArgumentNullException(nameof(routeResult));

        request ??= new GroundTransportSendRequest
        {
            Envelope = envelope
        };

        return GroundTransportSendPlan.FromRoute(
            envelope,
            routeResult,
            request.TryFallbacks);
    }

    /// <summary>
    /// Route sonucu üzerinden envelope göndermeye çalışır.
    /// 
    /// Bu metot:
    /// - execution kaydı başlatır,
    /// - candidate transport seçer,
    /// - SendAsync çağırır,
    /// - sonucu execution tracker'a işler,
    /// - execution kaydını döndürür.
    /// </summary>
    public async Task<RouteExecutionRecord> SendAsync(
        GroundTransportSendRequest request,
        CommunicationRouteResult routeResult,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.Envelope is null)
            throw new ArgumentException("Request envelope boş olamaz.", nameof(request));

        if (routeResult is null)
            throw new ArgumentNullException(nameof(routeResult));

        var startedUtc = DateTimeOffset.UtcNow;

        var execution = _executionTracker.BeginExecution(
            request.Envelope,
            routeResult,
            startedUtc);

        if (!routeResult.CanRoute)
            return execution;

        var plan = BuildPlan(
            request.Envelope,
            routeResult,
            request);

        if (!plan.CanSend)
        {
            _executionTracker.RecordFailure(
                execution.ExecutionId,
                TransportKind.Unknown,
                TransportSendStatus.RouteUnavailable,
                startedUtc,
                DateTimeOffset.UtcNow,
                "Send plan has no candidate transport.");

            return execution;
        }

        var transports = SelectTransports(plan, request);

        if (transports.Count == 0)
        {
            _executionTracker.RecordFailure(
                execution.ExecutionId,
                TransportKind.Unknown,
                TransportSendStatus.LinkUnavailable,
                startedUtc,
                DateTimeOffset.UtcNow,
                "No connected transport instance found for route candidates.");

            return execution;
        }

        foreach (var transport in transports)
        {
            var transportStart = DateTimeOffset.UtcNow;

            _executionTracker.RecordSendAttempt(
                execution.ExecutionId,
                transport.Kind,
                transportStart);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(request.SendTimeout);

                await transport.SendAsync(
                    request.Envelope,
                    timeoutCts.Token);

                var completedUtc = DateTimeOffset.UtcNow;
                var latencyMs = Math.Max(0, (completedUtc - transportStart).TotalMilliseconds);

                if (plan.RequiresAck && request.TreatSuccessfulSendAsAckWhenRequired)
                {
                    _executionTracker.RecordAcked(
                        execution.ExecutionId,
                        transport.Kind,
                        transportStart,
                        completedUtc,
                        latencyMs,
                        $"Transport '{transport.Name}' sent message and simulated ACK was accepted.");
                }
                else
                {
                    _executionTracker.RecordSent(
                        execution.ExecutionId,
                        transport.Kind,
                        transportStart,
                        completedUtc,
                        latencyMs,
                        $"Transport '{transport.Name}' sent message successfully.");
                }

                if (!plan.IsBroadcast || !request.SendToAllForBroadcast)
                    break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _executionTracker.RecordTimeout(
                    execution.ExecutionId,
                    transport.Kind,
                    transportStart,
                    DateTimeOffset.UtcNow,
                    $"Transport '{transport.Name}' send timed out after {request.SendTimeout.TotalMilliseconds:0} ms.");

                if (!request.TryFallbacks)
                    break;
            }
            catch (Exception ex)
            {
                _executionTracker.RecordFailure(
                    execution.ExecutionId,
                    transport.Kind,
                    TransportSendStatus.Failed,
                    transportStart,
                    DateTimeOffset.UtcNow,
                    $"Transport '{transport.Name}' send failed.",
                    ex.Message);

                if (!request.TryFallbacks)
                    break;
            }
        }

        return execution;
    }

    /// <summary>
    /// Gönderim planına göre kullanılacak bağlı transport instance'larını seçer.
    /// </summary>
    private IReadOnlyList<ITransport> SelectTransports(
        GroundTransportSendPlan plan,
        GroundTransportSendRequest request)
    {
        if (plan.CandidateTransports.Count == 0)
            return Array.Empty<ITransport>();

        if (plan.IsBroadcast && request.SendToAllForBroadcast)
            return _registry.FindConnected(plan.CandidateTransports);

        var first = _registry.FindFirstConnected(plan.CandidateTransports);

        return first is null
            ? Array.Empty<ITransport>()
            : new[] { first };
    }
}