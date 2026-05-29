using System.IO.Ports;
using System.Threading.Channels;

namespace Hydronom.Core.Communication.Transport.SerialRf;

public sealed class SerialRfHydronomPacketTransport : IHydronomPacketTransport
{
    private readonly SerialRfHydronomTransportOptions _options;
    private readonly Channel<HydronomTransportPacket> _receiveChannel;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    private bool _isRunning;
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

    private long _sentPackets;
    private long _receivedPackets;
    private long _sentBytes;
    private long _receivedBytes;
    private long _droppedPackets;

    public SerialRfHydronomPacketTransport(
        SerialRfHydronomTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        TransportId = string.IsNullOrWhiteSpace(_options.TransportId)
            ? $"serial-rf-{_options.PortName}"
            : _options.TransportId;

        _receiveChannel = Channel.CreateBounded<HydronomTransportPacket>(
            new BoundedChannelOptions(Math.Max(8, _options.ReceiveChannelCapacity))
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = false
            });
    }

    public string TransportId { get; }

    public string TransportKind => SerialRfLinkDefaults.TransportKind;

    public string PortName => _options.PortName;

    public int BaudRate => _options.BaudRate;

    public bool IsRunning => _isRunning;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _port = new SerialPort(
            _options.PortName,
            _options.BaudRate,
            _options.Parity,
            _options.DataBits,
            _options.StopBits)
        {
            ReadTimeout = Math.Max(10, _options.ReadTimeoutMs),
            WriteTimeout = Math.Max(10, _options.WriteTimeoutMs),
            DtrEnable = _options.DtrEnable,
            RtsEnable = _options.RtsEnable,
            NewLine = "\n"
        };

        _port.Open();

        _isRunning = true;
        _startedAt = DateTimeOffset.UtcNow;

        _readTask = Task.Run(
            () => ReadLoopAsync(_cts.Token),
            CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // Stop best-effort.
        }

        try
        {
            _port?.Close();
            _port?.Dispose();
        }
        catch
        {
            // Stop best-effort.
        }

        if (_readTask is not null)
        {
            try
            {
                await _readTask.WaitAsync(
                        TimeSpan.FromSeconds(2),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Shutdown best-effort.
            }
        }
    }

    public async ValueTask SendAsync(
        HydronomTransportPacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isRunning || _port is null || !_port.IsOpen)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var forwarded = packet with
            {
                TransportId = TransportId,
                ChannelId = string.IsNullOrWhiteSpace(packet.ChannelId)
                    ? _options.ChannelId
                    : packet.ChannelId,
                SourceId = string.IsNullOrWhiteSpace(packet.SourceId)
                    ? _options.SourceId
                    : packet.SourceId,
                TargetId = string.IsNullOrWhiteSpace(packet.TargetId)
                    ? _options.TargetId
                    : packet.TargetId,
                CreatedAt = DateTimeOffset.UtcNow,
                Bytes = packet.Bytes.ToArray()
            };

            SerialRfHydronomPacketFramer.WritePacket(
                _port,
                forwarded,
                _options);

            Interlocked.Increment(ref _sentPackets);
            Interlocked.Add(ref _sentBytes, forwarded.SizeBytes);
        }
        catch
        {
            Interlocked.Increment(ref _droppedPackets);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask<HydronomTransportPacket?> TryReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_receiveChannel.Reader.TryRead(out var packet))
        {
            return packet;
        }

        await Task.Yield();
        return null;
    }

    public async IAsyncEnumerable<HydronomTransportPacket> ReceiveAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _receiveChannel.Reader.WaitToReadAsync(cancellationToken)
                   .ConfigureAwait(false))
        {
            while (_receiveChannel.Reader.TryRead(out var packet))
            {
                yield return packet;
            }
        }
    }

    public HydronomTransportStats SnapshotStats()
    {
        return new HydronomTransportStats
        {
            TransportId = TransportId,
            IsRunning = _isRunning,
            SentPackets = Interlocked.Read(ref _sentPackets),
            ReceivedPackets = Interlocked.Read(ref _receivedPackets),
            SentBytes = Interlocked.Read(ref _sentBytes),
            ReceivedBytes = Interlocked.Read(ref _receivedBytes),
            DroppedPackets = Interlocked.Read(ref _droppedPackets),
            PendingReceivePackets = _receiveChannel.Reader.Count,
            StartedAt = _startedAt,
            SnapshotAt = DateTimeOffset.UtcNow
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _receiveChannel.Writer.TryComplete();
        _cts?.Dispose();
        _writeLock.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_port is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested &&
               _isRunning &&
               _port.IsOpen)
        {
            try
            {
                var packet = SerialRfHydronomPacketFramer.TryReadPacket(
                    _port,
                    _options,
                    cancellationToken);

                if (packet is null)
                {
                    await Task.Delay(2, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var received = packet with
                {
                    TransportId = TransportId,
                    ChannelId = string.IsNullOrWhiteSpace(packet.ChannelId)
                        ? _options.ChannelId
                        : packet.ChannelId
                };

                Interlocked.Increment(ref _receivedPackets);
                Interlocked.Add(ref _receivedBytes, received.SizeBytes);

                if (!_receiveChannel.Writer.TryWrite(received))
                {
                    Interlocked.Increment(ref _droppedPackets);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                Interlocked.Increment(ref _droppedPackets);

                if (!_options.DropInvalidFrames)
                {
                    break;
                }

                await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            }
        }

        _isRunning = false;
    }
}