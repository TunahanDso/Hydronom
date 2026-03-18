using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Domain;
using HydronomOps.Gateway.Infrastructure.Time;

namespace HydronomOps.Gateway.Services.Health;

/// <summary>
/// Gateway sağlık ve heartbeat üretimini yapar.
/// </summary>
public sealed class GatewayHealthService : IGatewayHealthService
{
    private readonly ISystemClock _clock;

    public GatewayHealthService(ISystemClock clock)
    {
        _clock = clock;
    }

    public HeartbeatDto BuildHeartbeat(VehicleAggregateState state)
    {
        var now = _clock.UtcNow;
        var lastRuntime = state.LastRuntimeIngressUtc ?? state.StartedUtc;
        var ageMs = Math.Max(0, (now - lastRuntime).TotalMilliseconds);

        return new HeartbeatDto
        {
            TimestampUtc = now,
            IsAlive = true,
            ConnectedClientCount = state.WebSocketClientCount,
            RuntimeConnected = state.RuntimeConnected,
            RuntimeFreshness = new FreshnessDto
            {
                TimestampUtc = lastRuntime,
                AgeMs = ageMs,
                IsFresh = ageMs <= 5000,
                ThresholdMs = 5000,
                Source = "runtime"
            },
            UptimeMs = Math.Max(0, (now - state.StartedUtc).TotalMilliseconds)
        };
    }

    public object BuildStatusResponse(VehicleAggregateState state)
    {
        return new
        {
            service = "HydronomOps.Gateway",
            vehicleId = state.VehicleId,
            runtimeConnected = state.RuntimeConnected,
            pythonConnected = state.PythonConnected,
            webSocketClientCount = state.WebSocketClientCount,
            totalMessagesReceived = state.TotalMessagesReceived,
            totalMessagesBroadcast = state.TotalMessagesBroadcast,
            lastRuntimeIngressUtc = state.LastRuntimeIngressUtc,
            lastGatewayBroadcastUtc = state.LastGatewayBroadcastUtc,
            lastError = state.LastError,
            utc = _clock.UtcNow
        };
    }

    public object BuildHealthResponse(VehicleAggregateState state)
    {
        var diagnostics = BuildDiagnosticsState(state);

        return new
        {
            isHealthy = diagnostics.GatewayStatus == "healthy" || diagnostics.GatewayStatus == "running",
            diagnostics,
            utc = _clock.UtcNow
        };
    }

    public DiagnosticsStateDto BuildDiagnosticsState(VehicleAggregateState state)
    {
        var now = _clock.UtcNow;
        var lastRuntime = state.LastRuntimeIngressUtc ?? state.StartedUtc;
        var ageMs = Math.Max(0, (now - lastRuntime).TotalMilliseconds);

        return new DiagnosticsStateDto
        {
            TimestampUtc = now,
            GatewayStatus = state.RuntimeConnected ? "running" : "degraded",
            RuntimeConnected = state.RuntimeConnected,
            HasWebSocketClients = state.WebSocketClientCount > 0,
            ConnectedWebSocketClients = state.WebSocketClientCount,
            LastRuntimeMessageUtc = state.LastRuntimeIngressUtc,
            RuntimeFreshness = new FreshnessDto
            {
                TimestampUtc = lastRuntime,
                AgeMs = ageMs,
                IsFresh = ageMs <= 5000,
                ThresholdMs = 5000,
                Source = "runtime"
            },
            LastError = state.LastError,
            IngressMessageCount = state.TotalMessagesReceived,
            BroadcastMessageCount = state.TotalMessagesBroadcast
        };
    }
}