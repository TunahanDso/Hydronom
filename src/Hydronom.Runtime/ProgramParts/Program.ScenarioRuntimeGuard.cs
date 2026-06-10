using System;
using Hydronom.Core.Domain;
using Hydronom.Runtime.Scenarios.Runtime;

partial class Program
{
    private static void TickScenarioRuntimeWithGeometryGuard(
        RuntimeScenarioController runtimeScenarioController,
        VehicleState state,
        long tickIndex,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        if (ShouldBlockScenarioAdvance(geometry))
        {
            if (tickIndex % 5 == 0 || geometry.CollisionCandidate)
            {
                Console.WriteLine(
                    "[SCENARIO] ADVANCE_BLOCKED_BY_GEOMETRY " +
                    $"tick={tickIndex} " +
                    $"nearest={geometry.NearestObstacleId ?? "none"} " +
                    $"clear={geometry.NearestClearanceMeters:F2} " +
                    $"risk={geometry.RiskScore:F2} " +
                    $"hard={geometry.HardBlocked} " +
                    $"collision={geometry.CollisionCandidate} " +
                    $"summary={geometry.Summary}"
                );
            }

            return;
        }

        runtimeScenarioController.Tick(
            state,
            tickIndex);
    }
}