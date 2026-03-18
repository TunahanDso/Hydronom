using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using HydronomOps.Gateway.Configuration;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

/// <summary>
/// Runtime TCP kaynağına bağlanır ve NDJSON satır bazlı veri okur.
/// </summary>
public sealed class RuntimeTcpClientService : IAsyncDisposable
{
    private readonly RuntimeTcpOptions _options;
    private readonly ILogger<RuntimeTcpClientService> _logger;

    private TcpClient? _client;
    private NetworkStream? _stream;

    // Gelen byte'ları burada biriktiriyoruz.
    private readonly byte[] _readBuffer = new byte[8192];
    private readonly List<byte> _lineBuffer = new(16384);

    // Güvenlik amaçlı üst sınır. NDJSON frame çok büyürse parser zaten zorlanır.
    private const int MaxFrameBytes = 1024 * 1024; // 1 MB

    public RuntimeTcpClientService(
        IOptions<RuntimeTcpOptions> options,
        ILogger<RuntimeTcpClientService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Aktif TCP bağlantısı var mı.
    /// </summary>
    public bool IsConnected => _client?.Connected == true && _stream is not null;

    /// <summary>
    /// Runtime'a TCP bağlantısı açar.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        await DisconnectAsync(cancellationToken);

        var client = new TcpClient
        {
            NoDelay = true
        };

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_options.ConnectTimeoutMs > 0)
            {
                timeoutCts.CancelAfter(_options.ConnectTimeoutMs);
            }

            _logger.LogInformation(
                "Runtime TCP bağlantısı açılıyor. Host={Host}, Port={Port}",
                _options.Host,
                _options.Port);

            await client.ConnectAsync(_options.Host, _options.Port, timeoutCts.Token);

            var stream = client.GetStream();
            stream.ReadTimeout = Timeout.Infinite;
            stream.WriteTimeout = Timeout.Infinite;

            _client = client;
            _stream = stream;
            _lineBuffer.Clear();

            _logger.LogInformation(
                "Runtime TCP bağlantısı açıldı. Host={Host}, Port={Port}",
                _options.Host,
                _options.Port);
        }
        catch
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // Sessiz geç.
            }

            throw;
        }
    }

    /// <summary>
    /// Runtime'tan tek NDJSON satırı okur.
    /// Bağlantı kapanırsa null döner.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("TCP stream hazır değil. Önce ConnectAsync çağrılmalı.");
        }

        while (true)
        {
            // Önce elimizde daha önce birikmiş veride satır sonu var mı bakalım.
            if (TryExtractLineFromBuffer(out var bufferedLine))
            {
                return bufferedLine;
            }

            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(_readBuffer.AsMemory(0, _readBuffer.Length), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Karşı taraf bağlantıyı kapattı.
            if (bytesRead == 0)
            {
                // Eğer elde kalan tamamlanmamış ama boş olmayan veri varsa
                // bunu sessizce line diye döndürmeyelim; frame yarım kalmış demektir.
                if (_lineBuffer.Count > 0)
                {
                    var partial = Encoding.UTF8.GetString(_lineBuffer.ToArray()).Trim();
                    _lineBuffer.Clear();

                    if (!string.IsNullOrWhiteSpace(partial))
                    {
                        _logger.LogWarning(
                            "Runtime bağlantısı kapanırken tamamlanmamış frame atıldı. Uzunluk={Length}",
                            partial.Length);
                    }
                }

                return null;
            }

            AppendBytes(_readBuffer, bytesRead);
        }
    }

    /// <summary>
    /// Runtime'a NDJSON uyumlu tek satır veri yollar.
    /// </summary>
    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("TCP stream hazır değil. Önce ConnectAsync çağrılmalı.");
        }

        var normalized = NormalizeNdjsonLine(line);
        var bytes = Encoding.UTF8.GetBytes(normalized);

        await _stream.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Açık bağlantıyı kapatır.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Runtime TCP bağlantısı kapatılıyor.");

        _lineBuffer.Clear();

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Sessiz geç.
        }

        try
        {
            _client?.Close();
            _client?.Dispose();
        }
        catch
        {
            // Sessiz geç.
        }

        _stream = null;
        _client = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Okunan byte'ları iç buffer'a ekler.
    /// </summary>
    private void AppendBytes(byte[] buffer, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (_lineBuffer.Count + count > MaxFrameBytes)
        {
            _lineBuffer.Clear();
            throw new InvalidDataException(
                $"NDJSON frame izin verilen üst sınırı aştı. Max={MaxFrameBytes} byte.");
        }

        for (var i = 0; i < count; i++)
        {
            _lineBuffer.Add(buffer[i]);
        }
    }

    /// <summary>
    /// İç buffer'dan ilk tamamlanmış satırı ayıklar.
    /// </summary>
    private bool TryExtractLineFromBuffer(out string? line)
    {
        for (var i = 0; i < _lineBuffer.Count; i++)
        {
            if (_lineBuffer[i] != (byte)'\n')
            {
                continue;
            }

            var length = i;

            // CRLF geldiyse sondaki \r karakterini düş.
            if (length > 0 && _lineBuffer[length - 1] == (byte)'\r')
            {
                length--;
            }

            var lineBytes = _lineBuffer.GetRange(0, length).ToArray();

            // Okunan satırı ve satır sonunu buffer'dan sil.
            _lineBuffer.RemoveRange(0, i + 1);

            var decoded = Encoding.UTF8.GetString(lineBytes).Trim();

            // Boş satırları atla, bir sonraki satırı dene.
            if (string.IsNullOrWhiteSpace(decoded))
            {
                return TryExtractLineFromBuffer(out line);
            }

            line = decoded;
            return true;
        }

        line = null;
        return false;
    }

    /// <summary>
    /// NDJSON tek satır normalizasyonu yapar.
    /// </summary>
    private static string NormalizeNdjsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return "\n";
        }

        // Gönderilen payload tek satır olmalı.
        // Gerçek satır sonlarını kaçışlı hale getiriyoruz.
        line = line.Replace("\r", string.Empty);
        line = line.Replace("\n", "\\n");

        if (!line.EndsWith("\n", StringComparison.Ordinal))
        {
            line += "\n";
        }

        return line;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}