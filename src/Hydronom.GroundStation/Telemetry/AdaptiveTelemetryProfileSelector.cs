namespace Hydronom.GroundStation.Telemetry;

using Hydronom.Core.Communication;

/// <summary>
/// Hedef node'un kullanılabilir haberleşme transport'larına göre
/// uygun telemetry profilini seçen sınıftır.
/// 
/// Bu sınıf PDF'deki Adaptive Telemetry Profile mantığının ilk çekirdeğidir.
/// Amaç:
/// - Bağlantı güçlü ise Full telemetry seçmek,
/// - Bağlantı orta seviyedeyse Normal telemetry seçmek,
/// - Sadece düşük bant genişlikli kanal varsa Light telemetry seçmek,
/// - Hiç bilgi yoksa güvenli varsayılan olarak Light telemetry seçmektir.
/// </summary>
public sealed class AdaptiveTelemetryProfileSelector
{
    /// <summary>
    /// Verilen kullanılabilir transport listesine göre telemetry profili seçer.
    /// 
    /// Öncelik:
    /// 1. TCP / WebSocket / Cellular varsa Full
    /// 2. RF modem / Mesh varsa Normal
    /// 3. LoRa varsa Light
    /// 4. Bilgi yoksa Light
    /// </summary>
    public TelemetryProfile Select(IReadOnlyList<TransportKind> availableTransports)
    {
        if (availableTransports is null || availableTransports.Count == 0)
            return TelemetryProfile.Light;

        var available = availableTransports.ToHashSet();

        if (HasHighBandwidthTransport(available))
            return TelemetryProfile.Full;

        if (HasMediumBandwidthTransport(available))
            return TelemetryProfile.Normal;

        if (available.Contains(TransportKind.LoRa))
            return TelemetryProfile.Light;

        return TelemetryProfile.Light;
    }

    /// <summary>
    /// Seçilen telemetry profilini insan tarafından okunabilir kısa açıklamaya çevirir.
    /// 
    /// Bu açıklama:
    /// - Hydronom Ops telemetry panelinde,
    /// - Ground Station loglarında,
    /// - Diagnostics ekranlarında
    /// gösterilebilir.
    /// </summary>
    public string Explain(TelemetryProfile profile)
    {
        return profile switch
        {
            TelemetryProfile.Full =>
                "Full telemetry selected because a high-bandwidth transport is available.",

            TelemetryProfile.Normal =>
                "Normal telemetry selected because a medium-bandwidth transport is available.",

            TelemetryProfile.Light =>
                "Light telemetry selected because only low-bandwidth or unknown transports are available.",

            _ =>
                "Telemetry profile is unknown."
        };
    }

    /// <summary>
    /// Yüksek bant genişlikli transport olup olmadığını kontrol eder.
    /// 
    /// Bu kanallar Full telemetry için uygundur.
    /// </summary>
    private static bool HasHighBandwidthTransport(IReadOnlySet<TransportKind> available)
    {
        return available.Contains(TransportKind.Tcp) ||
               available.Contains(TransportKind.WebSocket) ||
               available.Contains(TransportKind.Cellular);
    }

    /// <summary>
    /// Orta bant genişlikli transport olup olmadığını kontrol eder.
    /// 
    /// Bu kanallar Normal telemetry için uygundur.
    /// </summary>
    private static bool HasMediumBandwidthTransport(IReadOnlySet<TransportKind> available)
    {
        return available.Contains(TransportKind.RfModem) ||
               available.Contains(TransportKind.Mesh);
    }
}