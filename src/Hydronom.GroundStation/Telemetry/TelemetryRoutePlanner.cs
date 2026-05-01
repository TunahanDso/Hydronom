namespace Hydronom.GroundStation.Telemetry;

using Hydronom.GroundStation.Communication;

/// <summary>
/// CommunicationRouter route sonucuna göre telemetry profil planı üreten sınıftır.
/// 
/// Bu sınıf iki parçayı birleştirir:
/// - CommunicationRouteResult: Mesaj/araç hangi transport'larla route edilebilir?
/// - AdaptiveTelemetryProfileSelector: Bu transport seviyesine göre Light/Normal/Full telemetry seçimi
/// 
/// İlk fazda gerçek telemetry payload üretmez.
/// Sadece "bu route için hangi telemetry yoğunluğu mantıklı?" kararını verir.
/// </summary>
public sealed class TelemetryRoutePlanner
{
    /// <summary>
    /// Transport listesine göre telemetry profilini seçen yardımcı sınıf.
    /// </summary>
    private readonly AdaptiveTelemetryProfileSelector _profileSelector;

    /// <summary>
    /// TelemetryRoutePlanner oluşturur.
    /// </summary>
    public TelemetryRoutePlanner(
        AdaptiveTelemetryProfileSelector? profileSelector = null)
    {
        _profileSelector = profileSelector ?? new AdaptiveTelemetryProfileSelector();
    }

    /// <summary>
    /// Route sonucundan telemetry planı üretir.
    /// 
    /// Profil seçimi için öncelikle route içindeki uygulanabilir Primary + Fallback
    /// transport listesi kullanılır.
    /// 
    /// Eğer route edilemiyorsa veya uygulanabilir transport yoksa:
    /// - Güvenli varsayılan olarak Light telemetry seçilir.
    /// - Plan CanRoute=false kalır.
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