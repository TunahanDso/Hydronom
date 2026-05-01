namespace Hydronom.GroundStation.Communication;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;
using Hydronom.GroundStation.Routing;

/// <summary>
/// Ground Station tarafında envelope mesajlarını gönderim öncesi route eden sınıftır.
/// 
/// Bu sınıf şimdilik gerçek TCP/LoRa/WebSocket gönderimi yapmaz.
/// İlk faz görevi:
/// - Envelope için policy route kararı üretmek,
/// - Hedef node FleetRegistry snapshot içinde var mı bakmak,
/// - Hedef node'un AvailableTransports listesine göre route'u filtrelemek,
/// - Mesajın gönderilebilir olup olmadığını söylemektir.
/// 
/// Yeni LinkHealth hazırlığı:
/// - Router artık opsiyonel link uygunluk filtresi alabilir.
/// - Bu filtre sayesinde ileride LinkHealthTracker üzerinden
///   kötü, critical veya lost linkler route kararından elenebilir.
/// - Böylece CommunicationRouter ileride kalite skoru tabanlı route kararına geçebilir.
/// 
/// İleride bu sınıfın üzerine:
/// - TransportManager,
/// - ITransport implementasyonları,
/// - retry/ACK tracking,
/// - link quality scoring,
/// - send queue,
/// - emergency broadcast fan-out
/// eklenecektir.
/// </summary>
public sealed class CommunicationRouter
{
    /// <summary>
    /// Mesaj tipine ve priority değerine göre teorik route kararı üreten policy.
    /// </summary>
    private readonly TransportRoutingPolicy _routingPolicy;

    /// <summary>
    /// Policy kararını hedef node'un gerçek transport listesine göre filtreleyen yardımcı.
    /// </summary>
    private readonly AvailableTransportFilter _transportFilter;

    /// <summary>
    /// CommunicationRouter oluşturur.
    /// 
    /// Dışarıdan policy/filter verilebilir.
    /// Verilmezse varsayılan implementasyonlar kullanılır.
    /// </summary>
    public CommunicationRouter(
        TransportRoutingPolicy? routingPolicy = null,
        AvailableTransportFilter? transportFilter = null)
    {
        _routingPolicy = routingPolicy ?? new TransportRoutingPolicy();
        _transportFilter = transportFilter ?? new AvailableTransportFilter();
    }

    /// <summary>
    /// Verilen envelope için mevcut fleet snapshot üzerinden route sonucu üretir.
    /// 
    /// Normal hedefli mesajlarda TargetNodeId üzerinden hedef araç aranır.
    /// Broadcast mesajlarda ise:
    /// - TargetNodeId "BROADCAST" olabilir,
    /// - Emergency priority olabilir,
    /// - BroadcastAllAvailableLinks true olabilir.
    /// 
    /// İlk fazda broadcast için tüm filodaki transport'lar birleştirilerek uygulanabilir route çıkarılır.
    /// 
    /// Bu overload eski davranışı korur.
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
    /// Verilen envelope için mevcut fleet snapshot ve opsiyonel link uygunluk filtresi üzerinden route sonucu üretir.
    /// 
    /// linkAvailabilityFilter:
    /// - vehicleId ve transportKind alır,
    /// - true dönerse link kullanılabilir kabul edilir,
    /// - false dönerse o transport route adaylarından çıkarılır.
    /// 
    /// Bu yapı LinkHealthTracker'a doğrudan bağımlılık kurmadan link-aware routing zemini hazırlar.
    /// Böylece CommunicationRouter saf route motoru olarak kalır,
    /// LinkHealthTracker ise GroundStationEngine tarafından dışarıdan bağlanabilir.
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
    /// Tek hedefli mesaj için route sonucu üretir.
    /// 
    /// Eğer linkAvailabilityFilter verilmişse hedef aracın AvailableTransports listesi
    /// önce bu filtreye göre daraltılır.
    /// Ardından mevcut AvailableTransportFilter ile policy kararı uygulanır.
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
    /// Broadcast mesajlar için route sonucu üretir.
    /// 
    /// İlk fazda tüm online araçların AvailableTransports listeleri birleştirilir.
    /// Böylece broadcast için pratikte kullanılabilecek transport türleri bulunur.
    /// 
    /// Eğer linkAvailabilityFilter verilmişse her online araç için transport listesi
    /// link sağlığına göre daraltılır ve sonra union alınır.
    /// 
    /// Not:
    /// Gerçek implementasyonda her node için ayrı route sonucu üretmek daha doğru olacaktır.
    /// Bu ilk çekirdek sadece toplam route uygulanabilirliğini gösterir.
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
    /// Hedef araç ve transport listesi için opsiyonel link uygunluk filtresi uygular.
    /// 
    /// Filtre yoksa AvailableTransports olduğu gibi döner.
    /// Filtre varsa sadece true dönen transport türleri kalır.
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
    /// Envelope'un broadcast olarak değerlendirilip değerlendirilmeyeceğini belirler.
    /// 
    /// Broadcast sayılan durumlar:
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