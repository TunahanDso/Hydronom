using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using System.Globalization;
using System.Text.Json;

namespace HydronomOps.Gateway.Services.Mapping;

/// <summary>
/// RuntimeTelemetrySummary / RuntimeSummary mesajlarÄ±nÄ± Gateway DTO modellerine dÃ¶nÃ¼ÅŸtÃ¼ren mapper parÃ§asÄ±.
/// Ana RuntimeToGatewayMapper dosyasÄ±nÄ± bÃ¼yÃ¼tmemek iÃ§in partial olarak ayrÄ±lmÄ±ÅŸtÄ±r.
/// </summary>
public sealed partial class RuntimeToGatewayMapper
{
    public VehicleTelemetryDto MapVehicleTelemetryFromRuntimeSummary(JsonElement root)
    {
        var now = _clock.UtcNow;

        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ??
            now;

        var vehicleId =
            GetString(root, "vehicleId", "VehicleId") ??
            "hydronom-main";

        var x = GetDouble(root, "stateX", "StateX", "x", "X");
        var y = GetDouble(root, "stateY", "StateY", "y", "Y");
        var z = GetDouble(root, "stateZ", "StateZ", "z", "Z");
        var yawDeg = GetDouble(root, "stateYawDeg", "StateYawDeg", "yawDeg", "YawDeg");

        var stateConfidence = GetDouble(root, "stateConfidence", "StateConfidence");
        var fusionConfidence = GetDouble(root, "fusionConfidence", "FusionConfidence");

        var acceptedCount = GetDouble(root, "acceptedStateUpdateCount", "AcceptedStateUpdateCount");
        var rejectedCount = GetDouble(root, "rejectedStateUpdateCount", "RejectedStateUpdateCount");

        var sensorCount = GetDouble(root, "sensorCount", "SensorCount");
        var healthySensorCount = GetDouble(root, "healthySensorCount", "HealthySensorCount");

        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["runtime.sensorCount"] = sensorCount,
            ["runtime.healthySensorCount"] = healthySensorCount,
            ["runtime.fusionConfidence"] = fusionConfidence,
            ["runtime.stateConfidence"] = stateConfidence,
            ["runtime.acceptedStateUpdateCount"] = acceptedCount,
            ["runtime.rejectedStateUpdateCount"] = rejectedCount,
            ["vehicle.x"] = x,
            ["vehicle.y"] = y,
            ["vehicle.z"] = z,
            ["vehicle.headingDeg"] = yawDeg
        };

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["origin"] = "runtime-telemetry-summary",
            ["runtime.runtimeId"] = GetString(root, "runtimeId", "RuntimeId") ?? "hydronom_runtime",
            ["runtime.overallHealth"] = GetString(root, "overallHealth", "OverallHealth") ?? "Unknown",
            ["runtime.fusionEngineName"] = GetString(root, "fusionEngineName", "FusionEngineName") ?? "unknown_fusion",
            ["runtime.fusionProducedCandidate"] = BoolText(GetBool(root, "fusionProducedCandidate", "FusionProducedCandidate")),
            ["runtime.lastStateDecision"] = GetString(root, "lastStateDecision", "LastStateDecision") ?? "Unknown",
            ["runtime.lastStateAccepted"] = BoolText(GetBool(root, "lastStateAccepted", "LastStateAccepted")),
            ["runtime.hasState"] = BoolText(GetBool(root, "hasState", "HasState")),
            ["runtime.hasCriticalIssue"] = BoolText(GetBool(root, "hasCriticalIssue", "HasCriticalIssue")),
            ["runtime.hasWarnings"] = BoolText(GetBool(root, "hasWarnings", "HasWarnings")),
            ["runtime.summary"] = GetString(root, "summary", "Summary") ?? string.Empty
        };

        return new VehicleTelemetryDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = vehicleId,

            X = x,
            Y = y,
            Z = z,

            RollDeg = 0.0,
            PitchDeg = 0.0,
            YawDeg = yawDeg,
            HeadingDeg = yawDeg,

            Vx = 0.0,
            Vy = 0.0,
            Vz = 0.0,

            RollRateDeg = 0.0,
            PitchRateDeg = 0.0,
            YawRateDeg = 0.0,

            ObstacleAhead = false,
            ObstacleCount = 0,
            Obstacles = new List<ObstacleDto>(),
            Landmarks = new List<LandmarkDto>(),

            Metrics = metrics,
            Fields = fields,
            Freshness = BuildFreshness(timestampUtc, "runtime-summary")
        };
    }

    public SensorStateDto MapSensorStateFromRuntimeSummary(JsonElement root)
    {
        var now = _clock.UtcNow;

        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ??
            now;

        var vehicleId =
            GetString(root, "vehicleId", "VehicleId") ??
            "hydronom-main";

        var sensorCount = GetInt(root, 0, "sensorCount", "SensorCount");
        var healthySensorCount = GetInt(root, 0, "healthySensorCount", "HealthySensorCount");

        var hasCritical = GetBool(root, "hasCriticalIssue", "HasCriticalIssue");
        var hasWarnings = GetBool(root, "hasWarnings", "HasWarnings");

        var fusionEngineName =
            GetString(root, "fusionEngineName", "FusionEngineName") ??
            "unknown_fusion";

        var summary =
            GetString(root, "summary", "Summary") ??
            string.Empty;

        var isHealthy =
            !hasCritical &&
            sensorCount > 0 &&
            healthySensorCount >= sensorCount;

        var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["runtime.sensorCount"] = sensorCount,
            ["runtime.healthySensorCount"] = healthySensorCount,
            ["runtime.fusionConfidence"] = GetDouble(root, "fusionConfidence", "FusionConfidence"),
            ["runtime.stateConfidence"] = GetDouble(root, "stateConfidence", "StateConfidence"),
            ["runtime.acceptedStateUpdateCount"] = GetDouble(root, "acceptedStateUpdateCount", "AcceptedStateUpdateCount"),
            ["runtime.rejectedStateUpdateCount"] = GetDouble(root, "rejectedStateUpdateCount", "RejectedStateUpdateCount")
        };

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["origin"] = "runtime-telemetry-summary",
            ["runtime.overallHealth"] = GetString(root, "overallHealth", "OverallHealth") ?? "Unknown",
            ["runtime.fusionEngineName"] = fusionEngineName,
            ["runtime.fusionProducedCandidate"] = BoolText(GetBool(root, "fusionProducedCandidate", "FusionProducedCandidate")),
            ["runtime.lastStateDecision"] = GetString(root, "lastStateDecision", "LastStateDecision") ?? "Unknown",
            ["runtime.lastStateAccepted"] = BoolText(GetBool(root, "lastStateAccepted", "LastStateAccepted")),
            ["runtime.hasWarnings"] = BoolText(hasWarnings),
            ["runtime.summary"] = summary
        };

        return new SensorStateDto
        {
            TimestampUtc = timestampUtc,
            VehicleId = vehicleId,

            SensorName = "RuntimeSensorSummary",
            SensorType = "runtime-summary",
            Source = "csharp-primary-runtime",
            Backend = "runtime-diagnostics",

            IsSimulated = false,
            IsEnabled = sensorCount > 0,
            IsHealthy = isHealthy,

            ConfiguredRateHz = null,
            EffectiveRateHz = null,
            LastSampleUtc = timestampUtc,

            LastError = hasCritical
                ? string.IsNullOrWhiteSpace(summary)
                    ? "Runtime reported critical issue."
                    : summary
                : null,

            Metrics = metrics,
            Fields = fields,
            Freshness = BuildFreshness(timestampUtc, "runtime-summary")
        };
    }

    public DiagnosticsStateDto MapDiagnosticsStateFromRuntimeSummary(JsonElement root)
    {
        var now = _clock.UtcNow;

        var timestampUtc =
            GetNullableDateTime(root, "timestampUtc", "TimestampUtc") ??
            now;

        var overallHealth =
            GetString(root, "overallHealth", "OverallHealth") ??
            "Unknown";

        var summary =
            GetString(root, "summary", "Summary") ??
            $"Runtime summary health={overallHealth}";

        var hasCritical = GetBool(root, "hasCriticalIssue", "HasCriticalIssue");

        return new DiagnosticsStateDto
        {
            TimestampUtc = timestampUtc,

            // GatewayStatus burada runtime'Ä±n overall health bilgisini taÅŸÄ±r.
            GatewayStatus = overallHealth,
            RuntimeConnected = true,

            HasWebSocketClients = false,
            ConnectedWebSocketClients = 0,

            LastRuntimeMessageUtc = timestampUtc,
            RuntimeFreshness = BuildFreshness(timestampUtc, "runtime-summary"),

            LastError = hasCritical
                ? $"Runtime critical: {summary}"
                : null,

            LastErrorUtc = hasCritical ? timestampUtc : null,

            IngressMessageCount = GetLong(root, 0, "ingressMessageCount", "IngressMessageCount"),
            BroadcastMessageCount = GetLong(root, 0, "broadcastMessageCount", "BroadcastMessageCount")
        };
    }
}
