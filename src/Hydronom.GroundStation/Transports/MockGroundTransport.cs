namespace Hydronom.GroundStation.Transports;

using System.Runtime.CompilerServices;
using Hydronom.Core.Communication;

/// <summary>
/// Ground Station testleri için kullanılan mock transport implementasyonudur.
/// 
/// Gerçek TCP/WebSocket/LoRa bağlantısı kurmaz.
/// SendAsync çağrısında isteğe göre:
/// - başarılı gönderim,
/// - gecikmeli gönderim,
/// - exception ile hata
/// simüle edebilir.
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
            throw new ArgumentException("Transport adı boş olamaz.", nameof(name));

        Name = name;
        Kind = kind;
        IsConnected = isConnected;
    }

    /// <summary>
    /// Transport instance adı.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Transport türü.
    /// </summary>
    public TransportKind Kind { get; }

    /// <summary>
    /// Bağlantı durumu.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// SendAsync çağrısında simüle edilecek gecikme.
    /// </summary>
    public TimeSpan SimulatedSendDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// true ise SendAsync exception fırlatır.
    /// </summary>
    public bool FailOnSend { get; set; }

    /// <summary>
    /// Gönderilen envelope sayısı.
    /// </summary>
    public int SentCount { get; private set; }

    /// <summary>
    /// Son gönderilen envelope.
    /// </summary>
    public HydronomEnvelope? LastSentEnvelope { get; private set; }

    /// <summary>
    /// Mock bağlantıyı açık kabul eder.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mock bağlantıyı kapalı kabul eder.
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Envelope gönderimini simüle eder.
    /// </summary>
    public async Task SendAsync(
        HydronomEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException($"Mock transport '{Name}' bağlı değil.");

        if (FailOnSend)
            throw new InvalidOperationException($"Mock transport '{Name}' gönderim hatası simüle etti.");

        if (SimulatedSendDelay > TimeSpan.Zero)
            await Task.Delay(SimulatedSendDelay, cancellationToken);

        LastSentEnvelope = envelope;
        SentCount++;
    }

    /// <summary>
    /// Test amaçlı receive kuyruğuna envelope ekler.
    /// </summary>
    public void EnqueueReceived(HydronomEnvelope envelope)
    {
        if (envelope is not null)
            _received.Enqueue(envelope);
    }

    /// <summary>
    /// Mock receive akışı.
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