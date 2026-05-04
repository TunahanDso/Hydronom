using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.RuntimeOps;

/// <summary>
/// Gateway state içinden Hydronom Ops için runtime telemetry/diagnostics projection üretir.
/// </summary>
public interface IGatewayRuntimeOpsProjectionService
{
    GatewayRuntimeTelemetrySummaryDto BuildTelemetrySummary(VehicleAggregateState state);

    GatewayRuntimeDiagnosticsDto BuildDiagnostics(VehicleAggregateState state);
}