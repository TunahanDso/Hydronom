namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// WebSocket yayÄ±n katmanÄ± ayarlarÄ±.
/// </summary>
public sealed class WebSocketOptions
{
    public const string SectionName = "WebSocket";

    /// <summary>
    /// WebSocket endpoint yolu.
    /// </summary>
    public string Path { get; set; } = "/ws";

    /// <summary>
    /// Ä°stemci yokken de yayÄ±n servisini aktif tut.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// GÃ¶nderim sÄ±rasÄ±nda yavaÅŸ istemciler iÃ§in yazma timeout sÃ¼resi.
    /// </summary>
    public int SendTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Heartbeat yayÄ±n aralÄ±ÄŸÄ±.
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Bir istemci baÄŸlantÄ±sÄ±na izin verilen maksimum kuyruk uzunluÄŸu.
    /// </summary>
    public int MaxPendingMessagesPerClient { get; set; } = 256;

    /// <summary>
    /// WebSocket katmanÄ±nda detay loglarÄ± aÃ§Ä±lsÄ±n mÄ±.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}
