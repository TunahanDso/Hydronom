using System.Globalization;
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

        CopyRootField(root, worldState.Fields, "scenarioFamily", "ScenarioFamily");
        CopyRootField(root, worldState.Fields, "coordinateFrame", "CoordinateFrame");
        CopyRootField(root, worldState.Fields, "vehiclePlatform", "VehiclePlatform");
        CopyRootField(root, worldState.Fields, "runMode", "RunMode");

        if (!string.IsNullOrWhiteSpace(worldState.ScenarioId))
            worldState.Fields["scenarioId"] = worldState.ScenarioId;

        if (!string.IsNullOrWhiteSpace(worldState.CurrentObjectiveId))
            worldState.Fields["currentObjectiveId"] = worldState.CurrentObjectiveId;

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
            return null;

        var x = TryReadDouble(targetElement, "x", "X");
        var y = TryReadDouble(targetElement, "y", "Y");

        if (!x.HasValue || !y.HasValue)
            return null;

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
            return new List<WorldRoutePointDto>();

        var route = new List<WorldRoutePointDto>();

        foreach (var item in routeElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var id = GetString(item, "id", "Id") ?? $"route-{route.Count + 1}";
            var x = TryReadDouble(item, "x", "X");
            var y = TryReadDouble(item, "y", "Y");

            if (!x.HasValue || !y.HasValue)
                continue;

            route.Add(new WorldRoutePointDto
            {
                Id = id,
                Label = GetString(item, "label", "Label"),
                ObjectiveId = GetString(item, "objectiveId", "ObjectiveId"),
                Index = (int)(TryReadDouble(item, "index", "Index") ?? route.Count),
                Type = GetString(item, "type", "Type", "kind", "Kind") ?? "route-point",
                X = x.Value,
                Y = y.Value,
                Z = TryReadDouble(item, "z", "Z") ?? 0.0,
                ToleranceMeters = TryReadDouble(item, "toleranceMeters", "ToleranceMeters"),
                Active = TryReadBool(item, "active", "Active", "isActive", "IsActive") ?? false,
                Completed = TryReadBool(item, "completed", "Completed", "isCompleted", "IsCompleted") ?? false
            });
        }

        return route.OrderBy(x => x.Index).ToList();
    }

    private static List<WorldObjectDto> ReadWorldObjects(JsonElement root)
    {
        if (!root.TryGetProperty("objects", out var objectsElement) &&
            !root.TryGetProperty("Objects", out objectsElement))
        {
            return new List<WorldObjectDto>();
        }

        if (objectsElement.ValueKind != JsonValueKind.Array)
            return new List<WorldObjectDto>();

        var objects = new List<WorldObjectDto>();

        foreach (var item in objectsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var id = GetString(item, "id", "Id") ?? $"object-{objects.Count + 1}";
            var x = TryReadDouble(item, "x", "X");
            var y = TryReadDouble(item, "y", "Y");

            if (!x.HasValue || !y.HasValue)
                continue;

            var kind =
                GetString(item, "kind", "Kind") ??
                GetString(item, "type", "Type") ??
                "object";

            var role = GetString(item, "role", "Role");
            var layer = GetString(item, "layer", "Layer");
            var name = GetString(item, "name", "Name");

            var radius =
                TryReadDouble(item, "radius", "Radius", "r", "R", "radiusM", "RadiusM") ??
                InferRadiusFromObjectKind(kind);

            var worldObject = new WorldObjectDto
            {
                Id = id,
                Type = kind,
                Label = GetString(item, "label", "Label") ?? name ?? id,
                ObjectiveId = GetString(item, "objectiveId", "ObjectiveId"),
                Side = GetString(item, "side", "Side"),
                X = x.Value,
                Y = y.Value,
                Z = TryReadDouble(item, "z", "Z") ?? 0.0,
                Radius = radius,
                Color = GetString(item, "color", "Color"),
                Active = TryReadBool(item, "active", "Active", "isActive", "IsActive") ?? true,
                Completed = TryReadBool(item, "completed", "Completed", "isCompleted", "IsCompleted") ?? false
            };

            PutField(worldObject.Fields, "type", worldObject.Type);
            PutField(worldObject.Fields, "kind", kind);
            PutField(worldObject.Fields, "role", role);
            PutField(worldObject.Fields, "layer", layer);
            PutField(worldObject.Fields, "name", name);
            PutField(worldObject.Fields, "label", worldObject.Label);
            PutField(worldObject.Fields, "objectiveId", worldObject.ObjectiveId);
            PutField(worldObject.Fields, "side", worldObject.Side);

            CopyMetric(item, worldObject.Metrics, "length", "Length", "lengthM", "LengthM");
            CopyMetric(item, worldObject.Metrics, "width", "Width", "widthM", "WidthM");
            CopyMetric(item, worldObject.Metrics, "height", "Height", "heightM", "HeightM");
            CopyMetric(item, worldObject.Metrics, "diameter", "Diameter", "diameterM", "DiameterM");
            CopyMetric(item, worldObject.Metrics, "radius", "Radius", "radiusM", "RadiusM");
            CopyMetric(item, worldObject.Metrics, "yawDeg", "YawDeg");
            CopyMetric(item, worldObject.Metrics, "toleranceMeters", "ToleranceMeters");
            CopyMetric(item, worldObject.Metrics, "scoreValue", "ScoreValue");

            if (TryReadDouble(item, "length", "Length", "lengthM", "LengthM") is { } length)
                worldObject.Metrics["length"] = length;

            if (TryReadDouble(item, "width", "Width", "widthM", "WidthM") is { } width)
                worldObject.Metrics["width"] = width;

            if (TryReadDouble(item, "height", "Height", "heightM", "HeightM") is { } height)
                worldObject.Metrics["height"] = height;

            if (TryReadDouble(item, "diameter", "Diameter", "diameterM", "DiameterM") is { } diameter)
                worldObject.Metrics["diameter"] = diameter;

            if (TryReadDouble(item, "yawDeg", "YawDeg") is { } yawDeg)
                worldObject.Metrics["yawDeg"] = yawDeg;

            if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                CopyTags(tags, worldObject.Fields, worldObject.Metrics);

            objects.Add(worldObject);
        }

        return objects;
    }

    private static void CopyTags(
        JsonElement tags,
        Dictionary<string, string> fields,
        Dictionary<string, double> metrics)
    {
        foreach (var prop in tags.EnumerateObject())
        {
            var key = $"tag.{prop.Name}";

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    var text = prop.Value.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        break;

                    fields[key] = text!;

                    if (double.TryParse(
                            text,
                            NumberStyles.Float | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        metrics[key] = parsed;
                    }

                    break;

                case JsonValueKind.Number:
                    if (prop.Value.TryGetDouble(out var number))
                    {
                        metrics[key] = number;
                        fields[key] = number.ToString(CultureInfo.InvariantCulture);
                    }
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    fields[key] = prop.Value.GetBoolean() ? "true" : "false";
                    break;

                default:
                    fields[key] = prop.Value.ToString();
                    break;
            }
        }
    }

    private static void CopyMetric(
        JsonElement item,
        Dictionary<string, double> metrics,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var number))
            {
                metrics[NormalizeMetricName(name)] = number;
                return;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    prop.GetString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                metrics[NormalizeMetricName(name)] = parsed;
                return;
            }
        }
    }

    private static void CopyRootField(
        JsonElement root,
        Dictionary<string, string> fields,
        params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(root, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[name] = value!;
                return;
            }
        }
    }

    private static string NormalizeMetricName(string name)
    {
        return name switch
        {
            "Length" or "lengthM" or "LengthM" => "length",
            "Width" or "widthM" or "WidthM" => "width",
            "Height" or "heightM" or "HeightM" => "height",
            "Diameter" or "diameterM" or "DiameterM" => "diameter",
            "Radius" or "radiusM" or "RadiusM" => "radius",
            "YawDeg" => "yawDeg",
            "ToleranceMeters" => "toleranceMeters",
            "ScoreValue" => "scoreValue",
            _ => name
        };
    }

    private static void PutField(Dictionary<string, string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            fields[key] = value!;
    }

    private static double InferRadiusFromObjectKind(string kind)
    {
        return kind switch
        {
            "tracking_stripe" => 0.05,
            "guidance_path_segment" => 0.15,
            "pipe" => 0.30,
            "pipe_gate" => 0.30,
            "release_zone" => 0.60,
            "hint_marker" => 0.15,
            "pipe_checkpoint" => 0.20,
            "waypoint" => 0.35,
            "start_zone" => 0.75,
            _ => 0.5
        };
    }
}