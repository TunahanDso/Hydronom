namespace Hydronom.GroundStation.Transports.Receive;

/// <summary>
/// GroundTransportReceiver çalışma ayarlarını tutar.
/// </summary>
public sealed record GroundTransportReceiverOptions
{
    /// <summary>
    /// Receive loop hata aldığında çalışmaya devam etsin mi?
    /// </summary>
    public bool ContinueOnTransportError { get; init; } = true;

    /// <summary>
    /// Transport hata verdikten sonra tekrar denemeden önce bekleme süresi.
    /// </summary>
    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gelen heartbeat/command result gibi mesajlar üzerinden link görüldü metriği işlensin mi?
    /// </summary>
    public bool MarkLinkSeenOnReceive { get; init; } = true;

    /// <summary>
    /// Receive event geçmişinde tutulacak maksimum kayıt sayısı.
    /// </summary>
    public int MaxEventHistory { get; init; } = 500;
}