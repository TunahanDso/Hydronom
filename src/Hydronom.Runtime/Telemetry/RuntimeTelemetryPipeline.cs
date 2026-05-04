using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.StateRuntime;

namespace Hydronom.Runtime.Telemetry;

/// <summary>
/// C# Primary sensor/fusion/state/telemetry zincirini tek runtime pipeline olarak çalıştırır.
///
/// Bu sınıf sensör verisini TCP'den almaz.
/// Normal C# Primary akışta sensörler ISensorRuntime üzerinden doğrudan runtime içinde okunur.
/// TCP yalnızca RuntimeTelemetrySummary gibi dış telemetry özetlerini Gateway/Ops tarafına yayınlamak için kullanılır.
/// </summary>
public sealed class RuntimeTelemetryPipeline : IAsyncDisposable
{
    private readonly ISensorRuntime _sensorRuntime;
    private readonly FusionRuntimeHost _fusionHost;
    private readonly VehicleStateStore _stateStore;
    private readonly RuntimeTelemetryHost _telemetryHost;
    private readonly string _vehicleId;
    private readonly string _frameId;
    private readonly double _maxSampleAgeMs;

    private bool _started;
    private long _tickIndex;

    public RuntimeTelemetryPipeline(
        ISensorRuntime sensorRuntime,
        FusionRuntimeHost fusionHost,
        VehicleStateStore stateStore,
        RuntimeTelemetryHost telemetryHost,
        string vehicleId,
        string frameId = "map",
        double maxSampleAgeMs = 2_000.0)
    {
        _sensorRuntime = sensorRuntime ?? throw new ArgumentNullException(nameof(sensorRuntime));
        _fusionHost = fusionHost ?? throw new ArgumentNullException(nameof(fusionHost));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _telemetryHost = telemetryHost ?? throw new ArgumentNullException(nameof(telemetryHost));

        _vehicleId = string.IsNullOrWhiteSpace(vehicleId)
            ? "hydronom-main"
            : vehicleId.Trim();

        _frameId = string.IsNullOrWhiteSpace(frameId)
            ? "map"
            : frameId.Trim();

        _maxSampleAgeMs = double.IsFinite(maxSampleAgeMs) && maxSampleAgeMs > 0.0
            ? maxSampleAgeMs
            : 2_000.0;
    }

    public bool IsStarted => _started;

    public long TickIndex => _tickIndex;

    public RuntimeDiagnosticsPublishResult LastPublishResult => _telemetryHost.LastResult;

    public RuntimeTelemetrySummary LastSummary => _telemetryHost.LastSummary;

    public VehicleStateStore StateStore => _stateStore;

    public FusionRuntimeHost FusionHost => _fusionHost;

    public ISensorRuntime SensorRuntime => _sensorRuntime;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _sensorRuntime.StartAsync().ConfigureAwait(false);
        _started = true;
    }

    public async ValueTask StopAsync()
    {
        if (!_started)
        {
            return;
        }

        await _sensorRuntime.StopAsync().ConfigureAwait(false);
        _started = false;
    }

    public async Task<RuntimeTelemetryPipelineTickResult> TickAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return RuntimeTelemetryPipelineTickResult.NotExecuted(
                tickIndex: _tickIndex,
                reason: "cancelled"
            );
        }

        if (!_started)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }

        var samples = await _sensorRuntime.ReadBatchAsync().ConfigureAwait(false);
        var sensorHealth = _sensorRuntime.GetHealth();

        var context = FusionContext.Create(
            vehicleId: _vehicleId,
            frameId: _frameId,
            maxSampleAgeMs: _maxSampleAgeMs,
            traceId: $"runtime-telemetry-pipeline-{_tickIndex}"
        );

        var fusionTick = _fusionHost
            .Tick(samples, context, utcNow: DateTime.UtcNow)
            .Sanitized();

        var publishResult = await _telemetryHost
            .PublishAsync(sensorHealth, _fusionHost, _stateStore, cancellationToken)
            .ConfigureAwait(false);

        var result = RuntimeTelemetryPipelineTickResult.CreateExecuted(
            tickIndex: _tickIndex,
            sampleCount: samples.Count,
            sensorCount: sensorHealth.SensorCount,
            healthySensorCount: sensorHealth.HealthyCount,
            candidateProduced: fusionTick.CandidateProduced,
            stateSubmitted: fusionTick.StateUpdateSubmitted,
            stateAccepted: fusionTick.StateUpdateAccepted,
            publishResult: publishResult
        );

        _tickIndex++;
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// RuntimeTelemetryPipeline tek tick sonucunu temsil eder.
/// Ana loop ve smoke testlerde zincirin ne yaptığını izlemek için kullanılır.
/// </summary>
public readonly record struct RuntimeTelemetryPipelineTickResult(
    bool Executed,
    long TickIndex,
    int SampleCount,
    int SensorCount,
    int HealthySensorCount,
    bool CandidateProduced,
    bool StateSubmitted,
    bool StateAccepted,
    bool Published,
    string RuntimeId,
    string VehicleId,
    string OverallHealth,
    bool HasCriticalIssue,
    bool HasWarnings,
    string Reason
)
{
    public static RuntimeTelemetryPipelineTickResult CreateExecuted(
        long tickIndex,
        int sampleCount,
        int sensorCount,
        int healthySensorCount,
        bool candidateProduced,
        bool stateSubmitted,
        bool stateAccepted,
        RuntimeDiagnosticsPublishResult publishResult)
    {
        return new RuntimeTelemetryPipelineTickResult(
            Executed: true,
            TickIndex: tickIndex,
            SampleCount: sampleCount < 0 ? 0 : sampleCount,
            SensorCount: sensorCount < 0 ? 0 : sensorCount,
            HealthySensorCount: healthySensorCount < 0 ? 0 : healthySensorCount,
            CandidateProduced: candidateProduced,
            StateSubmitted: stateSubmitted,
            StateAccepted: stateAccepted,
            Published: publishResult.Published,
            RuntimeId: publishResult.RuntimeId,
            VehicleId: publishResult.VehicleId,
            OverallHealth: publishResult.OverallHealth,
            HasCriticalIssue: publishResult.HasCriticalIssue,
            HasWarnings: publishResult.HasWarnings,
            Reason: publishResult.Reason
        );
    }

    public static RuntimeTelemetryPipelineTickResult NotExecuted(long tickIndex, string reason)
    {
        return new RuntimeTelemetryPipelineTickResult(
            Executed: false,
            TickIndex: tickIndex,
            SampleCount: 0,
            SensorCount: 0,
            HealthySensorCount: 0,
            CandidateProduced: false,
            StateSubmitted: false,
            StateAccepted: false,
            Published: false,
            RuntimeId: "hydronom_runtime",
            VehicleId: "UNKNOWN",
            OverallHealth: "Unknown",
            HasCriticalIssue: false,
            HasWarnings: false,
            Reason: string.IsNullOrWhiteSpace(reason) ? "not_executed" : reason.Trim()
        );
    }
}