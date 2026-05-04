using Hydronom.Core.Sensors.Common.Diagnostics;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Operations.Snapshots;
using Hydronom.Runtime.StateRuntime;

namespace Hydronom.Runtime.Telemetry;

/// <summary>
/// Runtime içindeki sensor health, fusion ve state authority bilgisinden
/// RuntimeTelemetrySummary üretip dış publisher'a aktarır.
///
/// Bu sınıfın görevi sensör verisini TCP'den almak değildir.
/// C# Primary akışta sensör/fusion/state bilgisi runtime içinde kalır;
/// bu host yalnızca oluşan operasyonel özeti Gateway/Ops tarafına yayınlar.
/// </summary>
public sealed class RuntimeTelemetryHost
{
    private readonly RuntimeOperationSnapshotBuilder _snapshotBuilder;
    private readonly TelemetryBridge _telemetryBridge;
    private readonly IRuntimeTelemetryPublisher _publisher;

    public RuntimeTelemetryHost(
        RuntimeOperationSnapshotBuilder snapshotBuilder,
        TelemetryBridge telemetryBridge,
        IRuntimeTelemetryPublisher publisher)
    {
        _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
        _telemetryBridge = telemetryBridge ?? throw new ArgumentNullException(nameof(telemetryBridge));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public RuntimeTelemetrySummary LastSummary { get; private set; }

    public RuntimeDiagnosticsPublishResult LastResult { get; private set; } =
        RuntimeDiagnosticsPublishResult.NotPublished("not_started");

    public async Task<RuntimeDiagnosticsPublishResult> PublishAsync(
        SensorRuntimeHealth sensorHealth,
        FusionRuntimeHost fusionHost,
        VehicleStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            var cancelled = RuntimeDiagnosticsPublishResult.NotPublished("cancelled");
            LastResult = cancelled;
            return cancelled;
        }

        ArgumentNullException.ThrowIfNull(fusionHost);
        ArgumentNullException.ThrowIfNull(stateStore);

        var snapshot = _snapshotBuilder
            .Build(sensorHealth, fusionHost, stateStore)
            .Sanitized();

        var summary = _telemetryBridge
            .Project(snapshot)
            .Sanitized();

        await _publisher.PublishAsync(summary, cancellationToken).ConfigureAwait(false);

        LastSummary = summary;

        var result = RuntimeDiagnosticsPublishResult.CreatePublished(
            runtimeId: summary.RuntimeId,
            vehicleId: summary.VehicleId,
            timestampUtc: summary.TimestampUtc,
            overallHealth: summary.OverallHealth,
            hasCriticalIssue: summary.HasCriticalIssue,
            hasWarnings: summary.HasWarnings
        );

        LastResult = result;
        return result;
    }
}

/// <summary>
/// Runtime telemetry publish sonucunu temsil eder.
/// Log, test ve runtime health raporlamasında kullanılabilir.
/// </summary>
public readonly record struct RuntimeDiagnosticsPublishResult(
    bool Published,
    string RuntimeId,
    string VehicleId,
    DateTime TimestampUtc,
    string OverallHealth,
    bool HasCriticalIssue,
    bool HasWarnings,
    string Reason
)
{
    public static RuntimeDiagnosticsPublishResult CreatePublished(
        string runtimeId,
        string vehicleId,
        DateTime timestampUtc,
        string overallHealth,
        bool hasCriticalIssue,
        bool hasWarnings)
    {
        return new RuntimeDiagnosticsPublishResult(
            Published: true,
            RuntimeId: string.IsNullOrWhiteSpace(runtimeId) ? "hydronom_runtime" : runtimeId.Trim(),
            VehicleId: string.IsNullOrWhiteSpace(vehicleId) ? "UNKNOWN" : vehicleId.Trim(),
            TimestampUtc: timestampUtc == default ? DateTime.UtcNow : timestampUtc,
            OverallHealth: string.IsNullOrWhiteSpace(overallHealth) ? "Unknown" : overallHealth.Trim(),
            HasCriticalIssue: hasCriticalIssue,
            HasWarnings: hasWarnings,
            Reason: "published"
        );
    }

    public static RuntimeDiagnosticsPublishResult NotPublished(string reason)
    {
        return new RuntimeDiagnosticsPublishResult(
            Published: false,
            RuntimeId: "hydronom_runtime",
            VehicleId: "UNKNOWN",
            TimestampUtc: DateTime.UtcNow,
            OverallHealth: "Unknown",
            HasCriticalIssue: false,
            HasWarnings: false,
            Reason: string.IsNullOrWhiteSpace(reason) ? "not_published" : reason.Trim()
        );
    }
}