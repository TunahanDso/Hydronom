using HydronomOps.Gateway.Services.Health;
using HydronomOps.Gateway.Services.State;

namespace HydronomOps.Gateway.Endpoints;

/// <summary>
/// Status / health endpoint'lerini map eder.
/// </summary>
public static class StatusEndpointExtensions
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/status", (
            IGatewayHealthService healthService,
            IGatewayStateStore stateStore) =>
        {
            var state = stateStore.GetCurrent();
            var response = healthService.BuildStatusResponse(state);
            return Results.Ok(response);
        });

        endpoints.MapGet("/health", (
            IGatewayHealthService healthService,
            IGatewayStateStore stateStore) =>
        {
            var state = stateStore.GetCurrent();
            var response = healthService.BuildHealthResponse(state);
            return Results.Ok(response);
        });

        return endpoints;
    }
}
