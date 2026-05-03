using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Runtime.Sensors.Sim;

namespace Hydronom.Runtime.Sensors.Imu;

/// <summary>
/// C# tabanlı simülasyon IMU backend'i.
///
/// Bu sınıf artık eski anlamda genel sensör değil, ISensorBackend implementasyonudur.
/// Runtime bu backend'i açar, okur, health/capability bilgisini toplar.
///
/// Yeni davranış:
/// - Eğer IPhysicsTruthProvider verilmişse IMU ölçümü PhysicsTruthState üzerinden üretilir.
/// - Eğer truth provider yoksa eski procedural sim davranışı korunur.
///
/// Bu geçiş sayesinde mevcut smoke test kırılmaz, ama C# Primary mimaride doğru truth-fed sim sensör yapısına geçilir.
/// </summary>
public sealed class SimImuSensor : ISensorBackend
{
    private readonly ImuSensorOptions _options;
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

    public SimImuSensor(
        ImuSensorOptions? options = null,
        SimSensorClock? clock = null,
        IPhysicsTruthProvider? truthProvider = null)
    {
        _options = options ?? ImuSensorOptions.Default();
        _clock = clock ?? new SimSensorClock();
        _truthProvider = truthProvider;

        Identity = SensorIdentity.Create(
            sensorId: "imu0",
            sourceId: string.IsNullOrWhiteSpace(_options.Source) ? "sim_imu" : _options.Source,
            dataKind: SensorDataKind.Imu,
            frameId: string.IsNullOrWhiteSpace(_options.FrameId) ? "imu_link" : _options.FrameId,
            displayName: "Sim IMU"
        );

        Source = SensorSourceInfo.Sim("sim_imu");

        Capabilities = SensorCapabilitySet.Empty
            .AddOrUpdate(SensorCapability.Create(
                name: "linear_acceleration",
                confidence: 0.90,
                provider: "sim_imu",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "angular_velocity",
                confidence: 0.90,
                provider: "sim_imu",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "attitude_estimation",
                confidence: 0.85,
                provider: "sim_imu",
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
            throw new InvalidOperationException("Sim IMU açık değil.");

        var receiveUtc = DateTime.UtcNow;
        var captureUtc = _clock.NowUtc.UtcDateTime;

        try
        {
            var measurement = TryReadTruthMeasurement(out var truthMeasurement)
                ? truthMeasurement
                : CreateProceduralMeasurement();

            var data = new ImuSampleData(
                Ax: measurement.Ax,
                Ay: measurement.Ay,
                Az: measurement.Az,
                GxRadSec: measurement.GxRadSec,
                GyRadSec: measurement.GyRadSec,
                GzRadSec: measurement.GzRadSec,
                Mx: null,
                My: null,
                Mz: null,
                RollDeg: measurement.RollDeg,
                PitchDeg: measurement.PitchDeg,
                YawDeg: measurement.YawDeg,
                TemperatureC: _options.SimTemperatureC + Noise() * 2.0
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
                    confidence: 1.0
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
                dataKind: SensorDataKind.Imu,
                data: data,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-imu-{_sequence}"
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
            var quality = SensorQuality.Invalid($"Sim IMU read error: {ex.Message}");

            var sample = SensorSample.Create(
                sensor: Identity,
                source: Source,
                sequence: _sequence,
                dataKind: SensorDataKind.Imu,
                data: ImuSampleData.Zero,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-imu-invalid-{_sequence}"
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
            DataKind: SensorDataKind.Imu,
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
            BackendName: "sim_imu",
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

    private bool TryReadTruthMeasurement(out ImuMeasurement measurement)
    {
        measurement = default;

        if (_truthProvider is null || !_truthProvider.IsAvailable)
            return false;

        var truth = _truthProvider.GetLatestTruth().Sanitized();

        if (!truth.IsFinite)
            return false;

        /*
         * IMU truth-fed ölçüm:
         * - Acceleration: fizik truth ivmesi + küçük noise
         * - Angular velocity: truth açısal hızları deg/s -> rad/s
         * - Roll/Pitch/Yaw: truth orientation değerleri
         *
         * Not:
         * Az kanalına 9.80665 ekliyoruz. Bu sim modelde IMU'nun yerçekimi etkisini
         * ölçtüğü basit varsayımıdır. Daha ileri pakette frame dönüşümü ve gravity compensation
         * ayrı fiziksel model olarak ele alınabilir.
         */
        measurement = new ImuMeasurement(
            Ax: truth.Acceleration.X + Noise(),
            Ay: truth.Acceleration.Y + Noise(),
            Az: 9.80665 + truth.Acceleration.Z + Noise(),
            GxRadSec: DegToRad(truth.AngularVelocityDegSec.X),
            GyRadSec: DegToRad(truth.AngularVelocityDegSec.Y),
            GzRadSec: DegToRad(truth.AngularVelocityDegSec.Z),
            RollDeg: truth.Orientation.RollDeg,
            PitchDeg: truth.Orientation.PitchDeg,
            YawDeg: NormalizeDeg(truth.Orientation.YawDeg),
            SourceName: "sim_imu_truth_fed"
        );

        return true;
    }

    private ImuMeasurement CreateProceduralMeasurement()
    {
        var t = Math.Max(0.0, _clock.Elapsed.TotalSeconds);

        var yawDeg = NormalizeDeg(_options.SimYawRateDegPerSec * t);
        var yawRateRad = DegToRad(_options.SimYawRateDegPerSec);

        var rollDeg = _options.SimRollAmplitudeDeg * Math.Sin(t * 0.7);
        var pitchDeg = _options.SimPitchAmplitudeDeg * Math.Sin(t * 0.45);

        return new ImuMeasurement(
            Ax: Noise(),
            Ay: Noise(),
            Az: 9.80665 + Noise(),
            GxRadSec: Noise() * 0.1,
            GyRadSec: Noise() * 0.1,
            GzRadSec: yawRateRad,
            RollDeg: rollDeg,
            PitchDeg: pitchDeg,
            YawDeg: yawDeg,
            SourceName: "sim_imu_procedural"
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
            SensorHealthState.Simulated => "Sim IMU OK.",
            SensorHealthState.Offline => "Sim IMU offline.",
            SensorHealthState.Stale => "Sim IMU stale.",
            SensorHealthState.Degraded => $"Sim IMU degraded: {_consecutiveFailureCount} consecutive failures.",
            SensorHealthState.Failing => $"Sim IMU failing: {_consecutiveFailureCount} consecutive failures.",
            _ => $"Sim IMU state={state}."
        };
    }

    private double Noise()
    {
        return (_random.NextDouble() * 2.0 - 1.0) * _options.NoiseScale;
    }

    private static double DegToRad(double deg)
    {
        return deg * Math.PI / 180.0;
    }

    private static double NormalizeDeg(double deg)
    {
        var result = deg % 360.0;
        return result < 0.0 ? result + 360.0 : result;
    }

    private readonly record struct ImuMeasurement(
        double Ax,
        double Ay,
        double Az,
        double GxRadSec,
        double GyRadSec,
        double GzRadSec,
        double RollDeg,
        double PitchDeg,
        double YawDeg,
        string SourceName
    );
}