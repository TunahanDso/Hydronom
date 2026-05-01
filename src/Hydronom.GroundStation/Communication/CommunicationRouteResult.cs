namespace Hydronom.GroundStation.Communication;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.Routing;

/// <summary>
/// CommunicationRouter tarafından üretilen route sonucunu temsil eder.
/// 
/// Bu model gerçek gönderim sonucunu değil, gönderimden önceki yönlendirme kararını taşır.
/// Yani şu sorulara cevap verir:
/// - Envelope route edilebilir mi?
/// - Hedef node biliniyor mu?
/// - Hedef node hangi transport'lara sahip?
/// - Policy hangi transport'ları önerdi?
/// - Filtre sonrası hangi transport'lar gerçekten kullanılabilir?
/// - Broadcast gerekiyor mu?
/// - ACK gerekiyor mu?
/// 
/// İleride gerçek gönderim katmanı eklenince bu model:
/// - Send attempt result,
/// - Retry plan,
/// - Selected transport instance,
/// - Link quality,
/// - Failure reason
/// bilgileriyle genişletilebilir.
/// </summary>
public sealed record CommunicationRouteResult
{
    /// <summary>
    /// Route edilen envelope mesaj kimliği.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Route edilen envelope mesaj tipi.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın kaynak node kimliği.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın hedef node kimliği.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node FleetRegistry içinde bulundu mu?
    /// 
    /// Broadcast mesajlarında veya henüz registry'ye düşmemiş hedeflerde false olabilir.
    /// </summary>
    public bool TargetKnown { get; init; }

    /// <summary>
    /// Route kararının uygulanabilir olup olmadığını belirtir.
    /// 
    /// true ise en az bir kullanılabilir transport bulunmuştur veya broadcast için uygun kanal vardır.
    /// </summary>
    public bool CanRoute { get; init; }

    /// <summary>
    /// Route edilememe veya route edilebilme sebebinin kısa açıklaması.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Hedef node'un bildirdiği kullanılabilir transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> TargetAvailableTransports { get; init; } =
        Array.Empty<TransportKind>();

    /// <summary>
    /// TransportRoutingPolicy tarafından üretilen ham route kararı.
    /// </summary>
    public TransportRouteDecision? PolicyDecision { get; init; }

    /// <summary>
    /// AvailableTransportFilter sonrası pratikte uygulanabilir route kararı.
    /// </summary>
    public TransportRouteDecision? FilteredDecision { get; init; }

    /// <summary>
    /// Filtrelenmiş karardaki birincil transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> PrimaryTransports =>
        FilteredDecision?.PrimaryTransports ?? Array.Empty<TransportKind>();

    /// <summary>
    /// Filtrelenmiş karardaki fallback transport listesi.
    /// </summary>
    public IReadOnlyList<TransportKind> FallbackTransports =>
        FilteredDecision?.FallbackTransports ?? Array.Empty<TransportKind>();

    /// <summary>
    /// Mesaj tüm uygun bağlantılardan yayınlanmalı mı?
    /// </summary>
    public bool BroadcastAllAvailableLinks =>
        FilteredDecision?.BroadcastAllAvailableLinks == true;

    /// <summary>
    /// Mesaj için ACK bekleniyor mu?
    /// </summary>
    public bool RequiresAck =>
        FilteredDecision?.RequiresAck == true;

    /// <summary>
    /// Başarısız route sonucu üretir.
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
    /// Başarılı route sonucu üretir.
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