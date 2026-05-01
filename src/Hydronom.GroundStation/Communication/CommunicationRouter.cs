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
    /// </summary>
    public CommunicationRouteResult Route(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot)
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
            return RouteBroadcast(envelope, fleetSnapshot, policyDecision);
        }

        return RouteSingleTarget(envelope, fleetSnapshot, policyDecision);
    }

    /// <summary>
    /// Tek hedefli mesaj için route sonucu üretir.
    /// </summary>
    private CommunicationRouteResult RouteSingleTarget(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        TransportRouteDecision policyDecision)
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

        var availableTransports = target.AvailableTransports;
        var filteredDecision = _transportFilter.Filter(policyDecision, availableTransports);
        var applicable = _transportFilter.IsApplicable(filteredDecision);

        if (!applicable)
        {
            return CommunicationRouteResult.Failed(
                envelope,
                "Target node is known but no applicable transport remains after filtering.",
                targetKnown: true,
                targetAvailableTransports: availableTransports,
                policyDecision: policyDecision,
                filteredDecision: filteredDecision);
        }

        return CommunicationRouteResult.Succeeded(
            envelope,
            "Route resolved for target node.",
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
    /// Not:
    /// Gerçek implementasyonda her node için ayrı route sonucu üretmek daha doğru olacaktır.
    /// Bu ilk çekirdek sadece toplam route uygulanabilirliğini gösterir.
    /// </summary>
    private CommunicationRouteResult RouteBroadcast(
        HydronomEnvelope envelope,
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        TransportRouteDecision policyDecision)
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
            .SelectMany(x => x.AvailableTransports)
            .Distinct()
            .ToArray();

        var filteredDecision = _transportFilter.Filter(
            policyDecision,
            unionAvailableTransports);

        var applicable = _transportFilter.IsApplicable(filteredDecision);

        if (!applicable)
        {
            return CommunicationRouteResult.Failed(
                envelope,
                "Broadcast requested but no applicable transport remains after filtering.",
                targetKnown: true,
                targetAvailableTransports: unionAvailableTransports,
                policyDecision: policyDecision,
                filteredDecision: filteredDecision);
        }

        return CommunicationRouteResult.Succeeded(
            envelope,
            "Broadcast route resolved from online fleet transport union.",
            targetKnown: true,
            targetAvailableTransports: unionAvailableTransports,
            policyDecision: policyDecision,
            filteredDecision: filteredDecision);
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