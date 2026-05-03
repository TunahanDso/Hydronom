namespace Hydronom.GroundStation.Transports.Receive;

using Hydronom.Core.Communication;
using Hydronom.GroundStation.LinkHealth;
using Hydronom.GroundStation.Transports;

/// <summary>
/// Ground Station tarafÄ±nda kayÄ±tlÄ± transport'lardan gelen HydronomEnvelope mesajlarÄ±nÄ± dinler
/// ve GroundStationEngine benzeri bir handler'a aktarÄ±r.
/// 
/// Bu sÄ±nÄ±fÄ±n gÃ¶revi:
/// - ITransport.ReceiveAsync akÄ±ÅŸÄ±nÄ± baÅŸlatmak,
/// - Gelen envelope'larÄ± merkezi handler'a vermek,
/// - Receive event geÃ§miÅŸi tutmak,
/// - LinkHealthTracker Ã¼zerinde "link gÃ¶rÃ¼ldÃ¼" bilgisini gÃ¼ncellemek,
/// - Transport hata verirse kontrollÃ¼ ÅŸekilde kayÄ±t almaktÄ±r.
/// 
/// Bu sÄ±nÄ±f GroundStationEngine'e doÄŸrudan baÄŸÄ±mlÄ± deÄŸildir.
/// Handler delegate dÄ±ÅŸarÄ±dan verilir:
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
    /// KayÄ±tlÄ± receive event sayÄ±sÄ±.
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
    /// TÃ¼m baÄŸlÄ± transport'lar iÃ§in receive loop baÅŸlatÄ±r.
    /// 
    /// Bu metot her transport iÃ§in ayrÄ± task Ã¼retir ve hepsini bekler.
    /// CancellationToken iptal edilene kadar akÄ±ÅŸ devam eder.
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
    /// Belirli bir transport iÃ§in receive loop Ã§alÄ±ÅŸtÄ±rÄ±r.
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
    /// Receive event snapshot dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<GroundTransportReceiveEvent> GetSnapshot()
    {
        lock (_sync)
            return _events.OrderByDescending(x => x.ReceivedUtc).ToArray();
    }

    /// <summary>
    /// Gelen envelope'u iÅŸler.
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
    /// Event geÃ§miÅŸine yeni kayÄ±t ekler.
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
