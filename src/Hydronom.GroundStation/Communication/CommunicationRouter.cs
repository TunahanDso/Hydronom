namespace Hydronom.GroundStation.Communication;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Routing;

/// <summary>
/// Ground Station tarafÄ±nda envelope mesajlarÄ±nÄ± gÃ¶nderim Ã¶ncesi route eden sÄ±nÄ±ftÄ±r.
/// 
/// Bu sÄ±nÄ±f ÅŸimdilik gerÃ§ek TCP/LoRa/WebSocket gÃ¶nderimi yapmaz.
/// Ä°lk faz gÃ¶revi:
/// - Envelope iÃ§in policy route kararÄ± Ã¼retmek,
/// - Hedef node FleetRegistry snapshot iÃ§inde var mÄ± bakmak,
/// - Hedef node'un AvailableTransports listesine gÃ¶re route'u filtrelemek,
/// - MesajÄ±n gÃ¶nderilebilir olup olmadÄ±ÄŸÄ±nÄ± sÃ¶ylemektir.
/// 
/// Yeni LinkHealth hazÄ±rlÄ±ÄŸÄ±:
/// - Router artÄ±k opsiyonel link uygunluk filtresi alabilir.
/// - Bu filtre sayesinde ileride LinkHealthTracker Ã¼zerinden
///   kÃ¶tÃ¼, critical veya lost linkler route kararÄ±ndan elenebilir.
/// - BÃ¶ylece CommunicationRouter ileride kalite skoru tabanlÄ± route kararÄ±na geÃ§ebilir.
/// 
/// Ä°leride bu sÄ±nÄ±fÄ±n Ã¼zerine:
/// - TransportManager,
/// - ITransport implementasyonlarÄ±,
/// - retry/ACK tracking,
/// - link quality scoring,
/// - send queue,
/// - emergency broadcast fan-out
/// eklenecektir.
/// </summary>
public sealed class CommunicationRouter
{
    /// <summary>
    /// Mesaj tipine ve priority deÄŸerine gÃ¶re teorik route kararÄ± Ã¼reten policy.
    /// </summary>
    private readonly TransportRoutingPolicy _routingPolicy;

    /// <summary>
    /// Policy kararÄ±nÄ± hedef node'un gerÃ§ek transport listesine gÃ¶re filtreleyen yardÄ±mcÄ±.
    /// </summary>
    private readonly AvailableTransportFilter _transportFilter;

    /// <summary>
    /// CommunicationRouter oluÅŸturur.
    /// 
    /// DÄ±ÅŸarÄ±dan policy/filter verilebilir.
    /// Verilmezse varsayÄ±lan implementasyonlar kullanÄ±lÄ±r.
    /// </summary>
    public CommunicationRouter(
        TransportRoutingPolicy? routingPolicy = null,
        AvailableTransportFilter? transportFilter = null)
    {
        _routingPolicy = routingPolicy ?? new TransportRoutingPolicy();
        _transportFilter = transportFilter ?? new AvailableTransportFilter();
    }

    /// <summary>
    /// Verilen envelope iÃ§in mevcut fleet snapshot Ã¼zerinden route sonucu Ã¼retir.
    /// 
    /// Normal hedefli mesajlarda TargetNodeId Ã¼zerinden hedef araÃ§ aranÄ±r.
    /// Broadcast mesajlarda ise:
    /// - TargetNodeId "BROADCAST" olabilir,
    /// - Emergency priority olabilir,
    /// - BroadcastAllAvailableLinks true olabilir.
    /// 
    /// Ä°lk fazda broadcast iÃ§in tÃ¼m filodaki transport'lar birleÅŸtirilerek uygulanabilir route Ã§Ä±karÄ±lÄ±r.
    /// 
    /// Bu overload eski davranÄ±ÅŸÄ± korur.
    /// Link health filtresi uygulanmaz.
    /// </summary>
    public CommunicationRouteResult Route(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot)
    {
        return Route(
            envelope,
            fleetSnapshot,
            linkAvailabilityFilter: null);
    }

    /// <summary>
    /// Verilen envelope iÃ§in mevcut fleet snapshot ve opsiyonel link uygunluk filtresi Ã¼zerinden route sonucu Ã¼retir.
    /// 
    /// linkAvailabilityFilter:
    /// - vehicleId ve transportKind alÄ±r,
    /// - true dÃ¶nerse link kullanÄ±labilir kabul edilir,
    /// - false dÃ¶nerse o transport route adaylarÄ±ndan Ã§Ä±karÄ±lÄ±r.
    /// 
    /// Bu yapÄ± LinkHealthTracker'a doÄŸrudan baÄŸÄ±mlÄ±lÄ±k kurmadan link-aware routing zemini hazÄ±rlar.
    /// BÃ¶ylece CommunicationRouter saf route motoru olarak kalÄ±r,
    /// LinkHealthTracker ise GroundStationEngine tarafÄ±ndan dÄ±ÅŸarÄ±dan baÄŸlanabilir.
    /// </summary>
    public CommunicationRouteResult Route(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        Func<string, TransportKind, bool>? linkAvailabilityFilter)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        var policyDecision = _routingPolicy.Decide(envelope);

        if (fleetSnapshot is null || fleetSnapshot.Count == 0)
        {
            return CommunicationRouteResult.Failed(
                envelope,
                "Fleet snapshot is empty; target transports are unknown.",
                targetKnown: false,
                policyDecision: policyDecision);
        }

        if (IsBroadcastEnvelope(envelope, policyDecision))
        {
            return RouteBroadcast(
                envelope,
                fleetSnapshot,
                policyDecision,
                linkAvailabilityFilter);
        }

        return RouteSingleTarget(
            envelope,
            fleetSnapshot,
            policyDecision,
            linkAvailabilityFilter);
    }

    /// <summary>
    /// Tek hedefli mesaj iÃ§in route sonucu Ã¼retir.
    /// 
    /// EÄŸer linkAvailabilityFilter verilmiÅŸse hedef aracÄ±n AvailableTransports listesi
    /// Ã¶nce bu filtreye gÃ¶re daraltÄ±lÄ±r.
    /// ArdÄ±ndan mevcut AvailableTransportFilter ile policy kararÄ± uygulanÄ±r.
    /// </summary>
    private CommunicationRouteResult RouteSingleTarget(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        TransportRouteDecision policyDecision,
        Func<string, TransportKind, bool>? linkAvailabilityFilter)
    {
        var target = fleetSnapshot.FirstOrDefault(x =>
            string.Equals(
                x.Identity.NodeId,
                envelope.TargetNodeId,
                StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return CommunicationRouteResult.Failed(
                envelope,
                "Target node was not found in fleet snapshot.",
                targetKnown: false,
                policyDecision: policyDecision);
        }

        var availableTransports = ApplyLinkAvailabilityFilter(
            target.Identity.NodeId,
            target.AvailableTransports,
            linkAvailabilityFilter);

        var filteredDecision = _transportFilter.Filter(
            policyDecision,
            availableTransports);

        var applicable = _transportFilter.IsApplicable(filteredDecision);

        if (!applicable)
        {
            var reason = linkAvailabilityFilter is null
                ? "Target node is known but no applicable transport remains after filtering."
                : "Target node is known but no applicable healthy transport remains after link-aware filtering.";

            return CommunicationRouteResult.Failed(
                envelope,
                reason,
                targetKnown: true,
                targetAvailableTransports: availableTransports,
                policyDecision: policyDecision,
                filteredDecision: filteredDecision);
        }

        var successReason = linkAvailabilityFilter is null
            ? "Route resolved for target node."
            : "Route resolved for target node with link-aware filtering.";

        return CommunicationRouteResult.Succeeded(
            envelope,
            successReason,
            targetKnown: true,
            targetAvailableTransports: availableTransports,
            policyDecision: policyDecision,
            filteredDecision: filteredDecision);
    }

    /// <summary>
    /// Broadcast mesajlar iÃ§in route sonucu Ã¼retir.
    /// 
    /// Ä°lk fazda tÃ¼m online araÃ§larÄ±n AvailableTransports listeleri birleÅŸtirilir.
    /// BÃ¶ylece broadcast iÃ§in pratikte kullanÄ±labilecek transport tÃ¼rleri bulunur.
    /// 
    /// EÄŸer linkAvailabilityFilter verilmiÅŸse her online araÃ§ iÃ§in transport listesi
    /// link saÄŸlÄ±ÄŸÄ±na gÃ¶re daraltÄ±lÄ±r ve sonra union alÄ±nÄ±r.
    /// 
    /// Not:
    /// GerÃ§ek implementasyonda her node iÃ§in ayrÄ± route sonucu Ã¼retmek daha doÄŸru olacaktÄ±r.
    /// Bu ilk Ã§ekirdek sadece toplam route uygulanabilirliÄŸini gÃ¶sterir.
    /// </summary>
    private CommunicationRouteResult RouteBroadcast(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        TransportRouteDecision policyDecision,
        Func<string, TransportKind, bool>? linkAvailabilityFilter)
    {
        var onlineNodes = fleetSnapshot
            .Where(x => x.IsValid && x.IsOnline)
            .ToArray();

        if (onlineNodes.Length == 0)
        {
            return CommunicationRouteResult.Failed(
                envelope,
                "Broadcast requested but there are no online target nodes.",
                targetKnown: false,
                policyDecision: policyDecision);
        }

        var unionAvailableTransports = onlineNodes
            .SelectMany(x => ApplyLinkAvailabilityFilter(
                x.Identity.NodeId,
                x.AvailableTransports,
                linkAvailabilityFilter))
            .Distinct()
            .ToArray();

        var filteredDecision = _transportFilter.Filter(
            policyDecision,
            unionAvailableTransports);

        var applicable = _transportFilter.IsApplicable(filteredDecision);

        if (!applicable)
        {
            var reason = linkAvailabilityFilter is null
                ? "Broadcast requested but no applicable transport remains after filtering."
                : "Broadcast requested but no applicable healthy transport remains after link-aware filtering.";

            return CommunicationRouteResult.Failed(
                envelope,
                reason,
                targetKnown: true,
                targetAvailableTransports: unionAvailableTransports,
                policyDecision: policyDecision,
                filteredDecision: filteredDecision);
        }

        var successReason = linkAvailabilityFilter is null
            ? "Broadcast route resolved from online fleet transport union."
            : "Broadcast route resolved from online fleet healthy transport union.";

        return CommunicationRouteResult.Succeeded(
            envelope,
            successReason,
            targetKnown: true,
            targetAvailableTransports: unionAvailableTransports,
            policyDecision: policyDecision,
            filteredDecision: filteredDecision);
    }

    /// <summary>
    /// Hedef araÃ§ ve transport listesi iÃ§in opsiyonel link uygunluk filtresi uygular.
    /// 
    /// Filtre yoksa AvailableTransports olduÄŸu gibi dÃ¶ner.
    /// Filtre varsa sadece true dÃ¶nen transport tÃ¼rleri kalÄ±r.
    /// </summary>
    private static IReadOnlyList<TransportKind> ApplyLinkAvailabilityFilter(
        string vehicleId,
        IReadOnlyList<TransportKind> availableTransports,
        Func<string, TransportKind, bool>? linkAvailabilityFilter)
    {
        if (availableTransports is null || availableTransports.Count == 0)
            return Array.Empty<TransportKind>();

        if (linkAvailabilityFilter is null)
            return availableTransports;

        return availableTransports
            .Where(transportKind => linkAvailabilityFilter(vehicleId, transportKind))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Envelope'un broadcast olarak deÄŸerlendirilip deÄŸerlendirilmeyeceÄŸini belirler.
    /// 
    /// Broadcast sayÄ±lan durumlar:
    /// - TargetNodeId = BROADCAST
    /// - Policy broadcast istiyor
    /// - Priority Emergency
    /// </summary>
    private static bool IsBroadcastEnvelope(
        HydronomEnvelope envelope,
        TransportRouteDecision policyDecision)
    {
        return string.Equals(
                   envelope.TargetNodeId,
                   "BROADCAST",
                   StringComparison.OrdinalIgnoreCase) ||
               policyDecision.BroadcastAllAvailableLinks ||
               envelope.Priority == MessagePriority.Emergency;
    }
}
