using System.Net.Sockets;
using System.Threading.Channels;

namespace Hydronom.Core.Communication.Transport.Tcp;

public sealed class HydronomTcpPacketClientTransport : IHydronomPacketTransport
{
    private readonly Channel<HydronomTransportPacket> _receiveChannel;

    private TcpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    private bool _isRunning;
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

    private long _sentPackets;
    private long _receivedPackets;
    private long _sentBytes;
    private long _receivedBytes;
    private long _droppedPackets;

    public HydronomTcpPacketClientTransport(
        string transportId,
        string host,
        int port)
    {
        TransportId = string.IsNullOrWhiteSpace(transportId)
            ? $"tcp-client-{host}-{port}"
            : transportId;

        Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        Port = port;

        _receiveChannel = Channel.CreateUnbounded<HydronomTransportPacket>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public string TransportId { get; }

    public string TransportKind => "tcp-client";

    public string Host { get; }

    public int Port { get; }

    public bool IsRunning => _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _client = new TcpClient
        {
            NoDelay = true
        };

        await _client.ConnectAsync(
                Host,
                Port,
                _cts.Token)
            .ConfigureAwait(false);

        _isRunning = true;
        _startedAt = DateTimeOffset.UtcNow;

        _readTask = Task.Run(
            () => ReadLoopAsync(_cts.Token),
            CancellationToken.None);
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
            _client?.Close();
            _client?.Dispose();
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

        if (!_isRunning || _client is null || !_client.Connected)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        try
        {
            var stream = _client.GetStream();

            var forwarded = packet with
            {
                TransportId = TransportId,
                CreatedAt = DateTimeOffset.UtcNow,
                Bytes = packet.Bytes.ToArray()
            };

            await HydronomTcpPacketFramer.WritePacketAsync(
                    stream,
                    forwarded,
                    cancellationToken)
                .ConfigureAwait(false);

            Interlocked.Increment(ref _sentPackets);
            Interlocked.Add(ref _sentBytes, forwarded.SizeBytes);
        }
        catch
        {
            Interlocked.Increment(ref _droppedPackets);
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
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            var stream = _client.GetStream();

            while (!cancellationToken.IsCancellationRequested &&
                   _isRunning &&
                   _client.Connected)
            {
                HydronomTransportPacket? packet;

                try
                {
                    packet = await HydronomTcpPacketFramer.ReadPacketAsync(
                            stream,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Increment(ref _droppedPackets);
                    break;
                }

                if (packet is null)
                {
                    break;
                }

                Interlocked.Increment(ref _receivedPackets);
                Interlocked.Add(ref _receivedBytes, packet.SizeBytes);

                await _receiveChannel.Writer.WriteAsync(
                        packet,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }
}