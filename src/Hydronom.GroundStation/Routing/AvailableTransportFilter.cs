namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// TransportRoutingPolicy tarafÄ±ndan Ã¼retilen route kararÄ±nÄ±,
/// hedef node'un gerÃ§ekten kullanÄ±labilir transport listesine gÃ¶re filtreler.
/// 
/// Bu sÄ±nÄ±fÄ±n amacÄ±:
/// - Teorik route kararÄ±nÄ± pratik uygulanabilir route kararÄ±na Ã§evirmek,
/// - AraÃ§ta olmayan transport'larÄ± elemek,
/// - Plug-and-play haberleÅŸme mantÄ±ÄŸÄ±nÄ± gÃ¼Ã§lendirmek,
/// - CommunicationRouter'a sadece uygulanabilir transport listesini vermektir.
/// 
/// Ã–rnek:
/// Policy kararÄ±:
/// - Primary: Tcp, RfModem
/// - Fallback: LoRa
/// 
/// Hedef node available transports:
/// - Tcp
/// - Mock
/// 
/// Filtre sonrasÄ±:
/// - Primary: Tcp
/// - Fallback: boÅŸ
/// </summary>
public sealed class AvailableTransportFilter
{
    /// <summary>
    /// Route kararÄ±nÄ± hedef node'un kullanÄ±labilir transport listesine gÃ¶re filtreler.
    /// 
    /// EÄŸer availableTransports boÅŸsa:
    /// - Mevcut route kararÄ± deÄŸiÅŸtirilmeden dÃ¶ndÃ¼rÃ¼lÃ¼r.
    /// - Ã‡Ã¼nkÃ¼ bazÄ± durumlarda hedef node bilgisi henÃ¼z bilinmeyebilir.
    /// 
    /// EÄŸer karar BroadcastAllAvailableLinks ise:
    /// - PrimaryTransports iÃ§inde yalnÄ±zca hedefte mevcut olanlar bÄ±rakÄ±lÄ±r.
    /// - FallbackTransports temizlenir.
    /// 
    /// Normal route kararÄ±nda:
    /// - Primary ve Fallback ayrÄ± ayrÄ± filtrelenir.
    /// </summary>
    public TransportRouteDecision Filter(
        TransportRouteDecision decision,
        IReadOnlyList<TransportKind> availableTransports)
    {
        if (decision is null)
            throw new ArgumentNullException(nameof(decision));

        if (availableTransports is null || availableTransports.Count == 0)
            return decision;

        var available = availableTransports.ToHashSet();

        var filteredPrimary = decision.PrimaryTransports
            .Where(available.Contains)
            .ToArray();

        var filteredFallback = decision.FallbackTransports
            .Where(available.Contains)
            .ToArray();

        if (decision.BroadcastAllAvailableLinks)
        {
            return decision with
            {
                PrimaryTransports = filteredPrimary,
                FallbackTransports = Array.Empty<TransportKind>(),
                Reason = $"{decision.Reason} Filtered by target available transports for broadcast."
            };
        }

        return decision with
        {
            PrimaryTransports = filteredPrimary,
            FallbackTransports = filteredFallback,
            Reason = $"{decision.Reason} Filtered by target available transports."
        };
    }

    /// <summary>
    /// Route kararÄ±nÄ±n filtre sonrasÄ± hÃ¢lÃ¢ uygulanabilir olup olmadÄ±ÄŸÄ±nÄ± kontrol eder.
    /// 
    /// Uygulanabilirlik iÃ§in:
    /// - Broadcast ise en az bir Primary transport kalmalÄ±,
    /// - Normal route ise Primary veya Fallback iÃ§inde en az bir transport kalmalÄ±.
    /// </summary>
    public bool IsApplicable(TransportRouteDecision decision)
    {
        if (decision is null)
            return false;

        if (decision.BroadcastAllAvailableLinks)
            return decision.PrimaryTransports.Count > 0;

        return decision.PrimaryTransports.Count > 0 ||
               decision.FallbackTransports.Count > 0;
    }
}
