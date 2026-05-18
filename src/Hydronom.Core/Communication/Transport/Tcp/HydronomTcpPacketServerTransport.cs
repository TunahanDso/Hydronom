using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Hydronom.Core.Communication.Transport.Tcp;

public sealed class HydronomTcpPacketServerTransport : IHydronomPacketTransport
{
    private readonly Channel<HydronomTransportPacket> _receiveChannel;
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

    private int _clientIdCounter;
    private bool _isRunning;
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

    private long _sentPackets;
    private long _receivedPackets;
    private long _sentBytes;
    private long _receivedBytes;
    private long _droppedPackets;

    public HydronomTcpPacketServerTransport(
        string transportId,
        IPAddress listenAddress,
        int port)
    {
        TransportId = string.IsNullOrWhiteSpace(transportId)
            ? $"tcp-server-{port}"
            : transportId;

        ListenAddress = listenAddress ?? IPAddress.Loopback;
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

    public string TransportKind => "tcp-server";

    public IPAddress ListenAddress { get; }

    public int Port { get; }

    public bool IsRunning => _isRunning;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(ListenAddress, Port);
        _listener.Start();

        _isRunning = true;
        _startedAt = DateTimeOffset.UtcNow;

        _acceptTask = Task.Run(
            () => AcceptLoopAsync(_cts.Token),
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
            _listener?.Stop();
        }
        catch
        {
            // Stop best-effort.
        }

        foreach (var item in _clients)
        {
            try
            {
                item.Value.Close();
                item.Value.Dispose();
            }
            catch
            {
                // Client cleanup best-effort.
            }
        }

        _clients.Clear();

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.WaitAsync(
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

        if (!_isRunning || _clients.IsEmpty)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        var clients = _clients.ToArray();

        foreach (var (_, client) in clients)
        {
            if (!client.Connected)
            {
                Interlocked.Increment(ref _droppedPackets);
                continue;
            }

            try
            {
                var stream = client.GetStream();

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

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (_isRunning)
                {
                    Interlocked.Increment(ref _droppedPackets);
                }

                continue;
            }

            client.NoDelay = true;

            var clientId = Interlocked.Increment(ref _clientIdCounter);
            _clients[clientId] = client;

            _ = Task.Run(
                () => ClientReadLoopAsync(clientId, client, cancellationToken),
                CancellationToken.None);
        }
    }

    private async Task ClientReadLoopAsync(
        int clientId,
        TcpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = client.GetStream();

            while (!cancellationToken.IsCancellationRequested &&
                   _isRunning &&
                   client.Connected)
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
            _clients.TryRemove(clientId, out _);

            try
            {
                client.Close();
                client.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}