namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Transport Manager tarafÄ±ndan iÅŸlenecek gÃ¶nderim isteÄŸini temsil eder.
/// 
/// Bu model, Ã¼st seviye GroundStationEngine Ã§aÄŸrÄ±sÄ±ndan gelen envelope'u,
/// route sonucunu ve gÃ¶nderim davranÄ±ÅŸÄ±nÄ± tek yerde toplar.
/// </summary>
public sealed record GroundTransportSendRequest
{
    /// <summary>
    /// GÃ¶nderilecek Hydronom envelope.
    /// </summary>
    public HydronomEnvelope Envelope { get; init; } = new();

    /// <summary>
    /// Link health destekli route kullanÄ±lsÄ±n mÄ±?
    /// </summary>
    public bool UseLinkHealthRouting { get; init; } = true;

    /// <summary>
    /// ACK gerekiyorsa manager bunu ACK gibi mi iÅŸaretlesin?
    /// 
    /// Ä°lk fazda gerÃ§ek ACK dinleme sistemi yok.
    /// Bu yÃ¼zden baÅŸarÄ±lÄ± SendAsync sonucu, RequiresAck true ise simÃ¼le ACK olarak kaydedilebilir.
    /// GerÃ§ek ACK listener geldiÄŸinde bu davranÄ±ÅŸ deÄŸiÅŸtirilecektir.
    /// </summary>
    public bool TreatSuccessfulSendAsAckWhenRequired { get; init; } = true;

    /// <summary>
    /// Tek transport denemesinin timeout sÃ¼resi.
    /// </summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Primary transport baÅŸarÄ±sÄ±z olursa fallback denenmeli mi?
    /// </summary>
    public bool TryFallbacks { get; init; } = true;

    /// <summary>
    /// Broadcast route iÃ§in uygulanabilir tÃ¼m transport'lar denenmeli mi?
    /// </summary>
    public bool SendToAllForBroadcast { get; init; } = true;

    /// <summary>
    /// Ä°nsan-okunabilir aÃ§Ä±klama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Ek metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}
