namespace Hydronom.GroundStation.Transports.Receive;

using Hydronom.Core.Communication;

/// <summary>
/// Ground transport receive pipeline iÃ§inde alÄ±nan tek bir envelope olayÄ±nÄ± temsil eder.
/// 
/// Bu model:
/// - Hangi transport Ã¼zerinden mesaj geldiÄŸini,
/// - Gelen envelope'u,
/// - MesajÄ±n GroundStationEngine tarafÄ±ndan iÅŸlenip iÅŸlenmediÄŸini,
/// - Hata varsa aÃ§Ä±klamasÄ±nÄ±
/// taÅŸÄ±r.
/// </summary>
public sealed record GroundTransportReceiveEvent
{
    /// <summary>
    /// Receive event iÃ§in benzersiz ID.
    /// </summary>
    public string ReceiveEventId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// MesajÄ±n alÄ±ndÄ±ÄŸÄ± UTC zaman.
    /// </summary>
    public DateTimeOffset ReceivedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// MesajÄ±n geldiÄŸi transport adÄ±.
    /// </summary>
    public string TransportName { get; init; } = string.Empty;

    /// <summary>
    /// MesajÄ±n geldiÄŸi transport tÃ¼rÃ¼.
    /// </summary>
    public TransportKind TransportKind { get; init; } = TransportKind.Unknown;

    /// <summary>
    /// AlÄ±nan envelope.
    /// </summary>
    public HydronomEnvelope? Envelope { get; init; }

    /// <summary>
    /// Envelope GroundStationEngine tarafÄ±ndan baÅŸarÄ±yla iÅŸlendi mi?
    /// </summary>
    public bool Handled { get; init; }

    /// <summary>
    /// Receive sÄ±rasÄ±nda hata oluÅŸtu mu?
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Hata mesajÄ±.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Ä°nsan-okunabilir aÃ§Ä±klama.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
