using System.Text.Json;
using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.World;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void ProcessRuntimeWorldObjects(JsonElement root)
    {
        var timestamp = ReadTimestamp(root);

        var worldState = new WorldStateDto
        {
            TimestampUtc = timestamp,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            Source = GetString(root, "source", "Source") ?? "runtime",
            ScenarioId = GetString(root, "scenarioId", "ScenarioId"),
            ScenarioName = GetString(root, "scenarioName", "ScenarioName"),
            RunId = GetString(root, "runId", "RunId"),
            CurrentObjectiveId = GetString(root, "currentObjectiveId", "CurrentObjectiveId"),
            ActiveObjectiveTarget = ReadWorldTarget(root, "activeObjectiveTarget", "ActiveObjectiveTarget"),
            Route = ReadWorldRoute(root),
            Objects = ReadWorldObjects(root),
            Freshness = new FreshnessDto
            {
                TimestampUtc = timestamp,
                AgeMs = 0,
                IsFresh = true,
                ThresholdMs = 5000,
                Source = "runtime-world-objects"
            }
        };

        worldState.Metrics["route.count"] = worldState.Route.Count;
        worldState.Metrics["objects.count"] = worldState.Objects.Count;

        if (!string.IsNullOrWhiteSpace(worldState.ScenarioId))
        {
            worldState.Fields["scenarioId"] = worldState.ScenarioId;
        }

        if (!string.IsNullOrWhiteSpace(worldState.CurrentObjectiveId))
        {
            worldState.Fields["currentObjectiveId"] = worldState.CurrentObjectiveId;
        }

        _stateStore.SetWorldState(worldState);
        _stateStore.TouchRuntimeMessage("RuntimeWorldObjects");
    }

    private static WorldTargetDto? ReadWorldTarget(
        JsonElement root,
        params string[] propertyNames)
    {
        JsonElement targetElement = default;
        var found = false;

        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out targetElement) &&
                targetElement.ValueKind == JsonValueKind.Object)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return null;
        }

        var x = TryReadDouble(targetElement, "x", "X");
        var y = TryReadDouble(targetElement, "y", "Y");

        if (!x.HasValue || !y.HasValue)
        {
            return null;
        }

        return new WorldTargetDto
        {
            X = x.Value,
            Y = y.Value,
            Z = TryReadDouble(targetElement, "z", "Z") ?? 0.0,
            ToleranceMeters = TryReadDouble(targetElement, "toleranceMeters", "ToleranceMeters")
        };
    }

    private static List<WorldRoutePointDto> ReadWorldRoute(JsonElement root)
    {
        if (!root.TryGetProperty("route", out var routeElement) &&
            !root.TryGetProperty("Route", out routeElement))
        {
            return new List<WorldRoutePointDto>();
        }

        if (routeElement.ValueKind != JsonValueKind.Array)
        {
            return new List<WorldRoutePointDto>();
        }

        var route = new List<WorldRoutePointDto>();

        foreach (var item in routeElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetString(item, "id", "Id") ?? $"route-{route.Count + 1}";
            var x = TryReadDouble(item, "x", "X");
            var y = TryReadDouble(item, "y", "Y");

            if (!x.HasValue || !y.HasValue)
            {
                continue;
            }

            route.Add(new WorldRoutePointDto
            {
                Id = id,
                Label = GetString(item, "label", "Label"),
                ObjectiveId = GetString(item, "objectiveId", "ObjectiveId"),
                Index = (int)(TryReadDouble(item, "index", "Index") ?? route.Count),
                Type = GetString(item, "type", "Type") ?? "route-point",
                X = x.Value,
                Y = y.Value,
                Z = TryReadDouble(item, "z", "Z") ?? 0.0,
                ToleranceMeters = TryReadDouble(item, "toleranceMeters", "ToleranceMeters"),
                Active = TryReadBool(item, "active", "Active", "isActive", "IsActive") ?? false,
                Completed = TryReadBool(item, "completed", "Completed", "isCompleted", "IsCompleted") ?? false
            });
        }

        return route
            .OrderBy(x => x.Index)
            .ToList();
    }

    private static List<WorldObjectDto> ReadWorldObjects(JsonElement root)
    {
        if (!root.TryGetProperty("objects", out var objectsElement) &&
            !root.TryGetProperty("Objects", out objectsElement))
        {
            return new List<WorldObjectDto>();
        }

        if (objectsElement.ValueKind != JsonValueKind.Array)
        {
            return new List<WorldObjectDto>();
        }

        var objects = new List<WorldObjectDto>();

        foreach (var item in objectsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetString(item, "id", "Id") ?? $"object-{objects.Count + 1}";
            var x = TryReadDouble(item, "x", "X");
            var y = TryReadDouble(item, "y", "Y");

            if (!x.HasValue || !y.HasValue)
            {
                continue;
            }

            var worldObject = new WorldObjectDto
            {
                Id = id,
                Type = GetString(item, "type", "Type") ?? "object",
                Label = GetString(item, "label", "Label"),
                ObjectiveId = GetString(item, "objectiveId", "ObjectiveId"),
                Side = GetString(item, "side", "Side"),
                X = x.Value,
                Y = y.Value,
                Z = TryReadDouble(item, "z", "Z") ?? 0.0,
                Radius = TryReadDouble(item, "radius", "Radius") ?? 0.5,
                Color = GetString(item, "color", "Color"),
                Active = TryReadBool(item, "active", "Active", "isActive", "IsActive") ?? false,
                Completed = TryReadBool(item, "completed", "Completed", "isCompleted", "IsCompleted") ?? false
            };

            if (!string.IsNullOrWhiteSpace(worldObject.Type))
            {
                worldObject.Fields["type"] = worldObject.Type;
            }

            if (!string.IsNullOrWhiteSpace(worldObject.Label))
            {
                worldObject.Fields["label"] = worldObject.Label;
            }

            if (!string.IsNullOrWhiteSpace(worldObject.ObjectiveId))
            {
                worldObject.Fields["objectiveId"] = worldObject.ObjectiveId;
            }

            if (!string.IsNullOrWhiteSpace(worldObject.Side))
            {
                worldObject.Fields["side"] = worldObject.Side;
            }

            objects.Add(worldObject);
        }

        return objects;
    }
}