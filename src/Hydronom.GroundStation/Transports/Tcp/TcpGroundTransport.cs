namespace Hydronom.GroundStation.Transports.Tcp;

using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafından TCP üzerinden HydronomEnvelope gönderen gerçek transport implementasyonudur.
/// 
/// İlk faz kapsamı:
/// - TcpClient ile hedefe bağlanır.
/// - HydronomEnvelope JSON olarak serialize edilir.
/// - Varsayılan olarak NDJSON framing ile tek satır JSON + newline gönderilir.
/// - SendAsync başarılı olursa üst katman bunu Sent/Acked olarak yorumlayabilir.
/// - Timeout/exception durumları GroundTransportManager tarafından yakalanır.
/// 
/// Bu sınıf şu anda ReceiveAsync tarafında gerçek dinleme yapmaz.
/// Receive/listener pipeline sonraki pakette ayrı geliştirilecektir.
/// </summary>
public sealed class TcpGroundTransport : ITransport, IAsyncDisposable
{
    private readonly TcpGroundTransportOptions _options;
    private readonly object _sync = new();

    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpGroundTransport(TcpGroundTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Name))
            throw new ArgumentException("TCP transport adı boş olamaz.", nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new ArgumentException("TCP host boş olamaz.", nameof(options));

        if (_options.Port <= 0 || _options.Port > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "TCP port 1-65535 aralığında olmalıdır.");
    }

    /// <summary>
    /// Transport instance adı.
    /// </summary>
    public string Name => _options.Name;

    /// <summary>
    /// Transport türü.
    /// </summary>
    public TransportKind Kind => TransportKind.Tcp;

    /// <summary>
    /// TCP client bağlantı durumu.
    /// 
    /// TcpClient.Connected tek başına her zaman mutlak gerçekliği göstermeyebilir;
    /// ancak ilk faz için pratik bağlantı göstergesi olarak yeterlidir.
    /// </summary>
    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _client is not null &&
                       _stream is not null &&
                       _client.Connected;
            }
        }
    }

    /// <summary>
    /// TCP bağlantısını başlatır.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        await DisconnectAsync(cancellationToken);

        var client = new TcpClient
        {
            NoDelay = _options.NoDelay
        };

        if (_options.EnableKeepAlive)
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ConnectTimeout);

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                timeoutCts.Token);

            var stream = client.GetStream();
            stream.WriteTimeout = (int)Math.Max(1, _options.WriteTimeout.TotalMilliseconds);

            lock (_sync)
            {
                _client = client;
                _stream = stream;
            }
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// TCP bağlantısını kapatır.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        TcpClient? client;
        NetworkStream? stream;

        lock (_sync)
        {
            client = _client;
            stream = _stream;
            _client = null;
            _stream = null;
        }

        try
        {
            stream?.Dispose();
        }
        catch
        {
            // Kapanış sırasında stream dispose hatası operasyonu etkilememeli.
        }

        try
        {
            client?.Close();
            client?.Dispose();
        }
        catch
        {
            // Kapanış sırasında socket dispose hatası operasyonu etkilememeli.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Envelope'u TCP üzerinden JSON/NDJSON olarak gönderir.
    /// </summary>
    public async Task SendAsync(
        HydronomEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        if (!IsConnected)
        {
            if (!_options.AutoConnectOnSend)
                throw new InvalidOperationException($"TCP transport '{Name}' bağlı değil.");

            await ConnectAsync(cancellationToken);
        }

        var payload = SerializeEnvelope(envelope);
        var bytes = Encoding.UTF8.GetBytes(payload);

        NetworkStream? stream;

        lock (_sync)
        {
            stream = _stream;
        }

        if (stream is null)
            throw new InvalidOperationException($"TCP transport '{Name}' stream hazırlanamamış.");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.WriteTimeout);

            await stream.WriteAsync(
                bytes.AsMemory(0, bytes.Length),
                timeoutCts.Token);

            await stream.FlushAsync(timeoutCts.Token);
        }
        catch
        {
            if (_options.ResetClientOnSendFailure)
                await DisconnectAsync(CancellationToken.None);

            throw;
        }
    }

    /// <summary>
    /// İlk fazda TCP receive stream aktif değildir.
    /// 
    /// Receive/listener pipeline ayrı pakette eklenecektir.
    /// </summary>
    public async IAsyncEnumerable<HydronomEnvelope> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// TCP client kaynaklarını asenkron dispose eder.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Envelope'u JSON string'e dönüştürür.
    /// 
    /// NDJSON framing aktifse CR/LF temizlenir ve sona newline eklenir.
    /// </summary>
    private string SerializeEnvelope(HydronomEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(
            envelope,
            new JsonSerializerOptions
            {
                WriteIndented = false
            });

        if (!_options.UseNdjsonFraming)
            return json;

        json = json
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        return json + "\n";
    }
}