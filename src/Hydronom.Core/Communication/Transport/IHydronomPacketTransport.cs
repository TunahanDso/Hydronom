namespace Hydronom.Core.Communication.Transport;

public interface IHydronomPacketTransport : IAsyncDisposable
{
    string TransportId { get; }

    string TransportKind { get; }

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    ValueTask SendAsync(
        HydronomTransportPacket packet,
        CancellationToken cancellationToken = default);

    ValueTask<HydronomTransportPacket?> TryReceiveAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<HydronomTransportPacket> ReceiveAllAsync(
        CancellationToken cancellationToken = default);

    HydronomTransportStats SnapshotStats();
}