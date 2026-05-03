using System.Net.WebSockets;

namespace HydronomOps.Gateway.Domain;

/// <summary>
/// Gateway'e baÄŸlÄ± websocket istemcisini temsil eder.
/// </summary>
public sealed class GatewayClientConnection
{
    /// <summary>
    /// Ä°Ã§ baÄŸlantÄ± kimliÄŸi.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Socket nesnesi.
    /// </summary>
    public WebSocket Socket { get; init; } = default!;

    /// <summary>
    /// BaÄŸlantÄ± aÃ§Ä±lÄ±ÅŸ zamanÄ±.
    /// </summary>
    public DateTime ConnectedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Son gÃ¶rÃ¼lme zamanÄ±.
    /// </summary>
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son baÅŸarÄ±lÄ± gÃ¶nderim zamanÄ±.
    /// </summary>
    public DateTime? LastSentUtc { get; set; }

    /// <summary>
    /// Uzak uÃ§ bilgisi.
    /// </summary>
    public string RemoteIp { get; init; } = "unknown";

    /// <summary>
    /// BaÄŸlantÄ± halen canlÄ± mÄ±.
    /// </summary>
    public bool IsAlive =>
        Socket.State == WebSocketState.Open ||
        Socket.State == WebSocketState.CloseReceived;

    /// <summary>
    /// Son gÃ¶nderim zamanÄ±nÄ± gÃ¼nceller.
    /// </summary>
    public void MarkSent()
    {
        LastSentUtc = DateTime.UtcNow;
    }
}
