namespace Hydronom.GroundStation.Transports.Tcp;

/// <summary>
/// TcpGroundTransport için bağlantı ve framing ayarlarını tutar.
/// 
/// İlk fazda TCP transport yalnızca send tarafını gerçekler.
/// Receive/listener tarafı sonraki pakette ayrı bir akış olarak eklenecektir.
/// </summary>
public sealed record TcpGroundTransportOptions
{
    /// <summary>
    /// Transport instance adı.
    /// 
    /// Log, diagnostics ve registry içinde okunabilir isim olarak kullanılır.
    /// </summary>
    public string Name { get; init; } = "tcp-ground";

    /// <summary>
    /// Hedef TCP host.
    /// 
    /// Örnek:
    /// - 127.0.0.1
    /// - 192.168.1.50
    /// - vehicle-alpha.local
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// Hedef TCP port.
    /// </summary>
    public int Port { get; init; } = 5060;

    /// <summary>
    /// Her mesajdan sonra newline eklensin mi?
    /// 
    /// Hydronom tarafında NDJSON/line-delimited JSON standardına uygun olması için
    /// varsayılan olarak true tutulur.
    /// </summary>
    public bool UseNdjsonFraming { get; init; } = true;

    /// <summary>
    /// SendAsync sırasında bağlantı kopuksa otomatik bağlanmayı denesin mi?
    /// </summary>
    public bool AutoConnectOnSend { get; init; } = true;

    /// <summary>
    /// ConnectAsync için timeout süresi.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// TCP stream write timeout süresi.
    /// </summary>
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// TCP keep-alive aktif edilsin mi?
    /// </summary>
    public bool EnableKeepAlive { get; init; } = true;

    /// <summary>
    /// Küçük JSON komutlarında Nagle gecikmesini azaltmak için NoDelay aktif edilsin mi?
    /// </summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    /// Bağlantı kopunca SendAsync sonunda client temizlensin mi?
    /// </summary>
    public bool ResetClientOnSendFailure { get; init; } = true;
}