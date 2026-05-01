namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

/// <summary>
/// GroundTransportManager'ın bir envelope için oluşturduğu gönderim planıdır.
/// 
/// Plan:
/// - Route sonucunu,
/// - Denenecek transport türlerini,
/// - ACK gerekip gerekmediğini,
/// - Broadcast davranışını
/// tek modelde toplar.
/// </summary>
public sealed record GroundTransportSendPlan
{
    /// <summary>
    /// Gönderilecek envelope mesaj ID'si.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node ID.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Route sonucu.
    /// </summary>
    public CommunicationRouteResult? RouteResult { get; init; }

    /// <summary>
    /// Denenecek transport türleri.
    /// </summary>
    public IReadOnlyList<TransportKind> CandidateTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Mesaj ACK gerektiriyor mu?
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// Broadcast gönderimi mi?
    /// </summary>
    public bool IsBroadcast { get; init; }

    /// <summary>
    /// Plan gönderilebilir mi?
    /// </summary>
    public bool CanSend =>
        RouteResult?.CanRoute == true && CandidateTransports.Count > 0;

    /// <summary>
    /// Plan açıklaması.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Route sonucundan gönderim planı üretir.
    /// </summary>
    public static GroundTransportSendPlan FromRoute(
        HydronomEnvelope envelope,
        CommunicationRouteResult routeResult,
        bool tryFallbacks)
    {
        var candidates = routeResult.PrimaryTransports
            .Concat(tryFallbacks ? routeResult.FallbackTransports : Array.Empty<TransportKind>())
            .Distinct()
            .ToArray();

        return new GroundTransportSendPlan
        {
            MessageId = envelope.MessageId,
            TargetNodeId = envelope.TargetNodeId,
            RouteResult = routeResult,
            CandidateTransports = candidates,
            RequiresAck = routeResult.RequiresAck,
            IsBroadcast = envelope.IsBroadcast || routeResult.BroadcastAllAvailableLinks,
            Reason = routeResult.Reason
        };
    }
}