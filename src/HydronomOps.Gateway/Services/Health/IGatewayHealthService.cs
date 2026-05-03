using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.Health;

/// <summary>
/// Gateway saÄŸlÄ±k/heartbeat Ã¼retim servis sÃ¶zleÅŸmesi.
/// </summary>
public interface IGatewayHealthService
{
    HeartbeatDto BuildHeartbeat(VehicleAggregateState state);
    object BuildStatusResponse(VehicleAggregateState state);
    object BuildHealthResponse(VehicleAggregateState state);
    DiagnosticsStateDto BuildDiagnosticsState(VehicleAggregateState state);
}
