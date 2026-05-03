namespace Hydronom.GroundStation.Transports.Receive;

/// <summary>
/// GroundTransportReceiver Ã§alÄ±ÅŸma ayarlarÄ±nÄ± tutar.
/// </summary>
public sealed record GroundTransportReceiverOptions
{
    /// <summary>
    /// Receive loop hata aldÄ±ÄŸÄ±nda Ã§alÄ±ÅŸmaya devam etsin mi?
    /// </summary>
    public bool ContinueOnTransportError { get; init; } = true;

    /// <summary>
    /// Transport hata verdikten sonra tekrar denemeden Ã¶nce bekleme sÃ¼resi.
    /// </summary>
    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gelen heartbeat/command result gibi mesajlar Ã¼zerinden link gÃ¶rÃ¼ldÃ¼ metriÄŸi iÅŸlensin mi?
    /// </summary>
    public bool MarkLinkSeenOnReceive { get; init; } = true;

    /// <summary>
    /// Receive event geÃ§miÅŸinde tutulacak maksimum kayÄ±t sayÄ±sÄ±.
    /// </summary>
    public int MaxEventHistory { get; init; } = 500;
}
