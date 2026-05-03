using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Runtime.Sensors.Sim;

namespace Hydronom.Runtime.Sensors.Gps;

/// <summary>
/// C# tabanlı simülasyon GPS backend'i.
///
/// Bu sınıf artık eski anlamda genel sensör değil, ISensorBackend implementasyonudur.
/// Runtime bu backend'i açar, okur, health/capability bilgisini toplar.
///
/// Yeni davranış:
/// - Eğer IPhysicsTruthProvider verilmişse GPS ölçümü PhysicsTruthState üzerinden üretilir.
/// - Eğer truth provider yoksa eski procedural sim davranışı korunur.
///
/// Bu sayede mevcut testler kırılmaz, ama C# Primary mimaride doğru yöne geçiş başlar.
/// </summary>
public sealed class SimGpsSensor : ISensorBackend
{
    private const double DegToRad = Math.PI / 180.0;
    private const double LatMeters = 111_320.0;

    private readonly GpsSensorOptions _options;
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

    public SimGpsSensor(
        GpsSensorOptions? options = null,
        SimSensorClock? clock = null,
        IPhysicsTruthProvider? truthProvider = null)
    {
        _options = options ?? GpsSensorOptions.Default();
        _clock = clock ?? new SimSensorClock();
        _truthProvider = truthProvider;

        Identity = SensorIdentity.Create(
            sensorId: "gps0",
            sourceId: string.IsNullOrWhiteSpace(_options.Source) ? "sim_gps" : _options.Source,
            dataKind: SensorDataKind.Gps,
            frameId: string.IsNullOrWhiteSpace(_options.FrameId) ? "gps_link" : _options.FrameId,
            displayName: "Sim GPS"
        );

        Source = SensorSourceInfo.Sim("sim_gps");

        Capabilities = SensorCapabilitySet.Empty
            .AddOrUpdate(SensorCapability.Create(
                name: "global_position",
                confidence: 0.90,
                provider: "sim_gps",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "local_position",
                confidence: 0.90,
                provider: "sim_gps",
                frameId: "world",
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "ground_speed",
                confidence: 0.80,
                provider: "sim_gps",
                frameId: "world",
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
            throw new InvalidOperationException("Sim GPS açık değil.");

        var receiveUtc = DateTime.UtcNow;
        var captureUtc = _clock.NowUtc.UtcDateTime;

        try
        {
            var measurement = TryReadTruthMeasurement(out var truthMeasurement)
                ? truthMeasurement
                : CreateProceduralMeasurement();

            var cosLat = Math.Cos(_options.OriginLat * DegToRad);
            var lat = _options.OriginLat + measurement.Y / LatMeters;
            var lon = _options.OriginLon + measurement.X / Math.Max(1e-9, LatMeters * cosLat);

            var data = new GpsSampleData(
                Latitude: lat,
                Longitude: lon,
                AltitudeMeters: measurement.Z,
                X: measurement.X,
                Y: measurement.Y,
                Z: measurement.Z,
                SpeedMps: measurement.SpeedMps,
                CourseDeg: measurement.CourseDeg,
                Hdop: _options.SimHdop,
                FixType: 3,
                Satellites: 14
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
                    confidence: ComputeConfidenceFromHdop(_options.SimHdop)
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
                dataKind: SensorDataKind.Gps,
                data: data,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-gps-{_sequence}"
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
            var quality = SensorQuality.Invalid($"Sim GPS read error: {ex.Message}");

            var sample = SensorSample.Create(
                sensor: Identity,
                source: Source,
                sequence: _sequence,
                dataKind: SensorDataKind.Gps,
                data: GpsSampleData.Empty,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-gps-invalid-{_sequence}"
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
            DataKind: SensorDataKind.Gps,
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
            BackendName: "sim_gps",
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

    private bool TryReadTruthMeasurement(out GpsMeasurement measurement)
    {
        measurement = default;

        if (_truthProvider is null || !_truthProvider.IsAvailable)
            return false;

        var truth = _truthProvider.GetLatestTruth().Sanitized();

        if (!truth.IsFinite)
            return false;

        /*
         * GPS local position:
         * PhysicsTruthState.Position map/world frame içindeki sim gerçek konumudur.
         * Buraya GPS noise ekliyoruz çünkü gerçek GPS kusursuz truth vermez.
         */
        var x = truth.Position.X + NoiseMeters();
        var y = truth.Position.Y + NoiseMeters();
        var z = truth.Position.Z;

        var vx = truth.Velocity.X;
        var vy = truth.Velocity.Y;
        var vz = truth.Velocity.Z;

        var speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);

        var courseDeg = speed > 1e-6
            ? NormalizeDeg(Math.Atan2(vy, vx) * 180.0 / Math.PI)
            : 0.0;

        measurement = new GpsMeasurement(
            X: x,
            Y: y,
            Z: z,
            SpeedMps: speed,
            CourseDeg: courseDeg,
            SourceName: "sim_gps_truth_fed"
        );

        return true;
    }

    private GpsMeasurement CreateProceduralMeasurement()
    {
        var t = Math.Max(0.0, _clock.Elapsed.TotalSeconds);

        var x = _options.SimVxMetersPerSec * t + NoiseMeters();
        var y = _options.SimVyMetersPerSec * t + NoiseMeters();
        var z = 0.0;

        var speed = Math.Sqrt(
            _options.SimVxMetersPerSec * _options.SimVxMetersPerSec +
            _options.SimVyMetersPerSec * _options.SimVyMetersPerSec
        );

        var courseDeg = NormalizeDeg(
            Math.Atan2(_options.SimVyMetersPerSec, _options.SimVxMetersPerSec) * 180.0 / Math.PI
        );

        return new GpsMeasurement(
            X: x,
            Y: y,
            Z: z,
            SpeedMps: speed,
            CourseDeg: courseDeg,
            SourceName: "sim_gps_procedural"
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

        if (lastAgeMs > 2_000.0)
            return SensorHealthState.Stale;

        return SensorHealthState.Simulated;
    }

    private string BuildHealthSummary(SensorHealthState state)
    {
        return state switch
        {
            SensorHealthState.Simulated => "Sim GPS OK.",
            SensorHealthState.Offline => "Sim GPS offline.",
            SensorHealthState.Stale => "Sim GPS stale.",
            SensorHealthState.Degraded => $"Sim GPS degraded: {_consecutiveFailureCount} consecutive failures.",
            SensorHealthState.Failing => $"Sim GPS failing: {_consecutiveFailureCount} consecutive failures.",
            _ => $"Sim GPS state={state}."
        };
    }

    private double NoiseMeters()
    {
        return (_random.NextDouble() * 2.0 - 1.0) * _options.PositionNoiseMeters;
    }

    private static double ComputeConfidenceFromHdop(double hdop)
    {
        if (hdop <= 0.0)
            return 0.0;

        if (hdop <= 0.8)
            return 1.0;

        if (hdop <= 1.5)
            return 0.85;

        if (hdop <= 2.5)
            return 0.65;

        if (hdop <= 5.0)
            return 0.35;

        return 0.15;
    }

    private static double NormalizeDeg(double deg)
    {
        var result = deg % 360.0;
        return result < 0.0 ? result + 360.0 : result;
    }

    private readonly record struct GpsMeasurement(
        double X,
        double Y,
        double Z,
        double SpeedMps,
        double CourseDeg,
        string SourceName
    );
}