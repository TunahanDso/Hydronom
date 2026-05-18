namespace Hydronom.Core.Communication.Transport.InMemory;

public sealed record InMemoryHydronomTransportPair : IAsyncDisposable
{
    public InMemoryHydronomTransport A { get; init; } = new("inmem-a");

    public InMemoryHydronomTransport B { get; init; } = new("inmem-b");

    public static InMemoryHydronomTransportPair Create(
        string aId = "runtime-transport",
        string bId = "ground-transport")
    {
        var a = new InMemoryHydronomTransport(aId);
        var b = new InMemoryHydronomTransport(bId);

        a.ConnectPeer(b);
        b.ConnectPeer(a);

        return new InMemoryHydronomTransportPair
        {
            A = a,
            B = b
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await A.StartAsync(cancellationToken).ConfigureAwait(false);
        await B.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await A.StopAsync(cancellationToken).ConfigureAwait(false);
        await B.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await A.DisposeAsync().ConfigureAwait(false);
        await B.DisposeAsync().ConfigureAwait(false);
    }
}