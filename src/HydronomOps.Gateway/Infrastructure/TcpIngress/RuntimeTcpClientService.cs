using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using HydronomOps.Gateway.Configuration;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

/// <summary>
/// Runtime TCP kaynaÄŸÄ±na baÄŸlanÄ±r ve NDJSON satÄ±r bazlÄ± veri okur.
/// </summary>
public sealed class RuntimeTcpClientService : IAsyncDisposable
{
    private readonly RuntimeTcpOptions _options;
    private readonly ILogger<RuntimeTcpClientService> _logger;

    private TcpClient? _client;
    private NetworkStream? _stream;

    // Gelen byte'larÄ± burada biriktiriyoruz.
    private readonly byte[] _readBuffer = new byte[8192];
    private readonly List<byte> _lineBuffer = new(16384);

    // GÃ¼venlik amaÃ§lÄ± Ã¼st sÄ±nÄ±r. NDJSON frame Ã§ok bÃ¼yÃ¼rse parser zaten zorlanÄ±r.
    private const int MaxFrameBytes = 1024 * 1024; // 1 MB

    public RuntimeTcpClientService(
        IOptions<RuntimeTcpOptions> options,
        ILogger<RuntimeTcpClientService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Aktif TCP baÄŸlantÄ±sÄ± var mÄ±.
    /// </summary>
    public bool IsConnected => _client?.Connected == true && _stream is not null;

    /// <summary>
    /// Runtime'a TCP baÄŸlantÄ±sÄ± aÃ§ar.
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
                "Runtime TCP baÄŸlantÄ±sÄ± aÃ§Ä±lÄ±yor. Host={Host}, Port={Port}",
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
                "Runtime TCP baÄŸlantÄ±sÄ± aÃ§Ä±ldÄ±. Host={Host}, Port={Port}",
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
                // Sessiz geÃ§.
            }

            throw;
        }
    }

    /// <summary>
    /// Runtime'tan tek NDJSON satÄ±rÄ± okur.
    /// BaÄŸlantÄ± kapanÄ±rsa null dÃ¶ner.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("TCP stream hazÄ±r deÄŸil. Ã–nce ConnectAsync Ã§aÄŸrÄ±lmalÄ±.");
        }

        while (true)
        {
            // Ã–nce elimizde daha Ã¶nce birikmiÅŸ veride satÄ±r sonu var mÄ± bakalÄ±m.
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

            // KarÅŸÄ± taraf baÄŸlantÄ±yÄ± kapattÄ±.
            if (bytesRead == 0)
            {
                // EÄŸer elde kalan tamamlanmamÄ±ÅŸ ama boÅŸ olmayan veri varsa
                // bunu sessizce line diye dÃ¶ndÃ¼rmeyelim; frame yarÄ±m kalmÄ±ÅŸ demektir.
                if (_lineBuffer.Count > 0)
                {
                    var partial = Encoding.UTF8.GetString(_lineBuffer.ToArray()).Trim();
                    _lineBuffer.Clear();

                    if (!string.IsNullOrWhiteSpace(partial))
                    {
                        _logger.LogWarning(
                            "Runtime baÄŸlantÄ±sÄ± kapanÄ±rken tamamlanmamÄ±ÅŸ frame atÄ±ldÄ±. Uzunluk={Length}",
                            partial.Length);
                    }
                }

                return null;
            }

            AppendBytes(_readBuffer, bytesRead);
        }
    }

    /// <summary>
    /// Runtime'a NDJSON uyumlu tek satÄ±r veri yollar.
    /// </summary>
    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("TCP stream hazÄ±r deÄŸil. Ã–nce ConnectAsync Ã§aÄŸrÄ±lmalÄ±.");
        }

        var normalized = NormalizeNdjsonLine(line);
        var bytes = Encoding.UTF8.GetBytes(normalized);

        await _stream.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// AÃ§Ä±k baÄŸlantÄ±yÄ± kapatÄ±r.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Runtime TCP baÄŸlantÄ±sÄ± kapatÄ±lÄ±yor.");

        _lineBuffer.Clear();

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Sessiz geÃ§.
        }

        try
        {
            _client?.Close();
            _client?.Dispose();
        }
        catch
        {
            // Sessiz geÃ§.
        }

        _stream = null;
        _client = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Okunan byte'larÄ± iÃ§ buffer'a ekler.
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
                $"NDJSON frame izin verilen Ã¼st sÄ±nÄ±rÄ± aÅŸtÄ±. Max={MaxFrameBytes} byte.");
        }

        for (var i = 0; i < count; i++)
        {
            _lineBuffer.Add(buffer[i]);
        }
    }

    /// <summary>
    /// Ä°Ã§ buffer'dan ilk tamamlanmÄ±ÅŸ satÄ±rÄ± ayÄ±klar.
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

            // CRLF geldiyse sondaki \r karakterini dÃ¼ÅŸ.
            if (length > 0 && _lineBuffer[length - 1] == (byte)'\r')
            {
                length--;
            }

            var lineBytes = _lineBuffer.GetRange(0, length).ToArray();

            // Okunan satÄ±rÄ± ve satÄ±r sonunu buffer'dan sil.
            _lineBuffer.RemoveRange(0, i + 1);

            var decoded = Encoding.UTF8.GetString(lineBytes).Trim();

            // BoÅŸ satÄ±rlarÄ± atla, bir sonraki satÄ±rÄ± dene.
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
    /// NDJSON tek satÄ±r normalizasyonu yapar.
    /// </summary>
    private static string NormalizeNdjsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return "\n";
        }

        // GÃ¶nderilen payload tek satÄ±r olmalÄ±.
        // GerÃ§ek satÄ±r sonlarÄ±nÄ± kaÃ§Ä±ÅŸlÄ± hale getiriyoruz.
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
