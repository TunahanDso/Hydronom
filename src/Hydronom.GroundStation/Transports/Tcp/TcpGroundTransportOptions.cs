namespace Hydronom.GroundStation.Transports.Tcp;

/// <summary>
/// TcpGroundTransport iÃ§in baÄŸlantÄ±, framing ve receive/listener ayarlarÄ±nÄ± tutar.
/// 
/// Bu options modeli hem outbound TCP send tarafÄ±nÄ± hem de inbound TCP NDJSON listener tarafÄ±nÄ±
/// aynÄ± transport instance iÃ§inde yapÄ±landÄ±rmak iÃ§in kullanÄ±lÄ±r.
/// </summary>
public sealed record TcpGroundTransportOptions
{
    /// <summary>
    /// Transport instance adÄ±.
    /// 
    /// Log, diagnostics ve registry iÃ§inde okunabilir isim olarak kullanÄ±lÄ±r.
    /// </summary>
    public string Name { get; init; } = "tcp-ground";

    /// <summary>
    /// Outbound gÃ¶nderim iÃ§in hedef TCP host.
    /// 
    /// Ã–rnek:
    /// - 127.0.0.1
    /// - 192.168.1.50
    /// - vehicle-alpha.local
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// Outbound gÃ¶nderim iÃ§in hedef TCP port.
    /// </summary>
    public int Port { get; init; } = 5060;

    /// <summary>
    /// Inbound receive/listener aktif olsun mu?
    /// 
    /// true yapÄ±lÄ±rsa ReceiveAsync Ã§aÄŸrÄ±sÄ± TcpListener aÃ§ar ve gelen NDJSON satÄ±rlarÄ±nÄ±
    /// HydronomEnvelope olarak Ã¼retir.
    /// </summary>
    public bool EnableReceiveListener { get; init; } = false;

    /// <summary>
    /// Inbound listener host/IP.
    /// 
    /// Ã–rnek:
    /// - 127.0.0.1 sadece local test iÃ§in
    /// - 0.0.0.0 tÃ¼m network arayÃ¼zlerinden dinlemek iÃ§in
    /// </summary>
    public string ListenHost { get; init; } = "127.0.0.1";

    /// <summary>
    /// Inbound listener port.
    /// 
    /// 0 verilirse sistem uygun boÅŸ port seÃ§er.
    /// Testlerde 0 kullanmak faydalÄ±dÄ±r.
    /// </summary>
    public int ListenPort { get; init; } = 0;

    /// <summary>
    /// Listener gerÃ§ek olarak hangi porta baÄŸlandÄ±?
    /// 
    /// ListenPort = 0 olduÄŸunda test kodu bu deÄŸeri okuyarak client baÄŸlantÄ±sÄ± aÃ§abilir.
    /// </summary>
    public int? BoundListenPort { get; internal set; }

    /// <summary>
    /// Her mesajdan sonra newline eklensin mi?
    /// 
    /// Hydronom tarafÄ±nda NDJSON/line-delimited JSON standardÄ±na uygun olmasÄ± iÃ§in
    /// varsayÄ±lan olarak true tutulur.
    /// </summary>
    public bool UseNdjsonFraming { get; init; } = true;

    /// <summary>
    /// ReceiveAsync tarafÄ±nda boÅŸ satÄ±rlar yok sayÄ±lsÄ±n mÄ±?
    /// </summary>
    public bool IgnoreEmptyReceiveLines { get; init; } = true;

    /// <summary>
    /// Deserialize edilemeyen receive satÄ±rlarÄ±nda loop devam etsin mi?
    /// 
    /// true ise bozuk satÄ±r atlanÄ±r.
    /// false ise exception dÄ±ÅŸarÄ± fÄ±rlatÄ±lÄ±r.
    /// </summary>
    public bool ContinueOnInvalidReceiveJson { get; init; } = true;

    /// <summary>
    /// SendAsync sÄ±rasÄ±nda baÄŸlantÄ± kopuksa otomatik baÄŸlanmayÄ± denesin mi?
    /// </summary>
    public bool AutoConnectOnSend { get; init; } = true;

    /// <summary>
    /// ConnectAsync iÃ§in timeout sÃ¼resi.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// TCP stream write timeout sÃ¼resi.
    /// </summary>
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Receive tarafÄ±nda client kabul etme timeout/iptal kontrol aralÄ±ÄŸÄ±.
    /// </summary>
    public TimeSpan AcceptLoopDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// TCP keep-alive aktif edilsin mi?
    /// </summary>
    public bool EnableKeepAlive { get; init; } = true;

    /// <summary>
    /// KÃ¼Ã§Ã¼k JSON komutlarÄ±nda Nagle gecikmesini azaltmak iÃ§in NoDelay aktif edilsin mi?
    /// </summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    /// BaÄŸlantÄ± kopunca SendAsync sonunda client temizlensin mi?
    /// </summary>
    public bool ResetClientOnSendFailure { get; init; } = true;
}
