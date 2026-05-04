using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Lidar.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Runtime.Sensors.Backends.Lidar;
using Hydronom.Runtime.Sensors.Sim;
using Hydronom.Runtime.Simulation.Raycasting;
using Hydronom.Runtime.World.Runtime;

namespace Hydronom.Runtime.Sensors.Backends.Sim;

/// <summary>
/// C# tabanlı simülasyon LiDAR backend'i.
/// PhysicsTruthProvider + RuntimeWorldModel üzerinden LaserScan üretir.
/// </summary>
public sealed class SimLidarBackend : ISensorBackend
{
    private readonly LidarBackendOptions _options;
    private readonly SimSensorClock _clock;
    private readonly IPhysicsTruthProvider? _truthProvider;
    private readonly RuntimeWorldModel? _worldModel;
    private readonly Random _random = new();

    private bool _isOpen;
    private long _sequence;

    private DateTime _lastSampleUtc;
    private DateTime _lastGoodSampleUtc;

    private int _errorCount;
    private int _consecutiveFailureCount;
    private string _lastError = "";

    public SimLidarBackend(
        LidarBackendOptions? options = null,
        SimSensorClock? clock = null,
        IPhysicsTruthProvider? truthProvider = null,
        RuntimeWorldModel? worldModel = null)
    {
        _options = (options ?? LidarBackendOptions.Default()).Sanitized();
        _clock = clock ?? new SimSensorClock();
        _truthProvider = truthProvider;
        _worldModel = worldModel;

        Identity = SensorIdentity.Create(
            sensorId: _options.SensorId,
            sourceId: string.IsNullOrWhiteSpace(_options.Source) ? "sim_lidar" : _options.Source,
            dataKind: SensorDataKind.Lidar,
            frameId: string.IsNullOrWhiteSpace(_options.FrameId) ? "lidar_link" : _options.FrameId,
            displayName: "Sim LiDAR"
        );

        Source = SensorSourceInfo.Sim("sim_lidar");

        Capabilities = SensorCapabilitySet.Empty
            .AddOrUpdate(SensorCapability.Create(
                name: "range_scan",
                confidence: 0.90,
                provider: "sim_lidar",
                frameId: Identity.FrameId,
                targetRateHz: _options.RateHz
            ))
            .AddOrUpdate(SensorCapability.Create(
                name: "obstacle_detection",
                confidence: 0.80,
                provider: "sim_lidar",
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
        {
            throw new InvalidOperationException("Sim LiDAR açık değil.");
        }

        var receiveUtc = DateTime.UtcNow;
        var captureUtc = _clock.NowUtc.UtcDateTime;

        try
        {
            var pose = ResolvePose();
            var scan = CreateScan(pose.X, pose.Y, pose.YawDeg, captureUtc).Sanitized();

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
                    backendName: "sim_lidar",
                    simulated: true,
                    confidence: _worldModel is null ? 0.50 : 1.0
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
                dataKind: SensorDataKind.Lidar,
                data: scan,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-lidar-{_sequence}"
            ).WithTags("sim", "lidar", "laser_scan");

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
            var quality = SensorQuality.Invalid($"Sim LiDAR read error: {ex.Message}");

            var emptyScan = new LaserScan(
                AngleMinDeg: -_options.FovDeg / 2.0,
                AngleMaxDeg: _options.FovDeg / 2.0,
                AngleIncrementDeg: ComputeAngleIncrementDeg(),
                RangeMinMeters: _options.RangeMinMeters,
                RangeMaxMeters: _options.RangeMaxMeters,
                RangesMeters: Array.Empty<double>(),
                Points: Array.Empty<LidarPoint2D>(),
                ObstacleHints: Array.Empty<LidarObstacleHint>(),
                TimestampUtc: DateTime.UtcNow
            ).Sanitized();

            var sample = SensorSample.Create(
                sensor: Identity,
                source: Source,
                sequence: _sequence,
                dataKind: SensorDataKind.Lidar,
                data: emptyScan,
                quality: quality,
                timing: timing,
                calibrationId: _options.CalibrationId,
                traceId: $"sim-lidar-invalid-{_sequence}"
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
            DataKind: SensorDataKind.Lidar,
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
            BackendName: "sim_lidar",
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

    private LaserScan CreateScan(double originX, double originY, double yawDeg, DateTime captureUtc)
    {
        var beamCount = Math.Max(2, _options.BeamCount);
        var angleMin = -_options.FovDeg / 2.0;
        var angleMax = _options.FovDeg / 2.0;
        var increment = ComputeAngleIncrementDeg();

        var ranges = new double[beamCount];
        var points = new List<LidarPoint2D>(beamCount);
        var hints = new List<LidarObstacleHint>();

        var raycaster = _worldModel is null ? null : new SimWorldRaycaster2D(_worldModel);

        for (var i = 0; i < beamCount; i++)
        {
            var localAngle = angleMin + i * increment;
            var worldAngle = yawDeg + localAngle;

            var ray = SimRay2D.FromAngleDeg(
                originX: originX,
                originY: originY,
                angleDeg: worldAngle,
                maxDistanceMeters: _options.RangeMaxMeters
            );

            var hit = raycaster?.Cast(ray) ?? SimRaycastHit2D.NoHit(_options.RangeMaxMeters);

            var range = hit.Hit
                ? hit.DistanceMeters + NoiseMeters()
                : _options.RangeMaxMeters;

            range = Clamp(range, _options.RangeMinMeters, _options.RangeMaxMeters);

            ranges[i] = range;

            var point = ray.PointAt(range);

            points.Add(new LidarPoint2D(
                X: point.X,
                Y: point.Y,
                RangeMeters: range,
                AngleDeg: localAngle,
                Hit: hit.Hit,
                ObjectId: hit.ObjectId
            ));

            if (hit.Hit)
            {
                hints.Add(new LidarObstacleHint(
                    ObjectId: hit.ObjectId,
                    Kind: hit.Kind,
                    RangeMeters: range,
                    BearingDeg: localAngle,
                    HitX: point.X,
                    HitY: point.Y
                ));
            }
        }

        return new LaserScan(
            AngleMinDeg: angleMin,
            AngleMaxDeg: angleMax,
            AngleIncrementDeg: increment,
            RangeMinMeters: _options.RangeMinMeters,
            RangeMaxMeters: _options.RangeMaxMeters,
            RangesMeters: ranges,
            Points: points,
            ObstacleHints: hints
                .GroupBy(x => x.ObjectId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(x => x.RangeMeters).First())
                .ToArray(),
            TimestampUtc: captureUtc
        );
    }

    private (double X, double Y, double YawDeg) ResolvePose()
    {
        if (_truthProvider is not null && _truthProvider.IsAvailable)
        {
            var truth = _truthProvider.GetLatestTruth().Sanitized();

            if (truth.IsFinite)
            {
                return (
                    X: truth.Position.X,
                    Y: truth.Position.Y,
                    YawDeg: truth.Orientation.YawDeg
                );
            }
        }

        return (0.0, 0.0, 0.0);
    }

    private double ComputeAngleIncrementDeg()
    {
        return _options.BeamCount <= 1
            ? _options.FovDeg
            : _options.FovDeg / (_options.BeamCount - 1);
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

        if (lastAgeMs > 2_000.0)
        {
            return SensorHealthState.Stale;
        }

        return SensorHealthState.Simulated;
    }

    private string BuildHealthSummary(SensorHealthState state)
    {
        return state switch
        {
            SensorHealthState.Simulated => "Sim LiDAR OK.",
            SensorHealthState.Offline => "Sim LiDAR offline.",
            SensorHealthState.Stale => "Sim LiDAR stale.",
            SensorHealthState.Degraded => $"Sim LiDAR degraded: {_consecutiveFailureCount} consecutive failures.",
            SensorHealthState.Failing => $"Sim LiDAR failing: {_consecutiveFailureCount} consecutive failures.",
            _ => $"Sim LiDAR state={state}."
        };
    }

    private double NoiseMeters()
    {
        if (_options.NoiseMeters <= 0.0)
        {
            return 0.0;
        }

        return (_random.NextDouble() * 2.0 - 1.0) * _options.NoiseMeters;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (!double.IsFinite(value))
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}