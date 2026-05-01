namespace Hydronom.GroundStation.Transports.Tcp;

/// <summary>
/// TcpGroundTransport için bağlantı, framing ve receive/listener ayarlarını tutar.
/// 
/// Bu options modeli hem outbound TCP send tarafını hem de inbound TCP NDJSON listener tarafını
/// aynı transport instance içinde yapılandırmak için kullanılır.
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
    /// Outbound gönderim için hedef TCP host.
    /// 
    /// Örnek:
    /// - 127.0.0.1
    /// - 192.168.1.50
    /// - vehicle-alpha.local
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// Outbound gönderim için hedef TCP port.
    /// </summary>
    public int Port { get; init; } = 5060;

    /// <summary>
    /// Inbound receive/listener aktif olsun mu?
    /// 
    /// true yapılırsa ReceiveAsync çağrısı TcpListener açar ve gelen NDJSON satırlarını
    /// HydronomEnvelope olarak üretir.
    /// </summary>
    public bool EnableReceiveListener { get; init; } = false;

    /// <summary>
    /// Inbound listener host/IP.
    /// 
    /// Örnek:
    /// - 127.0.0.1 sadece local test için
    /// - 0.0.0.0 tüm network arayüzlerinden dinlemek için
    /// </summary>
    public string ListenHost { get; init; } = "127.0.0.1";

    /// <summary>
    /// Inbound listener port.
    /// 
    /// 0 verilirse sistem uygun boş port seçer.
    /// Testlerde 0 kullanmak faydalıdır.
    /// </summary>
    public int ListenPort { get; init; } = 0;

    /// <summary>
    /// Listener gerçek olarak hangi porta bağlandı?
    /// 
    /// ListenPort = 0 olduğunda test kodu bu değeri okuyarak client bağlantısı açabilir.
    /// </summary>
    public int? BoundListenPort { get; internal set; }

    /// <summary>
    /// Her mesajdan sonra newline eklensin mi?
    /// 
    /// Hydronom tarafında NDJSON/line-delimited JSON standardına uygun olması için
    /// varsayılan olarak true tutulur.
    /// </summary>
    public bool UseNdjsonFraming { get; init; } = true;

    /// <summary>
    /// ReceiveAsync tarafında boş satırlar yok sayılsın mı?
    /// </summary>
    public bool IgnoreEmptyReceiveLines { get; init; } = true;

    /// <summary>
    /// Deserialize edilemeyen receive satırlarında loop devam etsin mi?
    /// 
    /// true ise bozuk satır atlanır.
    /// false ise exception dışarı fırlatılır.
    /// </summary>
    public bool ContinueOnInvalidReceiveJson { get; init; } = true;

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
    /// Receive tarafında client kabul etme timeout/iptal kontrol aralığı.
    /// </summary>
    public TimeSpan AcceptLoopDelay { get; init; } = TimeSpan.FromMilliseconds(50);

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