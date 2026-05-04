using System.Globalization;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Services.Mapping;

public sealed partial class RuntimeToGatewayMapper
{
    private static List<ObstacleDto> ExtractRuntimeObstacles(JsonElement root)
    {
        var result = new List<ObstacleDto>();

        if (!TryGetProperty(root, out var inputs, "inputs", "Inputs") || inputs.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var input in inputs.EnumerateArray())
        {
            var sourceName =
                GetString(input, "_source", "source", "Source") ??
                GetString(input, "name", "Name");

            if (!string.Equals(sourceName, "runtime_obstacles", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sourceName, "lidar_runtime_obstacles", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetProperty(input, out var data, "data", "Data") ||
                !TryGetProperty(data, out var obstacles, "obstacles", "Obstacles") ||
                obstacles.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var obs in obstacles.EnumerateArray())
            {
                result.Add(new ObstacleDto
                {
                    X = GetDouble(obs, "x", "X"),
                    Y = GetDouble(obs, "y", "Y"),
                    R = GetDouble(obs, "r", "R", "radius", "Radius", "radiusM", "RadiusM")
                });
            }

            break;
        }

        return result;
    }

    private static List<LandmarkDto> ExtractLandmarks(JsonElement root)
    {
        var result = new List<LandmarkDto>();

        if (!TryGetProperty(root, out var landmarks, "landmarks", "Landmarks") ||
            landmarks.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var landmark in landmarks.EnumerateArray())
        {
            var dto = new LandmarkDto
            {
                Id = GetString(landmark, "id", "Id") ?? string.Empty,
                Type = GetString(landmark, "type", "Type") ?? string.Empty,
                Shape = GetString(landmark, "shape", "Shape") ?? string.Empty
            };

            if (TryGetProperty(landmark, out var points, "points", "Points") &&
                points.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in points.EnumerateArray())
                {
                    if (TryParsePoint(point, out var px, out var py))
                    {
                        dto.Points.Add(new ObstaclePointDto
                        {
                            X = px,
                            Y = py
                        });
                    }
                }
            }

            if (TryGetProperty(landmark, out var style, "style", "Style") &&
                style.ValueKind == JsonValueKind.Object)
            {
                dto.Style = ParseLandmarkStyle(style);
            }

            ExtractLandmarkExtraFields(landmark, dto);

            result.Add(dto);
        }

        return result;
    }

    private static LandmarkStyleDto ParseLandmarkStyle(JsonElement style)
    {
        var dto = new LandmarkStyleDto
        {
            Color = GetString(style, "color", "Color"),
            Width = GetNullableDouble(style, "width", "Width"),
            Radius = GetNullableDouble(style, "radius", "Radius"),
            Label = GetString(style, "label", "Label")
        };

        foreach (var prop in style.EnumerateObject())
        {
            if (prop.NameEquals("color") ||
                prop.NameEquals("Color") ||
                prop.NameEquals("width") ||
                prop.NameEquals("Width") ||
                prop.NameEquals("radius") ||
                prop.NameEquals("Radius") ||
                prop.NameEquals("label") ||
                prop.NameEquals("Label"))
            {
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var s = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    dto.Fields[prop.Name] = s!;
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number ||
                     prop.Value.ValueKind == JsonValueKind.True ||
                     prop.Value.ValueKind == JsonValueKind.False)
            {
                dto.Fields[prop.Name] = prop.Value.ToString();
            }
        }

        return dto;
    }

    private static void ExtractLandmarkExtraFields(JsonElement landmark, LandmarkDto dto)
    {
        foreach (var prop in landmark.EnumerateObject())
        {
            if (prop.NameEquals("id") ||
                prop.NameEquals("Id") ||
                prop.NameEquals("type") ||
                prop.NameEquals("Type") ||
                prop.NameEquals("shape") ||
                prop.NameEquals("Shape") ||
                prop.NameEquals("points") ||
                prop.NameEquals("Points") ||
                prop.NameEquals("style") ||
                prop.NameEquals("Style"))
            {
                continue;
            }

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (prop.Value.TryGetDouble(out var d))
                    {
                        dto.Metrics[prop.Name] = d;
                    }
                    break;

                case JsonValueKind.String:
                    var s = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        if (double.TryParse(
                            s,
                            NumberStyles.Float | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                        {
                            dto.Metrics[prop.Name] = parsed;
                        }
                        else
                        {
                            dto.Fields[prop.Name] = s!;
                        }
                    }
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    dto.Fields[prop.Name] = prop.Value.GetBoolean() ? "true" : "false";
                    break;

                case JsonValueKind.Object:
                    FlattenJsonToTelemetry(prop.Value, $"landmark.{dto.Id}.{prop.Name}", dto.Metrics, dto.Fields);
                    break;

                case JsonValueKind.Array:
                    FlattenJsonToTelemetry(prop.Value, $"landmark.{dto.Id}.{prop.Name}", dto.Metrics, dto.Fields);
                    break;
            }
        }
    }

    private static void ExtractInputTelemetry(
        JsonElement root,
        Dictionary<string, double> metrics,
        Dictionary<string, string> fields)
    {
        if (!TryGetProperty(root, out var inputs, "inputs", "Inputs") || inputs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var input in inputs.EnumerateArray())
        {
            var sourceName =
                GetString(input, "_source", "source", "Source") ??
                GetString(input, "name", "Name") ??
                "input";

            if (TryGetProperty(input, out var data, "data", "Data"))
            {
                FlattenJsonToTelemetry(data, sourceName, metrics, fields);
            }
            else
            {
                FlattenJsonToTelemetry(input, sourceName, metrics, fields);
            }
        }
    }

    private static void FlattenJsonToTelemetry(
        JsonElement element,
        string prefix,
        Dictionary<string, double> metrics,
        Dictionary<string, string> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? prop.Name
                        : $"{prefix}.{prop.Name}";

                    FlattenJsonToTelemetry(prop.Value, childPrefix, metrics, fields);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FlattenJsonToTelemetry(item, $"{prefix}[{index}]", metrics, fields);
                    }

                    index++;
                }
                break;

            case JsonValueKind.Number:
                if (element.TryGetDouble(out var d))
                {
                    metrics[prefix] = d;
                }
                break;

            case JsonValueKind.String:
                var s = element.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (double.TryParse(
                        s,
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                    {
                        metrics[prefix] = parsed;
                    }
                    else
                    {
                        fields[prefix] = s!;
                    }
                }
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                fields[prefix] = element.GetBoolean() ? "true" : "false";
                break;
        }
    }

    private static bool TryParsePoint(JsonElement point, out double x, out double y)
    {
        x = 0.0;
        y = 0.0;

        if (point.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in point.EnumerateArray())
            {
                if (index == 0 && item.ValueKind == JsonValueKind.Number)
                {
                    item.TryGetDouble(out x);
                }
                else if (index == 1 && item.ValueKind == JsonValueKind.Number)
                {
                    item.TryGetDouble(out y);
                    return true;
                }

                index++;
            }

            return false;
        }

        if (point.ValueKind == JsonValueKind.Object)
        {
            var px = GetNullableDouble(point, "x", "X");
            var py = GetNullableDouble(point, "y", "Y");

            if (px.HasValue && py.HasValue)
            {
                x = px.Value;
                y = py.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRuntimeObstacleStats(
        JsonElement root,
        double vehicleX,
        double vehicleY,
        double headingDeg,
        out bool obstacleAhead,
        out int obstacleCount)
    {
        obstacleAhead = false;
        obstacleCount = 0;

        if (!TryGetProperty(root, out var inputs, "inputs", "Inputs") || inputs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        const double aheadDistanceM = 12.0;
        const double halfFovDeg = 45.0;

        foreach (var input in inputs.EnumerateArray())
        {
            var sourceName =
                GetString(input, "_source", "source", "Source") ??
                GetString(input, "name", "Name");

            if (!string.Equals(sourceName, "runtime_obstacles", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sourceName, "lidar_runtime_obstacles", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetProperty(input, out var data, "data", "Data") ||
                !TryGetProperty(data, out var obstacles, "obstacles", "Obstacles") ||
                obstacles.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var headingRad = DegreesToRadians(headingDeg);
            var fwdX = Math.Cos(headingRad);
            var fwdY = Math.Sin(headingRad);

            foreach (var obs in obstacles.EnumerateArray())
            {
                var ox = GetDouble(obs, "x", "X");
                var oy = GetDouble(obs, "y", "Y");
                var r = GetDouble(obs, "r", "R", "radius", "Radius", "radiusM", "RadiusM");

                obstacleCount++;

                var dx = ox - vehicleX;
                var dy = oy - vehicleY;
                var centerDist = Math.Sqrt(dx * dx + dy * dy);

                if (centerDist <= 1e-9)
                {
                    obstacleAhead = true;
                    continue;
                }

                var cos = Math.Clamp((dx * fwdX + dy * fwdY) / centerDist, -1.0, 1.0);
                var angDeg = Math.Acos(cos) * 180.0 / Math.PI;

                var inCone = angDeg <= halfFovDeg;
                var inRange = centerDist <= (aheadDistanceM + r);

                if (inCone && inRange)
                {
                    obstacleAhead = true;
                }
            }

            return true;
        }

        return false;
    }
}