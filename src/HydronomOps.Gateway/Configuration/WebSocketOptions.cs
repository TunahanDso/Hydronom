namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// WebSocket yayın katmanı ayarları.
/// </summary>
public sealed class WebSocketOptions
{
    public const string SectionName = "WebSocket";

    /// <summary>
    /// WebSocket endpoint yolu.
    /// </summary>
    public string Path { get; set; } = "/ws";

    /// <summary>
    /// İstemci yokken de yayın servisini aktif tut.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gönderim sırasında yavaş istemciler için yazma timeout süresi.
    /// </summary>
    public int SendTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Heartbeat yayın aralığı.
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Bir istemci bağlantısına izin verilen maksimum kuyruk uzunluğu.
    /// </summary>
    public int MaxPendingMessagesPerClient { get; set; } = 256;

    /// <summary>
    /// WebSocket katmanında detay logları açılsın mı.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}