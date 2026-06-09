using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Depth.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Runtime.Sensors.Sim;

namespace Hydronom.Runtime.Sensors.Backends.Sim;

/// <summary>
/// C# tabanlı simülasyon depth backend'i.
///
/// Bu backend özellikle CDSE degraded senaryolarını test etmek için önemlidir:
/// - GPS yokken
/// - IMU + Depth varken
/// - Hydronom düşük/orta güvenli ama kontrollü state üretebilmelidir.
///
/// Hydronom world convention:
/// - Z yukarı yöndür.
/// - Depth pozitif aşağı yöndür.
/// - depth = max(0, -Z)
/// </summary>
public sealed class SimDepthSensor : ISensorBackend
{
    private readonly SimDepthSensorOptions _options;
    private readonly SimSensorClock _clock;
    private readonly IPhysicsTruthProvider? _truthProvider;
    private readonly Random _random = new();

    private bool _isOpen;
    private long _sequence;

    private DateTime _lastSampleUtc;
    private DateTime _lastGoodSampleUtc;

    private int _errorCount;
    private int _consecutiveFailureCount;
    private string _lastError = "";

    public SimDepthSensor(
        SimDepthSensorOptions? options = null,
        SimSensorClock? clock = null,
        IPhysicsTruthProvider? truthProvider = null)
    {
        _options = (options ?? SimDepthSensorOptions.Default()).Sanitized();
        _clock = clock ?? new SimSensorClock();
        _truthProvider = truthProvider;

        Identity = SensorIdentity.Create(
            sensorId: "depth0",
            sourceId: string.IsNullOrWhiteSpace(_options.Source) ? "sim_depth" : _options.Source,
            dataKind: SensorDataKind.Depth,
            frameId: string.IsNullOrWhiteSpace(_options.FrameId) ? "depth_link" : _options.FrameId,
            displayName: "Sim Depth"
        );

        Source = SensorSourceInfo.Sim("sim_depth");

        Capabilities = SensorCapabilitySet.Empty
            .AddOrUpdate(SensorCapability.Create(
                name: "depth",
                confidence: 0.90,
                provider: "sim_depth",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "vertical_position",
                confidence: 0.80,
                provider: "sim_depth",
                frameId: "world",
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "pressure",
                confidence: 0.80,
                provider: "sim_depth",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ));
    }

    public SensorIdentity Identity { get; }

    public SensorSourceInfo Source { get; }

    public SensorCapabilitySet Capabilities { get; }

    public bool IsOpen => _isOpen;

    public ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clock.Start();
        _isOpen = true;
        _lastError = "";
        _consecutiveFailureCount = 0;

        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clock.Stop();
        _isOpen = false;

        return ValueTask.CompletedTask;
    }

    public ValueTask<SensorSample?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isOpen)
            throw new InvalidOperationException("Sim Depth açık değil.");

        var receiveUtc = DateTime.UtcNow;
        var captureUtc = _clock.NowUtc.UtcDateTime;

        try
        {
            var measurement = TryReadTruthMeasurement(out var truthMeasurement)
                ? truthMeasurement
                : CreateProceduralMeasurement();

            var data = new DepthSampleData(
                DepthMeters: measurement.DepthMeters,
                PressureKPa: measurement.PressureKPa,
                AltitudeMeters: null,
                TemperatureC: measurement.TemperatureC,
                Valid: true
            ).Sanitized();

            var timing = SensorTiming.FromCapture(
                captureUtc: captureUtc,
                receiveUtc: receiveUtc,
                publishUtc: DateTime.UtcNow,
                targetRateHz: _options.RateHz,
                effectiveRateHz: _options.RateHz
            );

            var quality = SensorQuality
                .Good(
                    backendKind: SensorBackendKind.Sim,
                    backendName: measurement.SourceName,
                    simulated: true,
                    confidence: 0.95
                )
                .WithTiming(
                    ageMs: timing.CaptureAgeMs,
                    latencyMs: timing.ReceiveToPublishMs,
                    targetRateHz: _options.RateHz,
                    effectiveRateHz: _options.RateHz
                );

            _sequence++;
            _lastSampleUtc = DateTime.UtcNow;
            _lastGoodSampleUtc = _lastSampleUtc;
            _lastError = "";
            _consecutiveFailureCount = 0;

            var sample = SensorSample.Create(
                sensor: Identity,
                source: Source,
                sequence: _sequence,
                dataKind: SensorDataKind.Depth,
                data: data,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-depth-{_sequence}"
            );

            return ValueTask.FromResult<SensorSample?>(sample);
        }
        catch (Exception ex)
        {
            _sequence++;
            _errorCount++;
            _consecutiveFailureCount++;
            _lastSampleUtc = DateTime.UtcNow;
            _lastError = ex.Message;

            var timing = SensorTiming.Now(_options.RateHz, 0.0);
            var quality = SensorQuality.Invalid($"Sim Depth read error: {ex.Message}");

            var sample = SensorSample.Create(
                sensor: Identity,
                source: Source,
                sequence: _sequence,
                dataKind: SensorDataKind.Depth,
                data: DepthSampleData.Empty,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-depth-invalid-{_sequence}"
            );

            return ValueTask.FromResult<SensorSample?>(sample);
        }
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
            DataKind: SensorDataKind.Depth,
            State: state,
            TimestampUtc: now,
            LastSampleUtc: _lastSampleUtc,
            LastGoodSampleUtc: _lastGoodSampleUtc,
            LastSampleAgeMs: ageMs,
            EffectiveRateHz: _options.RateHz,
            TargetRateHz: _options.RateHz,
            ErrorCount: _errorCount,
            ConsecutiveFailureCount: _consecutiveFailureCount,
            LastError: _lastError,
            BackendKind: SensorBackendKind.Sim,
            BackendName: "sim_depth",
            Simulated: true,
            Replay: false,
            Summary: BuildHealthSummary(state)
        ).Sanitized();
    }

    public ValueTask<SensorHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetHealthSnapshot());
    }

    private bool TryReadTruthMeasurement(out DepthMeasurement measurement)
    {
        measurement = default;

        if (_truthProvider is null || !_truthProvider.IsAvailable)
            return false;

        var truth = _truthProvider.GetLatestTruth().Sanitized();

        if (!truth.IsFinite)
            return false;

        var depthMeters = Math.Max(0.0, -truth.Position.Z) + NoiseMeters();
        depthMeters = Math.Max(0.0, depthMeters);

        measurement = BuildMeasurement(
            depthMeters: depthMeters,
            sourceName: "sim_depth_truth_fed"
        );

        return true;
    }

    private DepthMeasurement CreateProceduralMeasurement()
    {
        var t = Math.Max(0.0, _clock.Elapsed.TotalSeconds);

        var wave = Math.Sin(t * 0.35) * 0.05;
        var depthMeters = Math.Max(0.0, _options.ProceduralDepthMeters + wave + NoiseMeters());

        return BuildMeasurement(
            depthMeters: depthMeters,
            sourceName: "sim_depth_procedural"
        );
    }

    private DepthMeasurement BuildMeasurement(double depthMeters, string sourceName)
    {
        var pressureKPa = _options.SurfacePressureKPa +
                          depthMeters * _options.PressureKPaPerMeter;

        return new DepthMeasurement(
            DepthMeters: depthMeters,
            PressureKPa: pressureKPa,
            TemperatureC: _options.SimTemperatureC,
            SourceName: sourceName
        );
    }

    private SensorHealthState DetermineHealthState(double lastAgeMs)
    {
        if (!_isOpen)
            return SensorHealthState.Offline;

        if (_consecutiveFailureCount >= 8)
            return SensorHealthState.Failing;

        if (_consecutiveFailureCount >= 3)
            return SensorHealthState.Degraded;

        if (lastAgeMs > 1_500.0)
            return SensorHealthState.Stale;

        return SensorHealthState.Simulated;
    }

    private string BuildHealthSummary(SensorHealthState state)
    {
        return state switch
        {
            SensorHealthState.Simulated => "Sim Depth OK.",
            SensorHealthState.Offline => "Sim Depth offline.",
            SensorHealthState.Stale => "Sim Depth stale.",
            SensorHealthState.Degraded => $"Sim Depth degraded: {_consecutiveFailureCount} consecutive failures.",
            SensorHealthState.Failing => $"Sim Depth failing: {_consecutiveFailureCount} consecutive failures.",
            _ => $"Sim Depth state={state}."
        };
    }

    private double NoiseMeters()
    {
        return (_random.NextDouble() * 2.0 - 1.0) * _options.DepthNoiseMeters;
    }

    private readonly record struct DepthMeasurement(
        double DepthMeters,
        double PressureKPa,
        double TemperatureC,
        string SourceName
    );
}