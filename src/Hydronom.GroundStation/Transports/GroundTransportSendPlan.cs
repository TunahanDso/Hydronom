namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Communication;

/// <summary>
/// GroundTransportManager'Ä±n bir envelope iÃ§in oluÅŸturduÄŸu gÃ¶nderim planÄ±dÄ±r.
/// 
/// Plan:
/// - Route sonucunu,
/// - Denenecek transport tÃ¼rlerini,
/// - ACK gerekip gerekmediÄŸini,
/// - Broadcast davranÄ±ÅŸÄ±nÄ±
/// tek modelde toplar.
/// </summary>
public sealed record GroundTransportSendPlan
{
    /// <summary>
    /// GÃ¶nderilecek envelope mesaj ID'si.
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
    /// Denenecek transport tÃ¼rleri.
    /// </summary>
    public IReadOnlyList<TransportKind> CandidateTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// Mesaj ACK gerektiriyor mu?
    /// </summary>
    public bool RequiresAck { get; init; }

    /// <summary>
    /// Broadcast gÃ¶nderimi mi?
    /// </summary>
    public bool IsBroadcast { get; init; }

    /// <summary>
    /// Plan gÃ¶nderilebilir mi?
    /// </summary>
    public bool CanSend =>
        RouteResult?.CanRoute == true && CandidateTransports.Count > 0;

    /// <summary>
    /// Plan aÃ§Ä±klamasÄ±.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Route sonucundan gÃ¶nderim planÄ± Ã¼retir.
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
