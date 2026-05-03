namespace Hydronom.GroundStation.Transports;

using System.Runtime.CompilerServices;
using Hydronom.Core.Communication;

/// <summary>
/// Ground Station testleri iÃ§in kullanÄ±lan mock transport implementasyonudur.
/// 
/// GerÃ§ek TCP/WebSocket/LoRa baÄŸlantÄ±sÄ± kurmaz.
/// SendAsync Ã§aÄŸrÄ±sÄ±nda isteÄŸe gÃ¶re:
/// - baÅŸarÄ±lÄ± gÃ¶nderim,
/// - gecikmeli gÃ¶nderim,
/// - exception ile hata
/// simÃ¼le edebilir.
/// </summary>
public sealed class MockGroundTransport : ITransport
{
    private readonly Queue<HydronomEnvelope> _received = new();

    public MockGroundTransport(
        string name,
        TransportKind kind,
        bool isConnected = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Transport adÄ± boÅŸ olamaz.", nameof(name));

        Name = name;
        Kind = kind;
        IsConnected = isConnected;
    }

    /// <summary>
    /// Transport instance adÄ±.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Transport tÃ¼rÃ¼.
    /// </summary>
    public TransportKind Kind { get; }

    /// <summary>
    /// BaÄŸlantÄ± durumu.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// SendAsync Ã§aÄŸrÄ±sÄ±nda simÃ¼le edilecek gecikme.
    /// </summary>
    public TimeSpan SimulatedSendDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// true ise SendAsync exception fÄ±rlatÄ±r.
    /// </summary>
    public bool FailOnSend { get; set; }

    /// <summary>
    /// GÃ¶nderilen envelope sayÄ±sÄ±.
    /// </summary>
    public int SentCount { get; private set; }

    /// <summary>
    /// Son gÃ¶nderilen envelope.
    /// </summary>
    public HydronomEnvelope? LastSentEnvelope { get; private set; }

    /// <summary>
    /// Mock baÄŸlantÄ±yÄ± aÃ§Ä±k kabul eder.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mock baÄŸlantÄ±yÄ± kapalÄ± kabul eder.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Envelope gÃ¶nderimini simÃ¼le eder.
    /// </summary>
    public async Task SendAsync(
        HydronomEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException($"Mock transport '{Name}' baÄŸlÄ± deÄŸil.");

        if (FailOnSend)
            throw new InvalidOperationException($"Mock transport '{Name}' gÃ¶nderim hatasÄ± simÃ¼le etti.");

        if (SimulatedSendDelay > TimeSpan.Zero)
            await Task.Delay(SimulatedSendDelay, cancellationToken);

        LastSentEnvelope = envelope;
        SentCount++;
    }

    /// <summary>
    /// Test amaÃ§lÄ± receive kuyruÄŸuna envelope ekler.
    /// </summary>
    public void EnqueueReceived(HydronomEnvelope envelope)
    {
        if (envelope is not null)
            _received.Enqueue(envelope);
    }

    /// <summary>
    /// Mock receive akÄ±ÅŸÄ±.
    /// </summary>
    public async IAsyncEnumerable<HydronomEnvelope> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (_received.Count > 0)
                yield return _received.Dequeue();

            await Task.Delay(25, cancellationToken);
        }
    }
}
