namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// Runtime TCP giriş ayarları.
/// </summary>
public sealed class RuntimeTcpOptions
{
    public const string SectionName = "RuntimeTcp";

    /// <summary>
    /// Runtime'ın dinlediği IP veya host.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Runtime TCP portu.
    /// </summary>
    public int Port { get; set; } = 5060;

    /// <summary>
    /// İlk bağlantı kurulumunda otomatik bağlan.
    /// </summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>
    /// Bağlantı koparsa yeniden deneme aralığı.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 3000;

    /// <summary>
    /// Soket bağlantı zaman aşımı.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Soket okuma timeout süresi.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Soket yazma timeout süresi.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// NDJSON satır tampon boyutu.
    /// </summary>
    public int BufferSize { get; set; } = 8192;

    /// <summary>
    /// Geriye dönük uyumluluk için alternatif buffer adı.
    /// </summary>
    public int ReceiveBufferSize => BufferSize;
}