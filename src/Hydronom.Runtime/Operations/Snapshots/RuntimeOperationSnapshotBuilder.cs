using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Runtime.Diagnostics.Snapshots;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.StateRuntime;

namespace Hydronom.Runtime.Operations.Snapshots;

/// <summary>
/// Runtime içindeki sensor, fusion ve state authority durumlarını tek diagnostics snapshot'a dönüştürür.
/// </summary>
public sealed class RuntimeOperationSnapshotBuilder
{
    private readonly string _runtimeId;

    public RuntimeOperationSnapshotBuilder(string runtimeId = "hydronom_runtime")
    {
        _runtimeId = string.IsNullOrWhiteSpace(runtimeId)
            ? "hydronom_runtime"
            : runtimeId.Trim();
    }

    public RuntimeDiagnosticsSnapshot Build(
        SensorRuntimeHealth sensorHealth,
        FusionRuntimeHost fusionHost,
        VehicleStateStore stateStore)
    {
        ArgumentNullException.ThrowIfNull(fusionHost);
        ArgumentNullException.ThrowIfNull(stateStore);

        var stateSnapshot = stateStore.GetSnapshot();
        var stateTelemetry = fusionHost.LastTelemetry.Sanitized();
        var fusionDiagnostics = fusionHost.LastFusionDiagnostics.Sanitized();

        return new RuntimeDiagnosticsSnapshot(
            RuntimeId: _runtimeId,
            TimestampUtc: DateTime.UtcNow,
            SensorHealth: sensorHealth,
            FusionDiagnostics: fusionDiagnostics,
            StateStoreSnapshot: stateSnapshot,
            StateTelemetry: stateTelemetry,
            HasCriticalIssue: false,
            HasWarnings: false,
            OverallHealth: "",
            Summary: ""
        ).Sanitized();
    }
}