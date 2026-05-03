namespace Hydronom.GroundStation.Communication;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Routing;

/// <summary>
/// CommunicationRouter tarafÄ±ndan Ã¼retilen route sonucunu temsil eder.
/// 
/// Bu model gerÃ§ek gÃ¶nderim sonucunu deÄŸil, gÃ¶nderimden Ã¶nceki yÃ¶nlendirme kararÄ±nÄ± taÅŸÄ±r.
/// Yani ÅŸu sorulara cevap verir:
/// - Envelope route edilebilir mi?
/// - Hedef node biliniyor mu?
/// - Hedef node hangi transport'lara sahip?
/// - Policy hangi transport'larÄ± Ã¶nerdi?
/// - Filtre sonrasÄ± hangi transport'lar gerÃ§ekten kullanÄ±labilir?
/// - Broadcast gerekiyor mu?
/// - ACK gerekiyor mu?
/// 
/// Ä°leride gerÃ§ek gÃ¶nderim katmanÄ± eklenince bu model:
/// - Send attempt result,
/// - Retry plan,
/// - Selected transport instance,
/// - Link quality,
/// - Failure reason
/// bilgileriyle geniÅŸletilebilir.
/// </summary>
public sealed record CommunicationRouteResult
{
    /// <summary>
    /// Route edilen envelope mesaj kimliÄŸi.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilen envelope mesaj tipi.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n kaynak node kimliÄŸi.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n hedef node kimliÄŸi.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node FleetRegistry iÃ§inde bulundu mu?
    /// 
    /// Broadcast mesajlarÄ±nda veya henÃ¼z registry'ye dÃ¼ÅŸmemiÅŸ hedeflerde false olabilir.
    /// </summary>
    public bool TargetKnown { get; init; }

    /// <summary>
    /// Route kararÄ±nÄ±n uygulanabilir olup olmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// true ise en az bir kullanÄ±labilir transport bulunmuÅŸtur veya broadcast iÃ§in uygun kanal vardÄ±r.
    /// </summary>
    public bool CanRoute { get; init; }

    /// <summary>
    /// Route edilememe veya route edilebilme sebebinin kÄ±sa aÃ§Ä±klamasÄ±.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node'un bildirdiÄŸi kullanÄ±labilir transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> TargetAvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// TransportRoutingPolicy tarafÄ±ndan Ã¼retilen ham route kararÄ±.
    /// </summary>
    public TransportRouteDecision? PolicyDecision { get; init; }

    /// <summary>
    /// AvailableTransportFilter sonrasÄ± pratikte uygulanabilir route kararÄ±.
    /// </summary>
    public TransportRouteDecision? FilteredDecision { get; init; }

    /// <summary>
    /// FiltrelenmiÅŸ karardaki birincil transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports =>
        FilteredDecision?.PrimaryTransports ?? Array.Empty<TransportKind>();

    /// <summary>
    /// FiltrelenmiÅŸ karardaki fallback transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports =>
        FilteredDecision?.FallbackTransports ?? Array.Empty<TransportKind>();

    /// <summary>
    /// Mesaj tÃ¼m uygun baÄŸlantÄ±lardan yayÄ±nlanmalÄ± mÄ±?
    /// </summary>
    public bool BroadcastAllAvailableLinks =>
        FilteredDecision?.BroadcastAllAvailableLinks == true;

    /// <summary>
    /// Mesaj iÃ§in ACK bekleniyor mu?
    /// </summary>
    public bool RequiresAck =>
        FilteredDecision?.RequiresAck == true;

    /// <summary>
    /// BaÅŸarÄ±sÄ±z route sonucu Ã¼retir.
    /// </summary>
    public static CommunicationRouteResult Failed(
        HydronomEnvelope envelope,
        string reason,
        bool targetKnown = false,
        IReadOnlyList<TransportKind>? targetAvailableTransports = null,
        TransportRouteDecision? policyDecision = null,
        TransportRouteDecision? filteredDecision = null)
    {
        return new CommunicationRouteResult
        {
            MessageId = envelope?.MessageId ?? string.Empty,
            MessageType = envelope?.MessageType ?? string.Empty,
            SourceNodeId = envelope?.SourceNodeId ?? string.Empty,
            TargetNodeId = envelope?.TargetNodeId ?? string.Empty,
            TargetKnown = targetKnown,
            CanRoute = false,
            Reason = reason,
            TargetAvailableTransports = targetAvailableTransports ?? Array.Empty<TransportKind>(),
            PolicyDecision = policyDecision,
            FilteredDecision = filteredDecision
        };
    }

    /// <summary>
    /// BaÅŸarÄ±lÄ± route sonucu Ã¼retir.
    /// </summary>
    public static CommunicationRouteResult Succeeded(
        HydronomEnvelope envelope,
        string reason,
        bool targetKnown,
        IReadOnlyList<TransportKind> targetAvailableTransports,
        TransportRouteDecision policyDecision,
        TransportRouteDecision filteredDecision)
    {
        return new CommunicationRouteResult
        {
            MessageId = envelope.MessageId,
            MessageType = envelope.MessageType,
            SourceNodeId = envelope.SourceNodeId,
            TargetNodeId = envelope.TargetNodeId,
            TargetKnown = targetKnown,
            CanRoute = true,
            Reason = reason,
            TargetAvailableTransports = targetAvailableTransports,
            PolicyDecision = policyDecision,
            FilteredDecision = filteredDecision
        };
    }
}
