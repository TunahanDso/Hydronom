namespace Hydronom.GroundStation.Transports.Receive;

using Hydronom.Core.Communication;

/// <summary>
/// Ground transport receive pipeline içinde alınan tek bir envelope olayını temsil eder.
/// 
/// Bu model:
/// - Hangi transport üzerinden mesaj geldiğini,
/// - Gelen envelope'u,
/// - Mesajın GroundStationEngine tarafından işlenip işlenmediğini,
/// - Hata varsa açıklamasını
/// taşır.
/// </summary>
public sealed record GroundTransportReceiveEvent
{
    /// <summary>
    /// Receive event için benzersiz ID.
    /// </summary>
    public string ReceiveEventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Mesajın alındığı UTC zaman.
    /// </summary>
    public DateTimeOffset ReceivedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Mesajın geldiği transport adı.
    /// </summary>
    public string TransportName { get; init; } = string.Empty;

    /// <summary>
    /// Mesajın geldiği transport türü.
    /// </summary>
    public TransportKind TransportKind { get; init; } = TransportKind.Unknown;

    /// <summary>
    /// Alınan envelope.
    /// </summary>
    public HydronomEnvelope? Envelope { get; init; }

    /// <summary>
    /// Envelope GroundStationEngine tarafından başarıyla işlendi mi?
    /// </summary>
    public bool Handled { get; init; }

    /// <summary>
    /// Receive sırasında hata oluştu mu?
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Hata mesajı.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// İnsan-okunabilir açıklama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}