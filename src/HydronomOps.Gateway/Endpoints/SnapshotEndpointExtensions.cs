using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.Endpoints;

/// <summary>
/// Anlık gateway snapshot endpoint'ini map eder.
/// </summary>
public static class SnapshotEndpointExtensions
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/snapshot", (IGatewayStateStore stateStore) =>
        {
            var state = stateStore.GetCurrent();

            var response = new
            {
                vehicleId = state.VehicleId,
                startedUtc = state.StartedUtc,
                lastUpdatedUtc = state.LastUpdatedUtc,
                lastRuntimeIngressUtc = state.LastRuntimeIngressUtc,
                lastVehicleTelemetryUtc = state.LastVehicleTelemetryUtc,
                lastMissionStateUtc = state.LastMissionStateUtc,
                lastWorldStateUtc = state.LastWorldStateUtc,
                lastSensorStateUtc = state.LastSensorStateUtc,
                lastDebugSensorStateUtc = state.LastDebugSensorStateUtc,
                lastActuatorStateUtc = state.LastActuatorStateUtc,
                lastDiagnosticsStateUtc = state.LastDiagnosticsStateUtc,
                lastGatewayBroadcastUtc = state.LastGatewayBroadcastUtc,
                lastError = state.LastError,

                runtimeConnected = state.RuntimeConnected,
                pythonConnected = state.PythonConnected,
                webSocketClientCount = state.WebSocketClientCount,
                totalMessagesReceived = state.TotalMessagesReceived,
                totalMessagesBroadcast = state.TotalMessagesBroadcast,

                vehicleTelemetry = state.VehicleTelemetry,
                missionState = state.MissionState,
                worldState = state.WorldState,
                sensorState = state.SensorState,
                runtimeSensorState = state.RuntimeSensorState,
                debugSensorState = state.DebugSensorState,
                actuatorState = state.ActuatorState,
                diagnosticsState = state.DiagnosticsState,

                logs = state.Logs
            };

            return Results.Ok(response);
        });

        return endpoints;
    }
}