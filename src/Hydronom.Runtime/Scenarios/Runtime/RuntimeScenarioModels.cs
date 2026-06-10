using System;
using System.Collections.Generic;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed class RuntimeScenarioSnapshot
{
    public string? Message { get; set; }
    public bool HasActiveScenario { get; set; }
    public bool IsRunning { get; set; }
    public string? ScenarioId { get; set; }
    public string? ScenarioName { get; set; }

    /// <summary>
    /// Runtime tarafından kullanılan operasyonel vehicle id.
    /// Ops/Gateway tarafında telemetry, mission, world ve actuator frame'leri bu kimlikle birleşir.
    /// </summary>
    public string? VehicleId { get; set; }

    /// <summary>
    /// Scenario dosyasından gelen metadata/default araç kimliği.
    /// Bu değer runtime identity yerine kullanılmaz; debug/izleme için tutulur.
    /// </summary>
    public string? ScenarioVehicleId { get; set; }

    public string State { get; set; } = "None";
    public string? RunId { get; set; }
    public string? CurrentObjectiveId { get; set; }
    public int CompletedObjectiveCount { get; set; }
    public int TotalObjectiveCount { get; set; }
    public string? LastCompletedObjectiveId { get; set; }
    public double? LastDistanceToTargetMeters { get; set; }
    public double? LastDistance3DToTargetMeters { get; set; }
    public string? LastTickSummary { get; set; }
    public string? SessionSummary { get; set; }

    public double? ActiveObjectiveTargetX { get; set; }
    public double? ActiveObjectiveTargetY { get; set; }
    public double? ActiveObjectiveTargetZ { get; set; }
    public double? ActiveObjectiveToleranceMeters { get; set; }

    public IReadOnlyList<RuntimeScenarioRoutePoint> RoutePoints { get; set; } =
        Array.Empty<RuntimeScenarioRoutePoint>();

    public IReadOnlyList<RuntimeScenarioWorldObject> WorldObjects { get; set; } =
        Array.Empty<RuntimeScenarioWorldObject>();
}

public sealed class RuntimeScenarioRoutePoint
{
    public string Id { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public int Index { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double ToleranceMeters { get; set; }
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
}

public sealed class RuntimeScenarioWorldObject
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "object";
    public string? Label { get; set; }
    public string? ObjectiveId { get; set; }
    public string? Side { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Radius { get; set; } = 0.5;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsDetectable { get; set; }
}