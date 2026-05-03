namespace Hydronom.GroundStation.Telemetry;

using Hydronom.Core.Communication;

/// <summary>
/// Hedef node'un kullanÄ±labilir haberleÅŸme transport'larÄ±na gÃ¶re
/// uygun telemetry profilini seÃ§en sÄ±nÄ±ftÄ±r.
/// 
/// Bu sÄ±nÄ±f PDF'deki Adaptive Telemetry Profile mantÄ±ÄŸÄ±nÄ±n ilk Ã§ekirdeÄŸidir.
/// AmaÃ§:
/// - BaÄŸlantÄ± gÃ¼Ã§lÃ¼ ise Full telemetry seÃ§mek,
/// - BaÄŸlantÄ± orta seviyedeyse Normal telemetry seÃ§mek,
/// - Sadece dÃ¼ÅŸÃ¼k bant geniÅŸlikli kanal varsa Light telemetry seÃ§mek,
/// - HiÃ§ bilgi yoksa gÃ¼venli varsayÄ±lan olarak Light telemetry seÃ§mektir.
/// </summary>
public sealed class AdaptiveTelemetryProfileSelector
{
    /// <summary>
    /// Verilen kullanÄ±labilir transport listesine gÃ¶re telemetry profili seÃ§er.
    /// 
    /// Ã–ncelik:
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
    /// SeÃ§ilen telemetry profilini insan tarafÄ±ndan okunabilir kÄ±sa aÃ§Ä±klamaya Ã§evirir.
    /// 
    /// Bu aÃ§Ä±klama:
    /// - Hydronom Ops telemetry panelinde,
    /// - Ground Station loglarÄ±nda,
    /// - Diagnostics ekranlarÄ±nda
    /// gÃ¶sterilebilir.
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
    /// YÃ¼ksek bant geniÅŸlikli transport olup olmadÄ±ÄŸÄ±nÄ± kontrol eder.
    /// 
    /// Bu kanallar Full telemetry iÃ§in uygundur.
    /// </summary>
    private static bool HasHighBandwidthTransport(IReadOnlySet<TransportKind> available)
    {
        return available.Contains(TransportKind.Tcp) ||
               available.Contains(TransportKind.WebSocket) ||
               available.Contains(TransportKind.Cellular);
    }

    /// <summary>
    /// Orta bant geniÅŸlikli transport olup olmadÄ±ÄŸÄ±nÄ± kontrol eder.
    /// 
    /// Bu kanallar Normal telemetry iÃ§in uygundur.
    /// </summary>
    private static bool HasMediumBandwidthTransport(IReadOnlySet<TransportKind> available)
    {
        return available.Contains(TransportKind.RfModem) ||
               available.Contains(TransportKind.Mesh);
    }
}
