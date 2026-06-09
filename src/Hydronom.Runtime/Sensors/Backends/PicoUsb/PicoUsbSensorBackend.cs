using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;

namespace Hydronom.Runtime.Sensors.Backends.PicoUsb;

/// <summary>
/// Pico USB tabanlı sensör hub backend'i.
/// 
/// Bu backend Pico firmware değildir.
/// Pico/MCU tarafından USB üzerinden gönderilen sensör frame'lerini Hydronom SensorSample akışına bağlar.
/// 
/// İlk paket:
/// - ISensorBackend sözleşmesine uyar.
/// - Health/capability üretir.
/// - Runtime'a güvenli şekilde eklenebilir.
/// - Gerçek serial read henüz pasif bırakılmıştır.
/// </summary>
public sealed class PicoUsbSensorBackend : ISensorBackend
{
    private readonly PicoUsbSensorBackendOptions _options;
    private readonly PicoUsbFrameDecoder _decoder;
    private readonly PicoSensorSampleMapper _mapper;

    private bool _isOpen;
    private long _sequence;

    private DateTime _lastSampleUtc;
    private DateTime _lastGoodSampleUtc;

    private int _errorCount;
    private int _consecutiveFailureCount;
    private string _lastError = "";

    public PicoUsbSensorBackend(
        PicoUsbSensorBackendOptions? options = null,
        PicoUsbFrameDecoder? decoder = null,
        PicoSensorSampleMapper? mapper = null)
    {
        _options = (options ?? PicoUsbSensorBackendOptions.Default()).Sanitized();
        _decoder = decoder ?? new PicoUsbFrameDecoder();
        _mapper = mapper ?? new PicoSensorSampleMapper(_options);

        Identity = SensorIdentity.Create(
            sensorId: _options.DeviceId,
            sourceId: _options.BackendName,
            dataKind: SensorDataKind.Custom,
            frameId: "sensor_hub",
            displayName: $"Pico USB Sensor Hub ({_options.DeviceId})"
        );

        Source = SensorSourceInfo.PicoUsb(
            backendName: _options.BackendName,
            endpoint: _options.Connection.ToString()
        );

        Capabilities = SensorCapabilitySet.Empty
            .AddOrUpdate(SensorCapability.Create(
                name: "pico_usb_sensor_hub",
                confidence: 0.70,
                provider: _options.BackendName,
                frameId: "sensor_hub",
                targetRateHz: _options.TargetRateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "raw_sensor_frame_decode",
                confidence: 0.60,
                provider: _options.BackendName,
                frameId: "sensor_hub",
                targetRateHz: _options.TargetRateHz
            ));
    }

    public SensorIdentity Identity { get; }

    public SensorSourceInfo Source { get; }

    public SensorCapabilitySet Capabilities { get; }

    public bool IsOpen => _isOpen;

    public ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        /*
         * İlk paket davranışı:
         * Gerçek serial port açmıyoruz.
         * Backend runtime'a bağlanabiliyor, health üretebiliyor, ama sample üretmiyor.
         *
         * EnablePhysicalSerialRead=true yapıldığında ileride burada platform serial adapter açılacak.
         */
        if (_options.EnablePhysicalSerialRead)
        {
            _lastError = "Physical Pico USB serial read is not implemented yet.";
            _errorCount++;
            _consecutiveFailureCount++;

            if (!_options.AllowPassiveOpenWithoutPort)
            {
                throw new InvalidOperationException(_lastError);
            }
        }

        _isOpen = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _isOpen = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<SensorSample?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isOpen)
        {
            throw new InvalidOperationException("Pico USB sensor backend açık değil.");
        }

        /*
         * İlk pakette gerçek USB stream bağlı değil.
         * Bu yüzden sample üretmiyoruz.
         *
         * Bir sonraki pakette:
         * - PicoUsbSerialReader / adapter
         * - byte buffer
         * - decoder.Decode(...)
         * - mapper.TryMap(...)
         * hattı burada çalışacak.
         */
        if (!_options.EnablePhysicalSerialRead)
        {
            return ValueTask.FromResult<SensorSample?>(null);
        }

        _sequence++;
        _lastSampleUtc = DateTime.UtcNow;

        var frame = _decoder.Decode(ReadOnlySpan<byte>.Empty, _lastSampleUtc);
        var sample = _mapper.TryMap(frame);

        if (sample.HasValue && sample.Value.IsValid)
        {
            _lastGoodSampleUtc = DateTime.UtcNow;
            _lastError = "";
            _consecutiveFailureCount = 0;
            return ValueTask.FromResult<SensorSample?>(sample.Value);
        }

        if (!_options.DropInvalidFrames)
        {
            _errorCount++;
            _consecutiveFailureCount++;
            _lastError = string.IsNullOrWhiteSpace(frame.Error)
                ? "Pico USB frame could not be mapped to SensorSample."
                : frame.Error;
        }

        return ValueTask.FromResult<SensorSample?>(null);
    }

    public SensorHealthSnapshot GetHealthSnapshot()
    {
        var now = DateTime.UtcNow;

        var ageMs = _lastSampleUtc == default
            ? double.PositiveInfinity
            : Math.Max(0.0, (now - _lastSampleUtc).TotalMilliseconds);

        var state = DetermineHealthState(ageMs);

        return new SensorHealthSnapshot(
            SensorId: Identity.SensorId,
            SourceId: Identity.SourceId,
            DataKind: SensorDataKind.Custom,
            State: state,
            TimestampUtc: now,
            LastSampleUtc: _lastSampleUtc,
            LastGoodSampleUtc: _lastGoodSampleUtc,
            LastSampleAgeMs: ageMs,
            EffectiveRateHz: 0.0,
            TargetRateHz: _options.TargetRateHz,
            ErrorCount: _errorCount,
            ConsecutiveFailureCount: _consecutiveFailureCount,
            LastError: _lastError,
            BackendKind: SensorBackendKind.PicoUsb,
            BackendName: _options.BackendName,
            Simulated: false,
            Replay: false,
            Summary: BuildHealthSummary(state)
        ).Sanitized();
    }

    public ValueTask<SensorHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetHealthSnapshot());
    }

    private SensorHealthState DetermineHealthState(double lastAgeMs)
    {
        if (!_isOpen)
        {
            return SensorHealthState.Offline;
        }

        if (_consecutiveFailureCount >= 8)
        {
            return SensorHealthState.Failing;
        }

        if (_consecutiveFailureCount >= 3)
        {
            return SensorHealthState.Degraded;
        }

        if (lastAgeMs > _options.StaleAfterMs)
        {
            return SensorHealthState.Stale;
        }

        /*
         * Bu ilk paket pasif backend olduğu için açık ama data yoksa Degraded daha dürüst.
         * Gerçek serial reader eklendiğinde sağlıklı data akışında bu state iyileştirilecek.
         */
        return SensorHealthState.Degraded;
    }

    private string BuildHealthSummary(SensorHealthState state)
    {
        return state switch
        {
            SensorHealthState.Offline => "Pico USB sensor backend offline.",
            SensorHealthState.Stale => "Pico USB sensor backend open but no fresh sample has been received yet.",
            SensorHealthState.Degraded => string.IsNullOrWhiteSpace(_lastError)
                ? "Pico USB sensor backend open in passive mode."
                : $"Pico USB sensor backend degraded: {_lastError}",
            SensorHealthState.Failing => $"Pico USB sensor backend failing: {_lastError}",
            _ => $"Pico USB sensor backend state={state}."
        };
    }
}