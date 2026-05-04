using Hydronom.Runtime.Diagnostics.Snapshots;

namespace Hydronom.Runtime.Telemetry;

/// <summary>
/// Runtime içindeki sensor/fusion/state bilgisini Gateway ve Ops tüketimine hazır
/// sade telemetry özetine dönüştürür.
/// </summary>
public sealed class TelemetryBridge
{
    public RuntimeTelemetrySummary Project(RuntimeDiagnosticsSnapshot snapshot)
    {
        var safe = snapshot.Sanitized();

        return new RuntimeTelemetrySummary(
            RuntimeId: safe.RuntimeId,
            TimestampUtc: safe.TimestampUtc,
            OverallHealth: safe.OverallHealth,
            HasCriticalIssue: safe.HasCriticalIssue,
            HasWarnings: safe.HasWarnings,
            SensorCount: safe.SensorHealth.SensorCount,
            HealthySensorCount: safe.SensorHealth.HealthyCount,
            FusionEngineName: safe.FusionDiagnostics.FusionEngineName,
            FusionProducedCandidate: safe.FusionDiagnostics.ProducedCandidate,
            FusionConfidence: safe.FusionDiagnostics.Confidence,
            VehicleId: safe.StateTelemetry.VehicleId,
            HasState: safe.StateTelemetry.HasState,
            StateX: safe.StateTelemetry.Pose.X,
            StateY: safe.StateTelemetry.Pose.Y,
            StateZ: safe.StateTelemetry.Pose.Z,
            StateYawDeg: safe.StateTelemetry.Pose.YawDeg,
            StateConfidence: safe.StateTelemetry.Confidence,
            LastStateDecision: safe.StateTelemetry.LastDecision.ToString(),
            LastStateAccepted: safe.StateTelemetry.LastUpdateAccepted,
            AcceptedStateUpdateCount: safe.StateTelemetry.AcceptedUpdateCount,
            RejectedStateUpdateCount: safe.StateTelemetry.RejectedUpdateCount,
            Summary: safe.Summary
        ).Sanitized();
    }
}

/// <summary>
/// Gateway/Ops tarafına taşınabilecek sade runtime telemetry özeti.
/// Bu DTO bilinçli olarak düz ve JSON dostu tutulmuştur.
/// </summary>
public readonly record struct RuntimeTelemetrySummary(
    string RuntimeId,
    DateTime TimestampUtc,
    string OverallHealth,
    bool HasCriticalIssue,
    bool HasWarnings,
    int SensorCount,
    int HealthySensorCount,
    string FusionEngineName,
    bool FusionProducedCandidate,
    double FusionConfidence,
    string VehicleId,
    bool HasState,
    double StateX,
    double StateY,
    double StateZ,
    double StateYawDeg,
    double StateConfidence,
    string LastStateDecision,
    bool LastStateAccepted,
    int AcceptedStateUpdateCount,
    int RejectedStateUpdateCount,
    string Summary
)
{
    public RuntimeTelemetrySummary Sanitized()
    {
        return new RuntimeTelemetrySummary(
            RuntimeId: string.IsNullOrWhiteSpace(RuntimeId) ? "hydronom_runtime" : RuntimeId.Trim(),
            TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            OverallHealth: string.IsNullOrWhiteSpace(OverallHealth) ? "Unknown" : OverallHealth.Trim(),
            HasCriticalIssue: HasCriticalIssue,
            HasWarnings: HasWarnings,
            SensorCount: SensorCount < 0 ? 0 : SensorCount,
            HealthySensorCount: HealthySensorCount < 0 ? 0 : HealthySensorCount,
            FusionEngineName: string.IsNullOrWhiteSpace(FusionEngineName) ? "unknown_fusion" : FusionEngineName.Trim(),
            FusionProducedCandidate: FusionProducedCandidate,
            FusionConfidence: Clamp01(FusionConfidence),
            VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
            HasState: HasState,
            StateX: Safe(StateX),
            StateY: Safe(StateY),
            StateZ: Safe(StateZ),
            StateYawDeg: Safe(StateYawDeg),
            StateConfidence: Clamp01(StateConfidence),
            LastStateDecision: string.IsNullOrWhiteSpace(LastStateDecision) ? "Unknown" : LastStateDecision.Trim(),
            LastStateAccepted: LastStateAccepted,
            AcceptedStateUpdateCount: AcceptedStateUpdateCount < 0 ? 0 : AcceptedStateUpdateCount,
            RejectedStateUpdateCount: RejectedStateUpdateCount < 0 ? 0 : RejectedStateUpdateCount,
            Summary: string.IsNullOrWhiteSpace(Summary) ? "Runtime telemetry summary." : Summary.Trim()
        );
    }

    private static double Safe(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }

    private static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}