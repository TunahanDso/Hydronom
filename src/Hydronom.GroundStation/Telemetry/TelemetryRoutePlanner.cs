namespace Hydronom.GroundStation.Telemetry;

using Hydronom.GroundStation.Communication;

/// <summary>
/// CommunicationRouter route sonucuna gÃ¶re telemetry profil planÄ± Ã¼reten sÄ±nÄ±ftÄ±r.
/// 
/// Bu sÄ±nÄ±f iki parÃ§ayÄ± birleÅŸtirir:
/// - CommunicationRouteResult: Mesaj/araÃ§ hangi transport'larla route edilebilir?
/// - AdaptiveTelemetryProfileSelector: Bu transport seviyesine gÃ¶re Light/Normal/Full telemetry seÃ§imi
/// 
/// Ä°lk fazda gerÃ§ek telemetry payload Ã¼retmez.
/// Sadece "bu route iÃ§in hangi telemetry yoÄŸunluÄŸu mantÄ±klÄ±?" kararÄ±nÄ± verir.
/// </summary>
public sealed class TelemetryRoutePlanner
{
    /// <summary>
    /// Transport listesine gÃ¶re telemetry profilini seÃ§en yardÄ±mcÄ± sÄ±nÄ±f.
    /// </summary>
    private readonly AdaptiveTelemetryProfileSelector _profileSelector;

    /// <summary>
    /// TelemetryRoutePlanner oluÅŸturur.
    /// </summary>
    public TelemetryRoutePlanner(
        AdaptiveTelemetryProfileSelector? profileSelector = null)
    {
        _profileSelector = profileSelector ?? new AdaptiveTelemetryProfileSelector();
    }

    /// <summary>
    /// Route sonucundan telemetry planÄ± Ã¼retir.
    /// 
    /// Profil seÃ§imi iÃ§in Ã¶ncelikle route iÃ§indeki uygulanabilir Primary + Fallback
    /// transport listesi kullanÄ±lÄ±r.
    /// 
    /// EÄŸer route edilemiyorsa veya uygulanabilir transport yoksa:
    /// - GÃ¼venli varsayÄ±lan olarak Light telemetry seÃ§ilir.
    /// - Plan CanRoute=false kalÄ±r.
    /// </summary>
    public TelemetryRoutePlan Plan(CommunicationRouteResult route)
    {
        if (route is null)
            throw new ArgumentNullException(nameof(route));

        var usableTransports = route.PrimaryTransports
            .Concat(route.FallbackTransports)
            .Distinct()
            .ToArray();

        if (!route.CanRoute || usableTransports.Length == 0)
        {
            var fallbackProfile = TelemetryProfile.Light;

            return TelemetryRoutePlan.FromRoute(
                route,
                fallbackProfile,
                "Light telemetry selected because route is not currently applicable.");
        }

        var profile = _profileSelector.Select(usableTransports);
        var reason = _profileSelector.Explain(profile);

        return TelemetryRoutePlan.FromRoute(
            route,
            profile,
            reason);
    }
}
