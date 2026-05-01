namespace Hydronom.GroundStation.Transports;

using Hydronom.Core.Communication;

/// <summary>
/// Ground Transport Manager tarafından işlenecek gönderim isteğini temsil eder.
/// 
/// Bu model, üst seviye GroundStationEngine çağrısından gelen envelope'u,
/// route sonucunu ve gönderim davranışını tek yerde toplar.
/// </summary>
public sealed record GroundTransportSendRequest
{
    /// <summary>
    /// Gönderilecek Hydronom envelope.
    /// </summary>
    public HydronomEnvelope Envelope { get; init; } = new();

    /// <summary>
    /// Link health destekli route kullanılsın mı?
    /// </summary>
    public bool UseLinkHealthRouting { get; init; } = true;

    /// <summary>
    /// ACK gerekiyorsa manager bunu ACK gibi mi işaretlesin?
    /// 
    /// İlk fazda gerçek ACK dinleme sistemi yok.
    /// Bu yüzden başarılı SendAsync sonucu, RequiresAck true ise simüle ACK olarak kaydedilebilir.
    /// Gerçek ACK listener geldiğinde bu davranış değiştirilecektir.
    /// </summary>
    public bool TreatSuccessfulSendAsAckWhenRequired { get; init; } = true;

    /// <summary>
    /// Tek transport denemesinin timeout süresi.
    /// </summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Primary transport başarısız olursa fallback denenmeli mi?
    /// </summary>
    public bool TryFallbacks { get; init; } = true;

    /// <summary>
    /// Broadcast route için uygulanabilir tüm transport'lar denenmeli mi?
    /// </summary>
    public bool SendToAllForBroadcast { get; init; } = true;

    /// <summary>
    /// İnsan-okunabilir açıklama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Ek metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}