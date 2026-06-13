using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Runtime.Diagnostics.Snapshots;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Sensors.Diagnostics;
using Hydronom.Runtime.StateRuntime;

namespace Hydronom.Runtime.Operations.Snapshots;

/// <summary>
/// Runtime içindeki sensor, capability, fusion ve state authority durumlarını
/// tek diagnostics snapshot'a dönüştürür.
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
        VehicleStateStore stateStore,
        RuntimeSensorCapabilitySnapshot? sensorCapabilities = null)
    {
        ArgumentNullException.ThrowIfNull(fusionHost);
        ArgumentNullException.ThrowIfNull(stateStore);

        var stateSnapshot = stateStore.GetSnapshot();
        var stateTelemetry = fusionHost.LastTelemetry.Sanitized();
        var fusionDiagnostics = fusionHost.LastFusionDiagnostics.Sanitized();
        var capabilitySnapshot = (sensorCapabilities ?? RuntimeSensorCapabilitySnapshot.Empty).Sanitized();

        return new RuntimeDiagnosticsSnapshot(
            RuntimeId: _runtimeId,
            TimestampUtc: DateTime.UtcNow,
            SensorHealth: sensorHealth,
            SensorCapabilities: capabilitySnapshot,
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