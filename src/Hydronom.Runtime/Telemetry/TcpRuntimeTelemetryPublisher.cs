using System.Text.Json;

namespace Hydronom.Runtime.Telemetry;

/// <summary>
/// RuntimeTelemetrySummary bilgisini TcpJsonServer üzerinden NDJSON olarak yayınlar.
/// Gateway tarafı type="RuntimeTelemetrySummary" alanı ile bu mesajı tanır.
/// </summary>
public sealed class TcpRuntimeTelemetryPublisher : IRuntimeTelemetryPublisher
{
    private readonly TcpJsonServer _server;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public TcpRuntimeTelemetryPublisher(TcpJsonServer server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    public Task PublishAsync(RuntimeTelemetrySummary summary, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var safe = summary.Sanitized();

        /*
         * RuntimeTelemetrySummary record struct'ında Type alanı yok.
         * Gateway RuntimeFrameParser mesajı type alanına göre route ettiği için
         * yayın payload'ına bilinçli olarak type ekliyoruz.
         */
        var payload = new
        {
            type = "RuntimeTelemetrySummary",

            runtimeId = safe.RuntimeId,
            timestampUtc = safe.TimestampUtc,
            overallHealth = safe.OverallHealth,
            hasCriticalIssue = safe.HasCriticalIssue,
            hasWarnings = safe.HasWarnings,

            sensorCount = safe.SensorCount,
            healthySensorCount = safe.HealthySensorCount,

            fusionEngineName = safe.FusionEngineName,
            fusionProducedCandidate = safe.FusionProducedCandidate,
            fusionConfidence = safe.FusionConfidence,

            vehicleId = safe.VehicleId,
            hasState = safe.HasState,
            stateX = safe.StateX,
            stateY = safe.StateY,
            stateZ = safe.StateZ,
            stateYawDeg = safe.StateYawDeg,
            stateConfidence = safe.StateConfidence,

            lastStateDecision = safe.LastStateDecision,
            lastStateAccepted = safe.LastStateAccepted,
            acceptedStateUpdateCount = safe.AcceptedStateUpdateCount,
            rejectedStateUpdateCount = safe.RejectedStateUpdateCount,

            summary = safe.Summary
        };

        /*
         * TcpJsonServer.BroadcastAsync(object) kendi içinde JSON serialize edip
         * bağlı client'lara tek satır NDJSON olarak yazar.
         */
        return _server.BroadcastAsync(payload);
    }
}