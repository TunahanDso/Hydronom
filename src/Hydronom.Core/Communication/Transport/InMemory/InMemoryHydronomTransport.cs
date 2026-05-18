using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Hydronom.Core.Communication.Transport.InMemory;

public sealed class InMemoryHydronomTransport : IHydronomPacketTransport
{
    private readonly Channel<HydronomTransportPacket> _receiveChannel;
    private readonly ConcurrentQueue<HydronomTransportPacket> _receiveQueue = new();

    private InMemoryHydronomTransport? _peer;
    private bool _isRunning;
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

    private long _sentPackets;
    private long _receivedPackets;
    private long _sentBytes;
    private long _receivedBytes;
    private long _droppedPackets;

    public InMemoryHydronomTransport(string transportId)
    {
        TransportId = string.IsNullOrWhiteSpace(transportId)
            ? $"inmem-{Guid.NewGuid():N}"
            : transportId;

        _receiveChannel = Channel.CreateUnbounded<HydronomTransportPacket>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public string TransportId { get; }

    public string TransportKind => "in-memory";

    public bool IsRunning => _isRunning;

    internal void ConnectPeer(InMemoryHydronomTransport peer)
    {
        _peer = peer ?? throw new ArgumentNullException(nameof(peer));
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _isRunning = true;
        _startedAt = DateTimeOffset.UtcNow;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _isRunning = false;

        return Task.CompletedTask;
    }

    public async ValueTask SendAsync(
        HydronomTransportPacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isRunning || _peer is null || !_peer.IsRunning)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        var forwarded = packet with
        {
            TransportId = TransportId,
            CreatedAt = DateTimeOffset.UtcNow,
            Bytes = packet.Bytes.ToArray()
        };

        Interlocked.Increment(ref _sentPackets);
        Interlocked.Add(ref _sentBytes, forwarded.SizeBytes);

        await _peer.EnqueueIncomingAsync(forwarded, cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask<HydronomTransportPacket?> TryReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_receiveQueue.TryDequeue(out var queuedPacket))
        {
            return ValueTask.FromResult<HydronomTransportPacket?>(queuedPacket);
        }

        if (_receiveChannel.Reader.TryRead(out var channelPacket))
        {
            return ValueTask.FromResult<HydronomTransportPacket?>(channelPacket);
        }

        return ValueTask.FromResult<HydronomTransportPacket?>(null);
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
            PendingReceivePackets = _receiveQueue.Count,
            StartedAt = _startedAt,
            SnapshotAt = DateTimeOffset.UtcNow
        };
    }

    public ValueTask DisposeAsync()
    {
        _isRunning = false;
        _receiveChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private async ValueTask EnqueueIncomingAsync(
        HydronomTransportPacket packet,
        CancellationToken cancellationToken)
    {
        if (!_isRunning)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        _receiveQueue.Enqueue(packet);

        Interlocked.Increment(ref _receivedPackets);
        Interlocked.Add(ref _receivedBytes, packet.SizeBytes);

        await _receiveChannel.Writer.WriteAsync(packet, cancellationToken)
            .ConfigureAwait(false);
    }
}