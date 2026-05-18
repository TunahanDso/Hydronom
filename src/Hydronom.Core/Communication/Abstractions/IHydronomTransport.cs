using Hydronom.Core.Communication.Envelope;

namespace Hydronom.Core.Communication.Abstractions;

public interface IHydronomTransport : IAsyncDisposable
{
    string TransportId { get; }

    string TransportKind { get; }

    bool IsConnected { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SendAsync(
        HydronomEncodedMessage message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<HydronomEncodedMessage> ReceiveAsync(
        CancellationToken cancellationToken = default);
}