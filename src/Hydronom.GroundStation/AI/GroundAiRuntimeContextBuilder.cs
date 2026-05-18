using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Hydronom.GroundStation.AI;

public sealed record GroundAiRuntimeContext(
    string RuntimeContext,
    string MissionState,
    string VehicleState,
    string SensorState,
    string ActuatorState,
    string SafetyState,
    string WorldState,
    string OperatorSummary
);

public static class GroundAiRuntimeContextBuilder
{
    public static GroundAiRuntimeContext FromGatewaySnapshotJson(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
            throw new ArgumentException("Gateway snapshot JSON boş olamaz.", nameof(snapshotJson));

        using var doc = JsonDocument.Parse(snapshotJson);
        var root = doc.RootElement;

        var runtimeConnected = GetBool(root, "runtimeConnected");
        var pythonConnected = GetBool(root, "pythonConnected");
        var wsClients = GetInt(root, "webSocketClientCount");
        var totalMessages = GetLong(root, "totalMessagesReceived");
        var lastError = GetString(root, "lastError") ?? "none";

        var diagnostics = TryGet(root, "diagnosticsState");
        var gatewayStatus = GetString(diagnostics, "gatewayStatus") ?? "unknown";
        var runtimeFresh = GetBool(TryGet(diagnostics, "runtimeFreshness"), "isFresh");

        var telemetry = TryGet(root, "vehicleTelemetry");
        var x = GetDouble(telemetry, "x");
        var y = GetDouble(telemetry, "y");
        var z = GetDouble(telemetry, "z");
        var yaw = GetDouble(telemetry, "yawDeg");
        var speed = GetMetric(telemetry, "vehicle.speed") ?? EstimateSpeed(telemetry);
        var distanceToGoal = GetDoubleNullable(telemetry, "distanceToGoalM");
        var headingError = GetDoubleNullable(telemetry, "headingErrorDeg");
        var obstacleAhead = GetBool(telemetry, "obstacleAhead");
        var obstacleCount = GetInt(telemetry, "obstacleCount");

        var mission = TryGet(root, "missionState");
        var missionId = GetString(mission, "missionId") ?? "unknown";
        var missionName = GetString(mission, "missionName") ?? "unknown";
        var missionStatus = GetString(mission, "status") ?? "unknown";
        var currentStep = GetInt(mission, "currentStepIndex");
        var totalSteps = GetInt(mission, "totalStepCount");
        var currentStepTitle = GetString(mission, "currentStepTitle") ?? "unknown";
        var nextObjective = GetString(mission, "nextObjective") ?? "unknown";
        var remainingDistance = GetDoubleNullable(mission, "remainingDistanceMeters");
        var missionWarnings = ReadArrayStrings(mission, "warnings", 4);

        var world = TryGet(root, "worldState");
        var scenarioId = GetString(world, "scenarioId") ?? "unknown";
        var scenarioName = GetString(world, "scenarioName") ?? "unknown";
        var currentObjective = GetString(world, "currentObjectiveId") ?? "unknown";
        var routeCount = GetMetric(world, "route.count");
        var objectCount = GetMetric(world, "objects.count");
        var activeTarget = TryGet(world, "activeObjectiveTarget");
        var targetX = GetDoubleNullable(activeTarget, "x");
        var targetY = GetDoubleNullable(activeTarget, "y");
        var targetTol = GetDoubleNullable(activeTarget, "toleranceMeters");

        var sensor = TryGet(root, "sensorState");
        var sensorsHealthy = GetBool(sensor, "isHealthy");
        var sensorCount = GetMetric(sensor, "runtime.sensorCount");
        var healthySensorCount = GetMetric(sensor, "runtime.healthySensorCount");
        var fusionConfidence = GetMetric(sensor, "runtime.fusionConfidence");
        var stateConfidence = GetMetric(sensor, "runtime.stateConfidence");
        var fusionName = GetField(sensor, "runtime.fusionEngineName") ?? "unknown";
        var sensorSummary = GetField(sensor, "runtime.summary") ?? "n/a";
        var sensorFresh = GetBool(TryGet(sensor, "freshness"), "isFresh");

        var actuator = TryGet(root, "actuatorState");
        var actuatorHealthy = GetBool(actuator, "isHealthy");
        var actuatorEnabled = GetBool(actuator, "isEnabled");
        var allocationReason = GetField(actuator, "allocation.reason") ?? "unknown";
        var allocationSuccess = GetField(actuator, "allocation.success") ?? "unknown";
        var activeThrusters = GetMetric(actuator, "allocation.activeThrusterCount");
        var healthyThrusters = GetMetric(actuator, "allocation.healthyThrusterCount");
        var reverseClamp = GetMetric(actuator, "allocation.reverseClampCount");
        var saturation = GetMetric(actuator, "allocation.saturationRatio");
        var forceX = GetMetric(actuator, "force.x");
        var forceY = GetMetric(actuator, "force.y");
        var forceZ = GetMetric(actuator, "force.z");

        var logs = ReadLogSummary(root, maxItems: 5);

        var runtimeContext = new StringBuilder()
            .AppendLine($"Gateway status: {gatewayStatus}")
            .AppendLine($"Runtime connected: {runtimeConnected}")
            .AppendLine($"Runtime freshness: {(runtimeFresh ? "fresh" : "stale")}")
            .AppendLine($"Python connected: {pythonConnected}")
            .AppendLine($"WebSocket clients: {wsClients}")
            .AppendLine($"Total messages received: {totalMessages}")
            .AppendLine($"Last error: {lastError}")
            .ToString();

        var missionState = new StringBuilder()
            .AppendLine($"Mission: {missionName}")
            .AppendLine($"MissionId: {missionId}")
            .AppendLine($"Status: {missionStatus}")
            .AppendLine($"Current step: {currentStep}/{totalSteps} - {currentStepTitle}")
            .AppendLine($"Next objective: {nextObjective}")
            .AppendLine($"Remaining distance: {FormatMeters(remainingDistance)}")
            .AppendLine($"Warnings: {JoinOrNone(missionWarnings)}")
            .ToString();

        var vehicleState = new StringBuilder()
            .AppendLine($"Vehicle pose: x={x:F2}, y={y:F2}, z={z:F2}, yaw={yaw:F1}deg")
            .AppendLine($"Speed: {speed:F2} m/s")
            .AppendLine($"Distance to goal: {FormatMeters(distanceToGoal)}")
            .AppendLine($"Heading error: {FormatDegrees(headingError)}")
            .AppendLine($"Obstacle ahead: {obstacleAhead}")
            .AppendLine($"Obstacle count: {obstacleCount}")
            .ToString();

        var sensorState = new StringBuilder()
            .AppendLine($"Sensors healthy: {sensorsHealthy}")
            .AppendLine($"Sensor freshness: {(sensorFresh ? "fresh" : "stale")}")
            .AppendLine($"Sensor count: {FormatNumber(sensorCount)}")
            .AppendLine($"Healthy sensors: {FormatNumber(healthySensorCount)}")
            .AppendLine($"Fusion engine: {fusionName}")
            .AppendLine($"Fusion confidence: {FormatNumber(fusionConfidence)}")
            .AppendLine($"State confidence: {FormatNumber(stateConfidence)}")
            .AppendLine($"Summary: {sensorSummary}")
            .ToString();

        var actuatorState = new StringBuilder()
            .AppendLine($"Actuator enabled: {actuatorEnabled}")
            .AppendLine($"Actuator healthy: {actuatorHealthy}")
            .AppendLine($"Allocation success: {allocationSuccess}")
            .AppendLine($"Allocation reason: {allocationReason}")
            .AppendLine($"Active thrusters: {FormatNumber(activeThrusters)}")
            .AppendLine($"Healthy thrusters: {FormatNumber(healthyThrusters)}")
            .AppendLine($"Reverse clamp count: {FormatNumber(reverseClamp)}")
            .AppendLine($"Saturation ratio: {FormatNumber(saturation)}")
            .AppendLine($"Force body: Fx={FormatNumber(forceX)}, Fy={FormatNumber(forceY)}, Fz={FormatNumber(forceZ)}")
            .ToString();

        var safetyState = new StringBuilder()
            .AppendLine("AI authority: suggest/analyze only")
            .AppendLine("AI direct motor command: forbidden")
            .AppendLine("Human approval required before mission command execution")
            .AppendLine($"Gateway/runtime health: {gatewayStatus}, runtimeConnected={runtimeConnected}, runtimeFresh={runtimeFresh}")
            .AppendLine($"Actuator allocation: success={allocationSuccess}, reason={allocationReason}, reverseClamp={FormatNumber(reverseClamp)}")
            .ToString();

        var worldState = new StringBuilder()
            .AppendLine($"Scenario: {scenarioName}")
            .AppendLine($"ScenarioId: {scenarioId}")
            .AppendLine($"Current objective: {currentObjective}")
            .AppendLine($"Route count: {FormatNumber(routeCount)}")
            .AppendLine($"World object count: {FormatNumber(objectCount)}")
            .AppendLine($"Active target: x={FormatNullable(targetX)}, y={FormatNullable(targetY)}, tolerance={FormatMeters(targetTol)}")
            .AppendLine($"Recent logs: {logs}")
            .ToString();

        var concern = DetectMainConcern(
            missionStatus,
            speed,
            activeThrusters,
            runtimeConnected,
            runtimeFresh,
            sensorsHealthy,
            actuatorHealthy,
            remainingDistance);

        var operatorSummary = new StringBuilder()
            .AppendLine($"Genel durum: Gateway={gatewayStatus}, RuntimeConnected={runtimeConnected}, SensorsHealthy={sensorsHealthy}, ActuatorHealthy={actuatorHealthy}.")
            .AppendLine($"Görev: {missionName}, durum={missionStatus}, aktif hedef={nextObjective}, kalan mesafe={FormatMeters(remainingDistance)}.")
            .AppendLine($"Araç: konum=({x:F1},{y:F1},{z:F1}), hız={speed:F2} m/s, heading error={FormatDegrees(headingError)}.")
            .AppendLine($"Ana dikkat noktası: {concern}")
            .ToString();

        return new GroundAiRuntimeContext(
            RuntimeContext: runtimeContext,
            MissionState: missionState,
            VehicleState: vehicleState,
            SensorState: sensorState,
            ActuatorState: actuatorState,
            SafetyState: safetyState,
            WorldState: worldState,
            OperatorSummary: operatorSummary
        );
    }

    private static string DetectMainConcern(
        string missionStatus,
        double speed,
        double? activeThrusters,
        bool runtimeConnected,
        bool runtimeFresh,
        bool sensorsHealthy,
        bool actuatorHealthy,
        double? remainingDistance)
    {
        if (!runtimeConnected)
            return "Runtime bağlı değil; araçtan canlı veri alınamıyor.";

        if (!runtimeFresh)
            return "Runtime verisi taze değil; haberleşme veya ingest hattı kontrol edilmeli.";

        if (!sensorsHealthy)
            return "Sensör özeti sağlıksız görünüyor; füzyon ve state confidence incelenmeli.";

        if (!actuatorHealthy)
            return "Actuator özeti sağlıksız görünüyor; thruster/allocation hattı incelenmeli.";

        if (string.Equals(missionStatus, "running", StringComparison.OrdinalIgnoreCase) &&
            remainingDistance is > 0.5 &&
            speed < 0.02 &&
            (activeThrusters ?? 0) <= 0)
        {
            return "Görev running durumda ama araç hızı ve aktif thruster sayısı sıfır; arm/authority/control output/actuator command hattı kontrol edilmeli.";
        }

        return "Kritik bir anomali görünmüyor; görev, sensör, actuator ve safety state izlenmeye devam edilmeli.";
    }

    private static JsonElement TryGet(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var child))
            return child;

        return default;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var b) && b,
            _ => false
        };
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return 0;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            return parsed;

        return 0;
    }

    private static double GetDouble(JsonElement element, string propertyName)
        => GetDoubleNullable(element, propertyName) ?? 0;

    private static double? GetDoubleNullable(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static double? GetMetric(JsonElement element, string metricName)
    {
        var metrics = TryGet(element, "metrics");

        if (metrics.ValueKind != JsonValueKind.Object ||
            !metrics.TryGetProperty(metricName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n))
            return n;

        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static string? GetField(JsonElement element, string fieldName)
    {
        var fields = TryGet(element, "fields");

        if (fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty(fieldName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static double EstimateSpeed(JsonElement telemetry)
    {
        var vx = GetDouble(telemetry, "vx");
        var vy = GetDouble(telemetry, "vy");
        var vz = GetDouble(telemetry, "vz");

        return Math.Sqrt(vx * vx + vy * vy + vz * vz);
    }

    private static string[] ReadArrayStrings(JsonElement element, string propertyName, int max)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return array
            .EnumerateArray()
            .Take(max)
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
    }

    private static string ReadLogSummary(JsonElement root, int maxItems)
    {
        if (!root.TryGetProperty("logs", out var logs) || logs.ValueKind != JsonValueKind.Array)
            return "none";

        var items = logs
            .EnumerateArray()
            .Take(maxItems)
            .Select(log =>
            {
                var level = GetString(log, "level") ?? "unknown";
                var category = GetString(log, "category") ?? "unknown";
                var message = GetString(log, "message") ?? "";
                return $"{level}/{category}: {message}";
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return items.Length == 0 ? "none" : string.Join(" | ", items);
    }

    private static string JoinOrNone(string[] items)
        => items.Length == 0 ? "none" : string.Join(" | ", items);

    private static string FormatMeters(double? value)
        => value.HasValue ? $"{value.Value:F2} m" : "n/a";

    private static string FormatDegrees(double? value)
        => value.HasValue ? $"{value.Value:F1} deg" : "n/a";

    private static string FormatNumber(double? value)
        => value.HasValue ? $"{value.Value:F2}" : "n/a";

    private static string FormatNullable(double? value)
        => value.HasValue ? $"{value.Value:F2}" : "n/a";
}