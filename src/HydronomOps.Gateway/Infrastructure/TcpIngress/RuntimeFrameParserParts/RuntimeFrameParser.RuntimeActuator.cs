using System.Text.Json;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private void ProcessRuntimeActuatorState(JsonElement root)
    {
        var timestamp = ReadTimestamp(root);

        var actuator = new ActuatorStateDto
        {
            TimestampUtc = timestamp,
            VehicleId = GetString(root, "vehicleId", "VehicleId") ?? "hydronom-main",
            ActuatorName = GetString(root, "actuatorName", "ActuatorName") ?? "thruster-array",
            ActuatorType = GetString(root, "actuatorType", "ActuatorType") ?? "thruster-group",
            IsEnabled = TryReadBool(root, "isEnabled", "IsEnabled") ?? true,
            IsHealthy = TryReadBool(root, "isHealthy", "IsHealthy") ?? true,
            Command = TryReadDouble(root, "command", "Command") ?? 0.0,
            Freshness = new FreshnessDto
            {
                TimestampUtc = timestamp,
                AgeMs = 0,
                IsFresh = true,
                ThresholdMs = 5000,
                Source = "runtime-actuator-state"
            }
        };

        if (root.TryGetProperty("forceBody", out var forceBody))
        {
            actuator.Metrics["force.x"] = TryReadDouble(forceBody, "x", "X") ?? 0.0;
            actuator.Metrics["force.y"] = TryReadDouble(forceBody, "y", "Y") ?? 0.0;
            actuator.Metrics["force.z"] = TryReadDouble(forceBody, "z", "Z") ?? 0.0;
        }

        if (root.TryGetProperty("torqueBody", out var torqueBody))
        {
            actuator.Metrics["torque.x"] = TryReadDouble(torqueBody, "x", "X") ?? 0.0;
            actuator.Metrics["torque.y"] = TryReadDouble(torqueBody, "y", "Y") ?? 0.0;
            actuator.Metrics["torque.z"] = TryReadDouble(torqueBody, "z", "Z") ?? 0.0;
        }

        if (root.TryGetProperty("allocation", out var allocation))
        {
            actuator.Fields["allocation.reason"] = GetString(allocation, "reason", "Reason") ?? string.Empty;
            actuator.Metrics["allocation.normalizedError"] = TryReadDouble(allocation, "normalizedError", "NormalizedError") ?? 0.0;
            actuator.Metrics["allocation.saturationRatio"] = TryReadDouble(allocation, "saturationRatio", "SaturationRatio") ?? 0.0;
            actuator.Metrics["allocation.activeThrusterCount"] = TryReadDouble(allocation, "activeThrusterCount", "ActiveThrusterCount") ?? 0.0;
            actuator.Metrics["allocation.healthyThrusterCount"] = TryReadDouble(allocation, "healthyThrusterCount", "HealthyThrusterCount") ?? 0.0;
            actuator.Metrics["allocation.reverseClampCount"] = TryReadDouble(allocation, "reverseClampCount", "ReverseClampCount") ?? 0.0;
            actuator.Fields["allocation.success"] = (TryReadBool(allocation, "success", "Success") ?? false).ToString();
            actuator.Fields["allocation.authorityLimited"] = (TryReadBool(allocation, "authorityLimited", "AuthorityLimited") ?? false).ToString();
        }

        if (root.TryGetProperty("thrusters", out var thrusters) &&
            thrusters.ValueKind == JsonValueKind.Array)
        {
            var index = 0;

            foreach (var thruster in thrusters.EnumerateArray())
            {
                var prefix = $"thruster.{index}";

                actuator.Fields[$"{prefix}.id"] =
                    GetString(thruster, "id", "Id") ?? $"T{index + 1}";

                actuator.Fields[$"{prefix}.healthy"] =
                    (TryReadBool(thruster, "healthy", "Healthy") ?? true).ToString();

                actuator.Fields[$"{prefix}.canReverse"] =
                    (TryReadBool(thruster, "canReverse", "CanReverse") ?? false).ToString();

                actuator.Fields[$"{prefix}.healthFlags"] =
                    GetString(thruster, "healthFlags", "HealthFlags") ?? string.Empty;

                actuator.Metrics[$"{prefix}.command"] =
                    TryReadDouble(
                        thruster,
                        "normalizedCommand",
                        "NormalizedCommand",
                        "command",
                        "Command") ?? 0.0;

                actuator.Metrics[$"{prefix}.rpm"] =
                    TryReadDouble(thruster, "rpm", "Rpm") ?? 0.0;

                actuator.Metrics[$"{prefix}.currentMa"] =
                    TryReadDouble(thruster, "currentMa", "CurrentMa") ?? 0.0;

                index++;
            }

            actuator.Metrics["thruster.count"] = index;
        }

        _stateStore.SetActuatorState(actuator);
        _stateStore.TouchRuntimeMessage("RuntimeActuatorState");
    }
}