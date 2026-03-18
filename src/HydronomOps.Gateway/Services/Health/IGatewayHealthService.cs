using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.Health;

/// <summary>
/// Gateway sağlık/heartbeat üretim servis sözleşmesi.
/// </summary>
public interface IGatewayHealthService
{
    HeartbeatDto BuildHeartbeat(VehicleAggregateState state);
    object BuildStatusResponse(VehicleAggregateState state);
    object BuildHealthResponse(VehicleAggregateState state);
    DiagnosticsStateDto BuildDiagnosticsState(VehicleAggregateState state);
}