using Hydronom.Core.Fusion.Diagnostics;
using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Core.State.Store;
using Hydronom.Core.State.Telemetry;
using Hydronom.Runtime.Sensors.Diagnostics;

namespace Hydronom.Runtime.Diagnostics.Snapshots;

/// <summary>
/// Runtime seviyesinde tek bakışta health/diagnostics snapshot modeli.
/// Sensor, capability, fusion ve state authority zincirinin son durumunu birlikte taşır.
/// </summary>
public readonly record struct RuntimeDiagnosticsSnapshot(
    string RuntimeId,
    DateTime TimestampUtc,
    SensorRuntimeHealth SensorHealth,
    RuntimeSensorCapabilitySnapshot SensorCapabilities,
    FusionDiagnostics FusionDiagnostics,
    VehicleStateStoreSnapshot StateStoreSnapshot,
    StateAuthorityTelemetry StateTelemetry,
    bool HasCriticalIssue,
    bool HasWarnings,
    string OverallHealth,
    string Summary
)
{
    public RuntimeDiagnosticsSnapshot Sanitized()
    {
        /*
         * SensorRuntimeHealth mevcut mimaride kendi Evaluate/FromSensors hattından geliyor.
         * Bu modelde Sanitized() metodu yok; bu yüzden doğrudan kullanıyoruz.
         */
        var sensorHealth = SensorHealth;
        var sensorCapabilities = SensorCapabilities.Sanitized();
        var fusionDiagnostics = FusionDiagnostics.Sanitized();
        var stateStore = StateStoreSnapshot.Sanitized();
        var stateTelemetry = StateTelemetry.Sanitized();

        var hasCritical =
            sensorHealth.HasCriticalIssue ||
            !stateStore.HasState;

        var hasWarnings =
            sensorHealth.DegradedCount > 0 ||
            sensorHealth.StaleCount > 0 ||
            stateStore.RejectedUpdateCount > 0 ||
            !fusionDiagnostics.ProducedCandidate;

        var health = ResolveOverallHealth(hasCritical, hasWarnings);

        return new RuntimeDiagnosticsSnapshot(
            RuntimeId: string.IsNullOrWhiteSpace(RuntimeId) ? "hydronom_runtime" : RuntimeId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            SensorHealth: sensorHealth,
            SensorCapabilities: sensorCapabilities,
            FusionDiagnostics: fusionDiagnostics,
            StateStoreSnapshot: stateStore,
            StateTelemetry: stateTelemetry,
            HasCriticalIssue: hasCritical,
            HasWarnings: hasWarnings,
            OverallHealth: health,
            Summary: string.IsNullOrWhiteSpace(Summary)
                ? BuildSummary(sensorHealth, sensorCapabilities, fusionDiagnostics, stateStore, stateTelemetry, health)
                : Summary.Trim()
        );
    }

    private static string ResolveOverallHealth(bool hasCritical, bool hasWarnings)
    {
        if (hasCritical)
        {
            return "Critical";
        }

        if (hasWarnings)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static string BuildSummary(
        SensorRuntimeHealth sensorHealth,
        RuntimeSensorCapabilitySnapshot sensorCapabilities,
        FusionDiagnostics fusionDiagnostics,
        VehicleStateStoreSnapshot stateStore,
        StateAuthorityTelemetry stateTelemetry,
        string overallHealth)
    {
        return
            $"{overallHealth}: " +
            $"sensors={sensorHealth.SensorCount}, " +
            $"healthySensors={sensorHealth.HealthyCount}, " +
            $"capabilities={sensorCapabilities.CapabilityCount}, " +
            $"availableCapabilities={sensorCapabilities.AvailableCount}, " +
            $"degradedCapabilities={sensorCapabilities.DegradedCount}, " +
            $"missingCapabilities={sensorCapabilities.MissingCount}, " +
            $"fusionCandidate={fusionDiagnostics.ProducedCandidate}, " +
            $"fusionConfidence={fusionDiagnostics.Confidence:F2}, " +
            $"stateAccepted={stateStore.AcceptedUpdateCount}, " +
            $"stateRejected={stateStore.RejectedUpdateCount}, " +
            $"lastDecision={stateTelemetry.LastDecision}";
    }
}