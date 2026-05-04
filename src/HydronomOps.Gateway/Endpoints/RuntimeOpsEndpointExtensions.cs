using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Domain;
using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.Endpoints;

/// <summary>
/// Runtime/Ops endpoint'lerini map eder.
/// Bu endpointler Hydronom Ops arayüzünün runtime health, telemetry ve diagnostics
/// özetlerini sade JSON olarak tüketebilmesi için hazırlanmıştır.
/// </summary>
public static class RuntimeOpsEndpointExtensions
{
    public static IEndpointRouteBuilder MapRuntimeOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/runtime/telemetry-summary", (IGatewayStateStore stateStore) =>
        {
            var state = stateStore.GetCurrent();
            var response = BuildTelemetrySummary(state);

            return Results.Ok(response);
        });

        endpoints.MapGet("/runtime/diagnostics", (IGatewayStateStore stateStore) =>
        {
            var state = stateStore.GetCurrent();
            var response = BuildDiagnostics(state);

            return Results.Ok(response);
        });

        return endpoints;
    }

    private static GatewayRuntimeTelemetrySummaryDto BuildTelemetrySummary(VehicleAggregateState state)
    {
        var now = DateTime.UtcNow;
        var vehicleTelemetry = state.VehicleTelemetry;
        var sensorState = state.SensorState;
        var diagnosticsState = state.DiagnosticsState;

        var overallHealth = ResolveOverallHealth(state, out var hasCritical, out var hasWarnings);

        return new GatewayRuntimeTelemetrySummaryDto
        {
            RuntimeId = "hydronom_gateway_runtime",
            TimestampUtc = now,
            OverallHealth = overallHealth,
            HasCriticalIssue = hasCritical,
            HasWarnings = hasWarnings,
            RuntimeConnected = state.RuntimeConnected,
            PythonConnected = state.PythonConnected,
            WebSocketClientCount = state.WebSocketClientCount,
            TotalMessagesReceived = state.TotalMessagesReceived,
            TotalMessagesBroadcast = state.TotalMessagesBroadcast,

            VehicleId = vehicleTelemetry?.VehicleId ?? state.VehicleId ?? "hydronom-main",
            HasVehicleTelemetry = vehicleTelemetry is not null,

            X = vehicleTelemetry?.X ?? 0.0,
            Y = vehicleTelemetry?.Y ?? 0.0,
            Z = vehicleTelemetry?.Z ?? 0.0,
            YawDeg = vehicleTelemetry?.YawDeg ?? 0.0,
            HeadingDeg = vehicleTelemetry?.HeadingDeg ?? vehicleTelemetry?.YawDeg ?? 0.0,
            Vx = vehicleTelemetry?.Vx ?? 0.0,
            Vy = vehicleTelemetry?.Vy ?? 0.0,
            Vz = vehicleTelemetry?.Vz ?? 0.0,
            ObstacleCount = vehicleTelemetry?.ObstacleCount ?? 0,
            ObstacleAhead = vehicleTelemetry?.ObstacleAhead ?? false,

            HasSensorState = sensorState is not null,
            SensorHealthy = sensorState?.IsHealthy ?? false,
            SensorName = sensorState?.SensorName,
            SensorType = sensorState?.SensorType,

            HasDiagnosticsState = diagnosticsState is not null,
            GatewayStatus = diagnosticsState?.GatewayStatus,

            LastRuntimeIngressUtc = state.LastRuntimeIngressUtc,
            LastGatewayBroadcastUtc = state.LastGatewayBroadcastUtc,
            LastError = state.LastError,

            Summary = BuildSummary(
                overallHealth,
                state.RuntimeConnected,
                vehicleTelemetry is not null,
                sensorState?.IsHealthy,
                state.TotalMessagesReceived,
                state.WebSocketClientCount,
                state.LastError)
        };
    }

    private static GatewayRuntimeDiagnosticsDto BuildDiagnostics(VehicleAggregateState state)
    {
        var issues = BuildIssues(state);
        var overallHealth = ResolveOverallHealth(state, out var hasCritical, out var hasWarnings);

        return new GatewayRuntimeDiagnosticsDto
        {
            RuntimeId = "hydronom_gateway_runtime",
            TimestampUtc = DateTime.UtcNow,
            OverallHealth = overallHealth,
            HasCriticalIssue = hasCritical,
            HasWarnings = hasWarnings,

            RuntimeConnected = state.RuntimeConnected,
            PythonConnected = state.PythonConnected,
            WebSocketClientCount = state.WebSocketClientCount,
            IngressMessageCount = state.TotalMessagesReceived,
            BroadcastMessageCount = state.TotalMessagesBroadcast,

            StartedUtc = state.StartedUtc,
            LastUpdatedUtc = state.LastUpdatedUtc,
            LastRuntimeIngressUtc = state.LastRuntimeIngressUtc,
            LastVehicleTelemetryUtc = state.LastVehicleTelemetryUtc,
            LastSensorStateUtc = state.LastSensorStateUtc,
            LastDiagnosticsStateUtc = state.LastDiagnosticsStateUtc,
            LastGatewayBroadcastUtc = state.LastGatewayBroadcastUtc,

            VehicleId = state.VehicleId ?? "hydronom-main",
            HasVehicleTelemetry = state.VehicleTelemetry is not null,
            HasSensorState = state.SensorState is not null,
            HasDiagnosticsState = state.DiagnosticsState is not null,
            GatewayStatus = state.DiagnosticsState?.GatewayStatus,
            LastError = state.LastError,
            LastRawRuntimeLine = state.LastRawRuntimeLine,
            Issues = issues,

            Summary = BuildSummary(
                overallHealth,
                state.RuntimeConnected,
                state.VehicleTelemetry is not null,
                state.SensorState?.IsHealthy,
                state.TotalMessagesReceived,
                state.WebSocketClientCount,
                state.LastError)
        };
    }

    private static IReadOnlyList<GatewayRuntimeDiagnosticIssueDto> BuildIssues(VehicleAggregateState state)
    {
        var issues = new List<GatewayRuntimeDiagnosticIssueDto>();

        if (!state.RuntimeConnected)
        {
            issues.Add(new GatewayRuntimeDiagnosticIssueDto
            {
                Severity = "Warning",
                Code = "RUNTIME_DISCONNECTED",
                Message = "Runtime bağlantısı şu anda aktif görünmüyor."
            });
        }

        if (state.VehicleTelemetry is null)
        {
            issues.Add(new GatewayRuntimeDiagnosticIssueDto
            {
                Severity = "Warning",
                Code = "NO_VEHICLE_TELEMETRY",
                Message = "Gateway henüz vehicle telemetry almadı."
            });
        }

        if (state.SensorState is null)
        {
            issues.Add(new GatewayRuntimeDiagnosticIssueDto
            {
                Severity = "Warning",
                Code = "NO_SENSOR_STATE",
                Message = "Gateway henüz sensor state almadı."
            });
        }
        else if (!state.SensorState.IsHealthy)
        {
            issues.Add(new GatewayRuntimeDiagnosticIssueDto
            {
                Severity = "Critical",
                Code = "SENSOR_UNHEALTHY",
                Message = $"Sensor unhealthy: {state.SensorState.SensorName ?? "unknown"}"
            });
        }

        if (!string.IsNullOrWhiteSpace(state.LastError))
        {
            issues.Add(new GatewayRuntimeDiagnosticIssueDto
            {
                Severity = "Critical",
                Code = "GATEWAY_LAST_ERROR",
                Message = state.LastError
            });
        }

        return issues;
    }

    private static string ResolveOverallHealth(
        VehicleAggregateState state,
        out bool hasCritical,
        out bool hasWarnings)
    {
        var issues = BuildIssues(state);

        hasCritical = issues.Any(x => string.Equals(x.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        hasWarnings = issues.Any(x => string.Equals(x.Severity, "Warning", StringComparison.OrdinalIgnoreCase));

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
        string overallHealth,
        bool runtimeConnected,
        bool hasVehicleTelemetry,
        bool? sensorHealthy,
        long ingressCount,
        int webSocketClientCount,
        string? lastError)
    {
        var sensorText = sensorHealthy.HasValue
            ? sensorHealthy.Value ? "healthy" : "unhealthy"
            : "unknown";

        var errorText = string.IsNullOrWhiteSpace(lastError)
            ? "no-error"
            : "has-error";

        return
            $"{overallHealth}: " +
            $"runtimeConnected={runtimeConnected}, " +
            $"vehicleTelemetry={hasVehicleTelemetry}, " +
            $"sensor={sensorText}, " +
            $"ingress={ingressCount}, " +
            $"wsClients={webSocketClientCount}, " +
            $"error={errorText}";
    }
}