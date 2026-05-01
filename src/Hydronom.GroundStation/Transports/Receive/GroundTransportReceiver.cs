namespace Hydronom.GroundStation.Transports.Receive;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Transports;

/// <summary>
/// Ground Station tarafında kayıtlı transport'lardan gelen HydronomEnvelope mesajlarını dinler
/// ve GroundStationEngine benzeri bir handler'a aktarır.
/// 
/// Bu sınıfın görevi:
/// - ITransport.ReceiveAsync akışını başlatmak,
/// - Gelen envelope'ları merkezi handler'a vermek,
/// - Receive event geçmişi tutmak,
/// - LinkHealthTracker üzerinde "link görüldü" bilgisini güncellemek,
/// - Transport hata verirse kontrollü şekilde kayıt almaktır.
/// 
/// Bu sınıf GroundStationEngine'e doğrudan bağımlı değildir.
/// Handler delegate dışarıdan verilir:
///     Func&lt;HydronomEnvelope, bool&gt; envelopeHandler
/// </summary>
public sealed class GroundTransportReceiver
{
    private readonly GroundTransportRegistry _registry;
    private readonly LinkHealthTracker _linkHealthTracker;
    private readonly Func<HydronomEnvelope, bool> _envelopeHandler;
    private readonly GroundTransportReceiverOptions _options;

    private readonly List<GroundTransportReceiveEvent> _events = new();
    private readonly object _sync = new();

    public GroundTransportReceiver(
        GroundTransportRegistry registry,
        LinkHealthTracker linkHealthTracker,
        Func<HydronomEnvelope, bool> envelopeHandler,
        GroundTransportReceiverOptions? options = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _linkHealthTracker = linkHealthTracker ?? throw new ArgumentNullException(nameof(linkHealthTracker));
        _envelopeHandler = envelopeHandler ?? throw new ArgumentNullException(nameof(envelopeHandler));
        _options = options ?? new GroundTransportReceiverOptions();
    }

    /// <summary>
    /// Kayıtlı receive event sayısı.
    /// </summary>
    public int EventCount
    {
        get
        {
            lock (_sync)
                return _events.Count;
        }
    }

    /// <summary>
    /// Tüm bağlı transport'lar için receive loop başlatır.
    /// 
    /// Bu metot her transport için ayrı task üretir ve hepsini bekler.
    /// CancellationToken iptal edilene kadar akış devam eder.
    /// </summary>
    public async Task RunAllAsync(CancellationToken cancellationToken = default)
    {
        var transports = _registry.Transports
            .Where(x => x.IsConnected)
            .ToArray();

        if (transports.Length == 0)
            return;

        var tasks = transports
            .Select(transport => RunTransportAsync(transport, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Belirli bir transport için receive loop çalıştırır.
    /// </summary>
    public async Task RunTransportAsync(
        ITransport transport,
        CancellationToken cancellationToken = default)
    {
        if (transport is null)
            throw new ArgumentNullException(nameof(transport));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var envelope in transport.ReceiveAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    HandleReceivedEnvelope(transport, envelope);
                }

                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AddEvent(new GroundTransportReceiveEvent
                {
                    TransportName = transport.Name,
                    TransportKind = transport.Kind,
                    ErrorMessage = ex.Message,
                    Reason = $"Transport '{transport.Name}' receive loop failed."
                });

                if (!_options.ContinueOnTransportError)
                    break;

                await Task.Delay(_options.ErrorDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Receive event snapshot döndürür.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> GetSnapshot()
    {
        lock (_sync)
            return _events.OrderByDescending(x => x.ReceivedUtc).ToArray();
    }

    /// <summary>
    /// Gelen envelope'u işler.
    /// </summary>
    private void HandleReceivedEnvelope(
        ITransport transport,
        HydronomEnvelope envelope)
    {
        var handled = false;
        string? errorMessage = null;

        try
        {
            handled = _envelopeHandler(envelope);

            if (_options.MarkLinkSeenOnReceive &&
                envelope is not null &&
                !string.IsNullOrWhiteSpace(envelope.SourceNodeId))
            {
                _linkHealthTracker.MarkSeen(
                    envelope.SourceNodeId,
                    transport.Kind,
                    DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        AddEvent(new GroundTransportReceiveEvent
        {
            TransportName = transport.Name,
            TransportKind = transport.Kind,
            Envelope = envelope,
            Handled = handled,
            ErrorMessage = errorMessage,
            Reason = handled
                ? "Envelope received and handled by Ground Station."
                : "Envelope received but was not handled."
        });
    }

    /// <summary>
    /// Event geçmişine yeni kayıt ekler.
    /// </summary>
    private void AddEvent(GroundTransportReceiveEvent receiveEvent)
    {
        lock (_sync)
        {
            _events.Add(receiveEvent);

            while (_events.Count > _options.MaxEventHistory)
                _events.RemoveAt(0);
        }
    }
}