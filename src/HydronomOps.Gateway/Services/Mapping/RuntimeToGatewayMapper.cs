using System.Globalization;
using System.Text.Json;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Common;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Infrastructure.Time;

namespace HydronomOps.Gateway.Services.Mapping;

/// <summary>
/// Runtime'tan gelen ham JSON verisini gateway DTO'larÄ±na dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
/// </summary>
public sealed class RuntimeToGatewayMapper
{
    private readonly ISystemClock _clock;

    public RuntimeToGatewayMapper(ISystemClock clock)
    {
        _clock = clock;
    }

    public VehicleTelemetryDto MapVehicleTelemetry(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc", "t") ??
            GetNullableDateTime(root, "t_imu", "t_gps") ??
            now;

        var vehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main";

        // ---------- top-level defaults ----------
        var x = GetDouble(root, "x", "X");
        var y = GetDouble(root, "y", "Y");
        var z = GetDouble(root, "z", "Z");

        var rollDeg = GetDouble(root, "rollDeg", "RollDeg", "roll_deg");
        var pitchDeg = GetDouble(root, "pitchDeg", "PitchDeg", "pitch_deg");
        var yawDeg = GetDouble(root, "yawDeg", "YawDeg", "yaw_deg");
        var headingDeg = GetDouble(root, "headingDeg", "HeadingDeg", "heading_deg", "yawDeg", "YawDeg", "yaw_deg");

        var vx = GetDouble(root, "vx", "Vx");
        var vy = GetDouble(root, "vy", "Vy");
        var vz = GetDouble(root, "vz", "Vz");

        var rollRateDeg = GetDouble(root, "rollRateDeg", "RollRateDeg", "roll_rate_deg");
        var pitchRateDeg = GetDouble(root, "pitchRateDeg", "PitchRateDeg", "pitch_rate_deg");
        var yawRateDeg = GetDouble(root, "yawRateDeg", "YawRateDeg", "yaw_rate_deg");

        var targetX = GetNullableDouble(root, "targetX", "TargetX");
        var targetY = GetNullableDouble(root, "targetY", "TargetY");

        var distanceToGoalM = GetNullableDouble(root, "distanceToGoalM", "DistanceToGoalM");
        var headingErrorDeg = GetNullableDouble(root, "headingErrorDeg", "HeadingErrorDeg");

        var obstacleAhead = GetBool(root, "obstacleAhead", "ObstacleAhead");
        var obstacleCount = GetInt(root, 0, "obstacleCount", "ObstacleCount");

        // ---------- FusedState.pose ----------
        if (TryGetProperty(root, out var pose, "pose", "Pose"))
        {
            x = GetDouble(pose, "x", "X");
            y = GetDouble(pose, "y", "Y");
            z = GetDouble(pose, "z", "Z");

            rollDeg = GetDouble(pose, "roll", "Roll", "rollDeg", "RollDeg", "roll_deg");
            pitchDeg = GetDouble(pose, "pitch", "Pitch", "pitchDeg", "PitchDeg", "pitch_deg");

            var poseYaw = GetNullableDouble(pose, "yaw", "Yaw", "yawDeg", "YawDeg", "yaw_deg");
            if (poseYaw.HasValue)
            {
                yawDeg = poseYaw.Value;
                headingDeg = poseYaw.Value;
            }
        }

        // ---------- FusedState.twist ----------
        if (TryGetProperty(root, out var twist, "twist", "Twist"))
        {
            vx = GetDouble(twist, "vx", "Vx");
            vy = GetDouble(twist, "vy", "Vy");
            vz = GetDouble(twist, "vz", "Vz");

            rollRateDeg = GetDouble(twist, "roll_rate", "Roll_Rate", "rollRateDeg", "RollRateDeg");
            pitchRateDeg = GetDouble(twist, "pitch_rate", "Pitch_Rate", "pitchRateDeg", "PitchRateDeg");

            var twistYawRate = GetNullableDouble(
                twist,
                "yaw_rate", "Yaw_Rate",
                "yawRate", "YawRate",
                "yawRateDeg", "YawRateDeg");

            if (twistYawRate.HasValue)
            {
                yawRateDeg = twistYawRate.Value;
            }
        }

        // ---------- target / goal ----------
        if (TryGetProperty(root, out var target, "target", "Target"))
        {
            targetX = GetNullableDouble(target, "x", "X");
            targetY = GetNullableDouble(target, "y", "Y");
        }
        else if (TryGetProperty(root, out var goal, "goal", "Goal"))
        {
            targetX = GetNullableDouble(goal, "x", "X");
            targetY = GetNullableDouble(goal, "y", "Y");
        }

        if (targetX.HasValue && targetY.HasValue)
        {
            var dx = targetX.Value - x;
            var dy = targetY.Value - y;
            distanceToGoalM = Math.Sqrt(dx * dx + dy * dy);

            var targetHeadingDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            headingErrorDeg = NormalizeAngleDeg(targetHeadingDeg - headingDeg);
        }

        // ---------- geometry extraction ----------
        var obstacles = ExtractRuntimeObstacles(root);
        var landmarks = ExtractLandmarks(root);

        // ---------- obstacle fallback from inputs.runtime_obstacles ----------
        if ((!obstacleAhead || obstacleCount == 0) &&
            TryGetRuntimeObstacleStats(root, x, y, headingDeg, out var computedAhead, out var computedCount))
        {
            obstacleAhead = computedAhead;
            obstacleCount = computedCount;
        }

        if (obstacleCount == 0 && obstacles.Count > 0)
        {
            obstacleCount = obstacles.Count;
        }

        // ---------- flattened telemetry/debug inputs ----------
        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ExtractInputTelemetry(root, metrics, fields);

        // ---------- occupancy / mapping normalize ----------
        NormalizeOccupancyTelemetry(metrics, fields, landmarks);

        // KullanÄ±ÅŸlÄ± bazÄ± Ã¼st seviye alanlarÄ± da ayrÄ±ca ekleyelim
        fields["mapper.vehicleId"] = vehicleId;
        fields["mapper.timestampUtc"] = timestampUtc.ToString("O", CultureInfo.InvariantCulture);

        metrics["vehicle.x"] = x;
        metrics["vehicle.y"] = y;
        metrics["vehicle.z"] = z;
        metrics["vehicle.headingDeg"] = headingDeg;
        metrics["vehicle.vx"] = vx;
        metrics["vehicle.vy"] = vy;
        metrics["vehicle.vz"] = vz;
        metrics["vehicle.obstacleCount"] = obstacleCount;

        if (distanceToGoalM.HasValue)
        {
            metrics["mission.distanceToGoalM"] = distanceToGoalM.Value;
        }

        if (headingErrorDeg.HasValue)
        {
            metrics["mission.headingErrorDeg"] = headingErrorDeg.Value;
        }

        return new VehicleTelemetryDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = vehicleId,
            X = x,
            Y = y,
            Z = z,
            RollDeg = rollDeg,
            PitchDeg = pitchDeg,
            YawDeg = yawDeg,
            HeadingDeg = headingDeg,
            Vx = vx,
            Vy = vy,
            Vz = vz,
            RollRateDeg = rollRateDeg,
            PitchRateDeg = pitchRateDeg,
            YawRateDeg = yawRateDeg,
            TargetX = targetX,
            TargetY = targetY,
            DistanceToGoalM = distanceToGoalM,
            HeadingErrorDeg = headingErrorDeg,
            ObstacleAhead = obstacleAhead,
            ObstacleCount = obstacleCount,
            Obstacles = obstacles,
            Landmarks = landmarks,
            Metrics = metrics,
            Fields = fields,
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public VehicleTelemetryDto MapVehicleTelemetryFromExternalState(JsonElement root)
    {
        var dto = MapVehicleTelemetry(root);

        dto.Fields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        dto.Fields["origin"] = "external-state";

        return dto;
    }

    public MissionStateDto MapMissionState(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ?? now;

        return new MissionStateDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            MissionId = GetString(root, "missionId", "MissionId"),
            MissionName = GetString(root, "missionName", "MissionName"),
            Status = GetString(root, "status", "Status") ?? "idle",
            CurrentStepIndex = GetInt(root, 0, "currentStepIndex", "CurrentStepIndex"),
            TotalStepCount = GetInt(root, 0, "totalStepCount", "TotalStepCount"),
            CurrentStepTitle = GetString(root, "currentStepTitle", "CurrentStepTitle"),
            NextObjective = GetString(root, "nextObjective", "NextObjective"),
            RemainingDistanceMeters = GetNullableDouble(root, "remainingDistanceMeters", "RemainingDistanceMeters", "distanceToGoalM", "DistanceToGoalM"),
            StartedAtUtc = GetNullableDateTime(root, "startedAtUtc", "StartedAtUtc"),
            FinishedAtUtc = GetNullableDateTime(root, "finishedAtUtc", "FinishedAtUtc"),
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public SensorStateDto MapSensorState(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc", "lastSampleUtc", "LastSampleUtc") ?? now;

        return new SensorStateDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            SensorName = GetString(root, "sensorName", "SensorName", "name", "Name") ?? "sensor",
            SensorType = GetString(root, "sensorType", "SensorType", "type", "Type") ?? "unknown",
            Source = GetString(root, "source", "Source"),
            Backend = GetString(root, "backend", "Backend"),
            IsSimulated = GetBool(root, "isSimulated", "IsSimulated"),
            IsEnabled = GetBool(root, true, "isEnabled", "IsEnabled"),
            IsHealthy = GetBool(root, true, "isHealthy", "IsHealthy"),
            ConfiguredRateHz = GetNullableDouble(root, "configuredRateHz", "ConfiguredRateHz"),
            EffectiveRateHz = GetNullableDouble(root, "effectiveRateHz", "EffectiveRateHz"),
            LastSampleUtc = GetNullableDateTime(root, "lastSampleUtc", "LastSampleUtc", "timestampUtc", "TimestampUtc"),
            LastError = GetString(root, "lastError", "LastError"),
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public ActuatorStateDto MapActuatorState(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ?? now;

        return new ActuatorStateDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public DiagnosticsStateDto MapDiagnosticsState(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ?? now;

        return new DiagnosticsStateDto
        {
            TimestampUtc = timestampUtc,
            GatewayStatus = GetString(root, "gatewayStatus", "GatewayStatus") ?? "running",
            RuntimeConnected = GetBool(root, true, "runtimeConnected", "RuntimeConnected"),
            HasWebSocketClients = GetBool(root, "hasWebSocketClients", "HasWebSocketClients"),
            ConnectedWebSocketClients = GetInt(root, 0, "connectedWebSocketClients", "ConnectedWebSocketClients"),
            LastRuntimeMessageUtc = GetNullableDateTime(root, "lastRuntimeMessageUtc", "LastRuntimeMessageUtc"),
            RuntimeFreshness = BuildFreshness(timestampUtc, "runtime"),
            LastError = GetString(root, "lastError", "LastError"),
            LastErrorUtc = GetNullableDateTime(root, "lastErrorUtc", "LastErrorUtc"),
            IngressMessageCount = GetLong(root, 0, "ingressMessageCount", "IngressMessageCount"),
            BroadcastMessageCount = GetLong(root, 0, "broadcastMessageCount", "BroadcastMessageCount")
        };
    }

    public DiagnosticsStateDto MapDiagnosticsStateFromHealth(JsonElement root)
    {
        return MapDiagnosticsState(root);
    }

    public GatewayLogDto MapGatewayLogFromEvent(JsonElement root)
    {
        return new GatewayLogDto
        {
            TimestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ?? _clock.UtcNow,
            Level = GetString(root, "level", "Level") ?? "Info",
            Category = GetString(root, "category", "Category") ?? "runtime-event",
            Message = GetString(root, "message", "Message") ?? "Runtime event received.",
            Detail = root.ToString()
        };
    }

    public GatewayLogDto MapGatewayLogFromCapability(JsonElement root)
    {
        return new GatewayLogDto
        {
            TimestampUtc = GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ?? _clock.UtcNow,
            Level = "Info",
            Category = "capability",
            Message = "Runtime capability message received.",
            Detail = root.ToString()
        };
    }

    private FreshnessDto BuildFreshness(DateTime timestampUtc, string source)
    {
        var now = _clock.UtcNow;
        var ageMs = Math.Max(0, (long)(now - timestampUtc).TotalMilliseconds);
        const int thresholdMs = 5000;

        return new FreshnessDto
        {
            TimestampUtc = timestampUtc,
            AgeMs = ageMs,
            IsFresh = ageMs <= thresholdMs,
            ThresholdMs = thresholdMs,
            Source = source
        };
    }

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

    private static void NormalizeOccupancyTelemetry(
        Dictionary<string, double> metrics,
        Dictionary<string, string> fields,
        List<LandmarkDto> landmarks)
    {
        var gridWidth =
            GetMetricFirst(metrics,
                "occupancy.width",
                "occupancy_grid.width",
                "occupancy_grid.grid_width",
                "occupancy_grid.cells_w",
                "occupancy_grid.cellsW");

        var gridHeight =
            GetMetricFirst(metrics,
                "occupancy.height",
                "occupancy_grid.height",
                "occupancy_grid.grid_height",
                "occupancy_grid.cells_h",
                "occupancy_grid.cellsH");

        var previewCount =
            GetMetricFirst(metrics,
                "occupancy.previewCount",
                "occupancy_grid.preview_count",
                "occupancy_grid.preview_points",
                "occupancy_grid.previewPoints");

        var exportCount =
            GetMetricFirst(metrics,
                "occupancy.exportCount",
                "occupancy_grid.export_count",
                "occupancy_grid.export_points",
                "occupancy_grid.exportPoints");

        var occupiedCount =
            GetMetricFirst(metrics,
                "occupancy.occupiedCount",
                "occupancy_grid.occupied_count",
                "occupancy_grid.occupied_cells",
                "occupancy_grid.occupiedCells");

        var resolutionM =
            GetMetricFirst(metrics,
                "occupancy.resolutionM",
                "occupancy_grid.resolution",
                "occupancy_grid.resolution_m",
                "occupancy_grid.resolutionM");

        var scanCount =
            GetMetricFirst(metrics,
                "occupancy.scanCount",
                "occupancy_grid.scan_count",
                "occupancy_grid.scanCount");

        var previewLandmark = landmarks.FirstOrDefault(l =>
            string.Equals(l.Type, "occupancy_preview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Id, "ogm_preview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Id, "occ_poly", StringComparison.OrdinalIgnoreCase));

        var cellsLandmark = landmarks.FirstOrDefault(l =>
            string.Equals(l.Type, "occupancy_cells", StringComparison.OrdinalIgnoreCase) ||
            l.Id.EndsWith("_cells", StringComparison.OrdinalIgnoreCase));

        if ((!previewCount.HasValue || previewCount.Value <= 0) && previewLandmark is not null)
        {
            previewCount = previewLandmark.Points.Count;
        }

        if ((!exportCount.HasValue || exportCount.Value <= 0) && cellsLandmark is not null)
        {
            exportCount = cellsLandmark.Points.Count;
        }

        if ((!occupiedCount.HasValue || occupiedCount.Value <= 0))
        {
            if (exportCount.HasValue && exportCount.Value > 0)
            {
                occupiedCount = exportCount.Value;
            }
            else if (cellsLandmark is not null)
            {
                occupiedCount = cellsLandmark.Points.Count;
            }
        }

        if (gridWidth.HasValue)
        {
            metrics["occupancy.width"] = gridWidth.Value;
            metrics["occupancy.gridWidth"] = gridWidth.Value;
            metrics["occupancy_grid.width"] = gridWidth.Value;
        }

        if (gridHeight.HasValue)
        {
            metrics["occupancy.height"] = gridHeight.Value;
            metrics["occupancy.gridHeight"] = gridHeight.Value;
            metrics["occupancy_grid.height"] = gridHeight.Value;
        }

        if (previewCount.HasValue)
        {
            metrics["occupancy.previewCount"] = previewCount.Value;
            metrics["occupancy_grid.preview_count"] = previewCount.Value;
        }

        if (exportCount.HasValue)
        {
            metrics["occupancy.exportCount"] = exportCount.Value;
            metrics["occupancy_grid.export_count"] = exportCount.Value;
        }

        if (occupiedCount.HasValue)
        {
            metrics["occupancy.occupiedCount"] = occupiedCount.Value;
            metrics["occupancy_grid.occupied_count"] = occupiedCount.Value;
        }

        if (resolutionM.HasValue)
        {
            metrics["occupancy.resolutionM"] = resolutionM.Value;
            metrics["occupancy_grid.resolution_m"] = resolutionM.Value;
        }

        if (scanCount.HasValue)
        {
            metrics["occupancy.scanCount"] = scanCount.Value;
            metrics["occupancy_grid.scan_count"] = scanCount.Value;
        }

        if (gridWidth.HasValue && gridHeight.HasValue)
        {
            fields["occupancy.gridSize"] =
                $"{Convert.ToInt32(Math.Round(gridWidth.Value, MidpointRounding.AwayFromZero))}x{Convert.ToInt32(Math.Round(gridHeight.Value, MidpointRounding.AwayFromZero))}";
        }

        if (previewLandmark is not null)
        {
            fields["occupancy.previewLandmarkId"] = previewLandmark.Id;
        }

        if (cellsLandmark is not null)
        {
            fields["occupancy.cellsLandmarkId"] = cellsLandmark.Id;
        }
    }

    private static double? GetMetricFirst(Dictionary<string, double> metrics, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metrics.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
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

    private static bool TryGetProperty(JsonElement root, out JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out element))
            {
                return true;
            }
        }

        element = default;
        return false;
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String)
            {
                return p.GetString();
            }

            if (p.ValueKind == JsonValueKind.Number ||
                p.ValueKind == JsonValueKind.True ||
                p.ValueKind == JsonValueKind.False)
            {
                return p.ToString();
            }
        }

        return null;
    }

    private static double GetDouble(JsonElement root, params string[] names)
    {
        return GetNullableDouble(root, names) ?? 0.0;
    }

    private static double? GetNullableDouble(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                double.TryParse(p.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int GetInt(JsonElement root, int fallback = 0, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static long GetLong(JsonElement root, long fallback = 0, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String &&
                long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static bool GetBool(JsonElement root, params string[] names)
    {
        return GetBool(root, false, names);
    }

    private static bool GetBool(JsonElement root, bool fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
            {
                return p.GetBoolean();
            }

            if (p.ValueKind == JsonValueKind.String &&
                bool.TryParse(p.GetString(), out var parsed))
            {
                return parsed;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var intValue))
            {
                return intValue != 0;
            }
        }

        return fallback;
    }

    private static DateTime? GetNullableDateTime(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(
                    p.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                return dt;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var epochSeconds))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSeconds * 1000)).UtcDateTime;
                }
                catch
                {
                    // Yut
                }
            }
        }

        return null;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double NormalizeAngleDeg(double deg)
    {
        while (deg > 180.0) deg -= 360.0;
        while (deg < -180.0) deg += 360.0;
        return deg;
    }
}
