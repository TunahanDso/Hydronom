using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Telemetry;
using Hydronom.Runtime.StateRuntime;

namespace Hydronom.Runtime.FusionRuntime;

/// <summary>
/// SensorSample batch'lerini FusionEngine/StateEstimator hattına besleyen runtime host.
/// Üretilen StateUpdateCandidate, StateUpdatePipeline üzerinden authority kapısından geçirilir.
/// </summary>
public sealed class FusionRuntimeHost
{
    private readonly StateEstimatorRunner _estimatorRunner;
    private readonly StateUpdatePipeline _statePipeline;
    private readonly VehicleStateStore _stateStore;
    private readonly StateTelemetryBridge _stateTelemetryBridge;

    private StateUpdateResult? _lastStateUpdateResult;
    private StateUpdateCandidate? _lastCandidate;
    private StateAuthorityTelemetry _lastTelemetry;

    public FusionRuntimeHost(
        StateEstimatorRunner estimatorRunner,
        StateUpdatePipeline statePipeline,
        VehicleStateStore stateStore,
        StateTelemetryBridge? stateTelemetryBridge = null)
    {
        _estimatorRunner = estimatorRunner ?? throw new ArgumentNullException(nameof(estimatorRunner));
        _statePipeline = statePipeline ?? throw new ArgumentNullException(nameof(statePipeline));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _stateTelemetryBridge = stateTelemetryBridge ?? new StateTelemetryBridge();

        _lastTelemetry = _stateTelemetryBridge.Project(_stateStore);
    }

    public string EstimatorName => _estimatorRunner.EstimatorName;

    public FusionDiagnostics LastFusionDiagnostics => _estimatorRunner.LastDiagnostics;

    public StateUpdateCandidate? LastCandidate => _lastCandidate;

    public StateUpdateResult? LastStateUpdateResult => _lastStateUpdateResult;

    public StateAuthorityTelemetry LastTelemetry => _lastTelemetry;

    public bool HasAcceptedState => _stateStore.AcceptedUpdateCount > 0;

    /// <summary>
    /// Bir batch sensör örneğini işler:
    /// - estimator candidate üretir
    /// - candidate varsa authority pipeline'a gönderilir
    /// - store/telemetry güncellenir
    /// </summary>
    public FusionRuntimeHostTickResult Tick(
        IReadOnlyList<SensorSample> samples,
        FusionContext context,
        DateTime? utcNow = null)
    {
        var safeSamples = samples ?? Array.Empty<SensorSample>();
        var safeContext = context.Sanitized();

        var candidate = _estimatorRunner.Run(safeSamples, safeContext);
        _lastCandidate = candidate;

        if (candidate is null)
        {
            _lastTelemetry = _stateTelemetryBridge.Project(_stateStore);

            return new FusionRuntimeHostTickResult(
                TimestampUtc: DateTime.UtcNow,
                InputSampleCount: safeSamples.Count,
                CandidateProduced: false,
                StateUpdateSubmitted: false,
                StateUpdateAccepted: false,
                Decision: StateUpdateDecision.Unknown,
                Reason: "Estimator candidate üretmedi.",
                Telemetry: _lastTelemetry,
                FusionDiagnostics: LastFusionDiagnostics
            ).Sanitized();
        }

        var result = _statePipeline.Submit(candidate.Value, utcNow ?? safeContext.TimestampUtc);
        _lastStateUpdateResult = result;
        _lastTelemetry = _stateTelemetryBridge.Project(_stateStore);

        return new FusionRuntimeHostTickResult(
            TimestampUtc: DateTime.UtcNow,
            InputSampleCount: safeSamples.Count,
            CandidateProduced: true,
            StateUpdateSubmitted: true,
            StateUpdateAccepted: result.Accepted,
            Decision: result.Decision,
            Reason: result.Reason,
            Telemetry: _lastTelemetry,
            FusionDiagnostics: LastFusionDiagnostics
        ).Sanitized();
    }
}

/// <summary>
/// FusionRuntimeHost tek tick sonucudur.
/// Runtime loop ve testler bu modeli kullanarak zincirin çalışıp çalışmadığını kontrol eder.
/// </summary>
public readonly record struct FusionRuntimeHostTickResult(
    DateTime TimestampUtc,
    int InputSampleCount,
    bool CandidateProduced,
    bool StateUpdateSubmitted,
    bool StateUpdateAccepted,
    StateUpdateDecision Decision,
    string Reason,
    StateAuthorityTelemetry Telemetry,
    FusionDiagnostics FusionDiagnostics
)
{
    public FusionRuntimeHostTickResult Sanitized()
    {
        return new FusionRuntimeHostTickResult(
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            InputSampleCount: InputSampleCount < 0 ? 0 : InputSampleCount,
            CandidateProduced: CandidateProduced,
            StateUpdateSubmitted: StateUpdateSubmitted,
            StateUpdateAccepted: StateUpdateAccepted,
            Decision: Decision,
            Reason: string.IsNullOrWhiteSpace(Reason) ? "NO_REASON" : Reason.Trim(),
            Telemetry: Telemetry.Sanitized(),
            FusionDiagnostics: FusionDiagnostics.Sanitized()
        );
    }
}