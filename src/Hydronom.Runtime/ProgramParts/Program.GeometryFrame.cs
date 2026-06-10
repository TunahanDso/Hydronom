using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Planning;
using Hydronom.Runtime.Scenarios.Runtime;
using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

partial class Program
{
    private static RuntimeScenarioGeometryAuthority CreateScenarioGeometryAuthority(
        IConfiguration config,
        RuntimeWorldModel runtimeWorldModel)
    {
        return new RuntimeScenarioGeometryAuthority(
            config,
            runtimeWorldModel);
    }

    private static RuntimeScenarioGeometrySnapshot UpdateScenarioGeometrySnapshot(
        RuntimeScenarioGeometryAuthority geometryAuthority,
        RuntimePlanningCache planningCache,
        ITaskManager tasks,
        VehicleState state,
        long tickIndex)
    {
        if (geometryAuthority is null)
            return BuildGeometryUnavailableSnapshot(tickIndex);

        var planningSnapshot = planningCache.Snapshot();

        var referenceTarget = ResolveGeometryReferenceTarget(
            planningSnapshot,
            tasks.CurrentTask,
            state);

        return geometryAuthority
            .Update(
                state,
                referenceTarget,
                tickIndex,
                DateTime.UtcNow)
            .Sanitized();
    }

    private static RuntimeScenarioGeometrySnapshot BuildGeometryUnavailableSnapshot(
        long tickIndex)
    {
        return RuntimeScenarioGeometrySnapshot.Empty with
        {
            TimestampUtc = DateTime.UtcNow,
            TickIndex = tickIndex,
            Summary = "GEOMETRY_AUTHORITY_UNAVAILABLE"
        };
    }
}