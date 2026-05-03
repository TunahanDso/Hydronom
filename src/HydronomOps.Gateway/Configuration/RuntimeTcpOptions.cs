namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// Runtime TCP giriÅŸ ayarlarÄ±.
/// </summary>
public sealed class RuntimeTcpOptions
{
    public const string SectionName = "RuntimeTcp";

    /// <summary>
    /// Runtime'Ä±n dinlediÄŸi IP veya host.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Runtime TCP portu.
    /// </summary>
    public int Port { get; set; } = 5060;

    /// <summary>
    /// Ä°lk baÄŸlantÄ± kurulumunda otomatik baÄŸlan.
    /// </summary>
    public bool AutoConnect { get; set; } = true;

    /// <summary>
    /// BaÄŸlantÄ± koparsa yeniden deneme aralÄ±ÄŸÄ±.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 3000;

    /// <summary>
    /// Soket baÄŸlantÄ± zaman aÅŸÄ±mÄ±.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Soket okuma timeout sÃ¼resi.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Soket yazma timeout sÃ¼resi.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// NDJSON satÄ±r tampon boyutu.
    /// </summary>
    public int BufferSize { get; set; } = 8192;

    /// <summary>
    /// Geriye dÃ¶nÃ¼k uyumluluk iÃ§in alternatif buffer adÄ±.
    /// </summary>
    public int ReceiveBufferSize => BufferSize;
}
