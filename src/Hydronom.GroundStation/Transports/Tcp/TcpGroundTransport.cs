namespace Hydronom.GroundStation.Transports.Tcp;

using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Hydronom.Core.Fleet;
using Hydronom.Core.Communication;

/// <summary>
/// Ground Station tarafından TCP üzerinden HydronomEnvelope gönderen ve alabilen gerçek transport implementasyonudur.
/// 
/// Send tarafı:
/// - TcpClient ile hedefe bağlanır.
/// - HydronomEnvelope JSON olarak serialize edilir.
/// - Varsayılan olarak NDJSON framing ile tek satır JSON + newline gönderilir.
/// 
/// Receive tarafı:
/// - EnableReceiveListener true ise TcpListener açar.
/// - Gelen TCP client bağlantılarından satır satır NDJSON okur.
/// - Her satırı HydronomEnvelope olarak deserialize eder.
/// - ReceiveAsync üzerinden envelope üretir.
/// 
/// Timeout/exception durumları üst katmanda GroundTransportManager ve GroundTransportReceiver tarafından işlenir.
/// </summary>
public sealed class TcpGroundTransport : ITransport, IAsyncDisposable
{
    private readonly TcpGroundTransportOptions _options;
    private readonly object _sync = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private TcpListener? _listener;

    public TcpGroundTransport(TcpGroundTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Name))
            throw new ArgumentException("TCP transport adı boş olamaz.", nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new ArgumentException("TCP host boş olamaz.", nameof(options));

        if (_options.Port <= 0 || _options.Port > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "TCP port 1-65535 aralığında olmalıdır.");

        if (string.IsNullOrWhiteSpace(_options.ListenHost))
            throw new ArgumentException("TCP listener host boş olamaz.", nameof(options));

        if (_options.ListenPort < 0 || _options.ListenPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "TCP listener port 0-65535 aralığında olmalıdır.");
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
    /// TCP outbound client bağlantı durumu.
    /// 
    /// Not:
    /// Receive listener açık olsa bile outbound client bağlı değilse bu değer false dönebilir.
    /// Bu normaldir; listener durumu ayrı olarak IsListening ile okunabilir.
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
    /// TCP inbound listener açık mı?
    /// </summary>
    public bool IsListening
    {
        get
        {
            lock (_sync)
                return _listener is not null;
        }
    }

    /// <summary>
    /// Listener'ın bağlandığı gerçek port.
    /// 
    /// ListenPort = 0 ise sistemin seçtiği port buradan okunabilir.
    /// </summary>
    public int? BoundListenPort => _options.BoundListenPort;

    /// <summary>
    /// TCP outbound bağlantısını başlatır.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        await DisconnectOutboundAsync(cancellationToken);

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
    /// TCP outbound bağlantısını ve inbound listener'ı kapatır.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectOutboundAsync(cancellationToken);
        StopListener();
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
                await DisconnectOutboundAsync(CancellationToken.None);

            throw;
        }
    }

    /// <summary>
    /// TCP listener üzerinden gelen NDJSON HydronomEnvelope mesajlarını üretir.
    /// 
    /// EnableReceiveListener false ise boş akış döner.
    /// EnableReceiveListener true ise listener açılır ve cancellationToken iptal edilene kadar
    /// gelen client bağlantılarından satır satır JSON okur.
    /// </summary>
    public async IAsyncEnumerable<HydronomEnvelope> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_options.EnableReceiveListener)
            yield break;

        var listener = EnsureListenerStarted();

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? acceptedClient = null;

            try
            {
                acceptedClient = await listener.AcceptTcpClientAsync(cancellationToken);
                acceptedClient.NoDelay = _options.NoDelay;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (acceptedClient is null)
            {
                await Task.Delay(_options.AcceptLoopDelay, cancellationToken);
                continue;
            }

            await foreach (var envelope in ReadClientEnvelopesAsync(acceptedClient, cancellationToken))
                yield return envelope;
        }
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

    /// <summary>
    /// TCP listener'ı başlatır veya mevcut listener'ı döndürür.
    /// </summary>
    private TcpListener EnsureListenerStarted()
    {
        lock (_sync)
        {
            if (_listener is not null)
                return _listener;

            var ipAddress = ResolveListenAddress(_options.ListenHost);

            var listener = new TcpListener(
                ipAddress,
                _options.ListenPort);

            listener.Start();

            _options.BoundListenPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            _listener = listener;

            return listener;
        }
    }

    /// <summary>
    /// Listener'ı durdurur.
    /// </summary>
    private void StopListener()
    {
        TcpListener? listener;

        lock (_sync)
        {
            listener = _listener;
            _listener = null;
            _options.BoundListenPort = null;
        }

        try
        {
            listener?.Stop();
        }
        catch
        {
            // Listener kapanırken oluşan hata operasyonu etkilememeli.
        }
    }

    /// <summary>
    /// Outbound client bağlantısını kapatır.
    /// </summary>
    private Task DisconnectOutboundAsync(CancellationToken cancellationToken = default)
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
    /// Tek TCP client bağlantısından NDJSON envelope satırlarını okur.
    /// </summary>
    private async IAsyncEnumerable<HydronomEnvelope> ReadClientEnvelopesAsync(
        TcpClient client,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;

                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (line is null)
                    yield break;

                if (_options.IgnoreEmptyReceiveLines && string.IsNullOrWhiteSpace(line))
                    continue;

                HydronomEnvelope? envelope;

                try
                {
                    envelope = DeserializeEnvelope(line);
                }
                catch
                {
                    if (_options.ContinueOnInvalidReceiveJson)
                        continue;

                    throw;
                }

                if (envelope is not null)
                    yield return envelope;
            }
        }
    }

    /// <summary>
    /// NDJSON satırını HydronomEnvelope'a dönüştürür.
    /// </summary>
    private static HydronomEnvelope? DeserializeEnvelope(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        var envelope = JsonSerializer.Deserialize<HydronomEnvelope>(
            line,
            options);

        if (envelope is null)
            return null;

        if (!root.TryGetProperty("payload", out var payloadElement) &&
            !root.TryGetProperty("Payload", out payloadElement))
        {
            return envelope;
        }

        var typedPayload = DeserializePayload(
            envelope.MessageType,
            payloadElement,
            options);

        return envelope with
        {
            Payload = typedPayload ?? envelope.Payload
        };
    }
    private static object? DeserializePayload(
        string messageType,
        JsonElement payloadElement,
        JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return null;

        if (payloadElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return messageType switch
        {
            "FleetHeartbeat" => payloadElement.Deserialize<FleetHeartbeat>(options),
            "FleetCommand" => payloadElement.Deserialize<FleetCommand>(options),
            "FleetCommandResult" => payloadElement.Deserialize<FleetCommandResult>(options),
            "VehicleNodeStatus" => payloadElement.Deserialize<VehicleNodeStatus>(options),
            _ => null
        };
    }

    /// <summary>
    /// Listener host değerini IPAddress'e çevirir.
    /// </summary>
    private static IPAddress ResolveListenAddress(string host)
    {
        if (string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "*", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Any;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return IPAddress.Loopback;

        if (IPAddress.TryParse(host, out var address))
            return address;

        return IPAddress.Loopback;
    }
}