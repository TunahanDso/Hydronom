using System.Text.Json;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;

namespace HydronomOps.Gateway.Services.Mapping;

public sealed partial class RuntimeToGatewayMapper
{
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
            RemainingDistanceMeters = GetNullableDouble(
                root,
                "remainingDistanceMeters",
                "RemainingDistanceMeters",
                "distanceToGoalM",
                "DistanceToGoalM"),
            StartedAtUtc = GetNullableDateTime(root, "startedAtUtc", "StartedAtUtc"),
            FinishedAtUtc = GetNullableDateTime(root, "finishedAtUtc", "FinishedAtUtc"),
            Freshness = BuildFreshness(timestampUtc, "runtime")
        };
    }

    public SensorStateDto MapSensorState(JsonElement root)
    {
        var now = _clock.UtcNow;
        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc", "lastSampleUtc", "LastSampleUtc") ??
            now;

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
}