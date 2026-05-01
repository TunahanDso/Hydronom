namespace Hydronom.GroundStation.Routing;

using Hydronom.Core.Communication;

/// <summary>
/// TransportRoutingPolicy tarafından üretilen route kararını,
/// hedef node'un gerçekten kullanılabilir transport listesine göre filtreler.
/// 
/// Bu sınıfın amacı:
/// - Teorik route kararını pratik uygulanabilir route kararına çevirmek,
/// - Araçta olmayan transport'ları elemek,
/// - Plug-and-play haberleşme mantığını güçlendirmek,
/// - CommunicationRouter'a sadece uygulanabilir transport listesini vermektir.
/// 
/// Örnek:
/// Policy kararı:
/// - Primary: Tcp, RfModem
/// - Fallback: LoRa
/// 
/// Hedef node available transports:
/// - Tcp
/// - Mock
/// 
/// Filtre sonrası:
/// - Primary: Tcp
/// - Fallback: boş
/// </summary>
public sealed class AvailableTransportFilter
{
    /// <summary>
    /// Route kararını hedef node'un kullanılabilir transport listesine göre filtreler.
    /// 
    /// Eğer availableTransports boşsa:
    /// - Mevcut route kararı değiştirilmeden döndürülür.
    /// - Çünkü bazı durumlarda hedef node bilgisi henüz bilinmeyebilir.
    /// 
    /// Eğer karar BroadcastAllAvailableLinks ise:
    /// - PrimaryTransports içinde yalnızca hedefte mevcut olanlar bırakılır.
    /// - FallbackTransports temizlenir.
    /// 
    /// Normal route kararında:
    /// - Primary ve Fallback ayrı ayrı filtrelenir.
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
    /// Route kararının filtre sonrası hâlâ uygulanabilir olup olmadığını kontrol eder.
    /// 
    /// Uygulanabilirlik için:
    /// - Broadcast ise en az bir Primary transport kalmalı,
    /// - Normal route ise Primary veya Fallback içinde en az bir transport kalmalı.
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