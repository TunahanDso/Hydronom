using System.Net.WebSockets;

namespace HydronomOps.Gateway.Domain;

/// <summary>
/// Gateway'e bağlı websocket istemcisini temsil eder.
/// </summary>
public sealed class GatewayClientConnection
{
    /// <summary>
    /// İç bağlantı kimliği.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Socket nesnesi.
    /// </summary>
    public WebSocket Socket { get; init; } = default!;

    /// <summary>
    /// Bağlantı açılış zamanı.
    /// </summary>
    public DateTime ConnectedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Son görülme zamanı.
    /// </summary>
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son başarılı gönderim zamanı.
    /// </summary>
    public DateTime? LastSentUtc { get; set; }

    /// <summary>
    /// Uzak uç bilgisi.
    /// </summary>
    public string RemoteIp { get; init; } = "unknown";

    /// <summary>
    /// Bağlantı halen canlı mı.
    /// </summary>
    public bool IsAlive =>
        Socket.State == WebSocketState.Open ||
        Socket.State == WebSocketState.CloseReceived;

    /// <summary>
    /// Son gönderim zamanını günceller.
    /// </summary>
    public void MarkSent()
    {
        LastSentUtc = DateTime.UtcNow;
    }
}