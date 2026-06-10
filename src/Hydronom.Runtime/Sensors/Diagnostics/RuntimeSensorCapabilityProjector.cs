using Hydronom.Core.Sensors.Common.Abstractions;
using Hydronom.Core.Sensors.Common.Capabilities;

namespace Hydronom.Runtime.Sensors.Diagnostics;

/// <summary>
/// SensorRuntime.Capabilities modelini runtime diagnostics için okunabilir snapshot'a dönüştürür.
/// </summary>
public sealed class RuntimeSensorCapabilityProjector
{
    private static readonly string[] DefaultTrackedCapabilities =
    {
        "global_position",
        "local_position",
        "ground_speed",
        "linear_acceleration",
        "angular_velocity",
        "attitude_estimation",
        "depth",
        "vertical_position",
        "pressure",
        "obstacle_detection",
        "camera_image",
        "power_monitoring"
    };

    public RuntimeSensorCapabilitySnapshot Project(ISensorRuntime? runtime)
    {
        if (runtime is null)
            return Project(SensorCapabilitySet.Empty);
        
        return Project(runtime.Capabilities);
    }

    public RuntimeSensorCapabilitySnapshot Project(SensorCapabilitySet capabilitySet)
    {
        var safe = capabilitySet.Sanitized();

        var entries = new List<RuntimeSensorCapabilityEntry>();

        foreach (var capability in safe.Capabilities)
        {
            var cap = capability.Sanitized();

            entries.Add(new RuntimeSensorCapabilityEntry(
                Name: cap.Name,
                Status: RuntimeSensorCapabilityEntry.ResolveStatus(cap.Confidence),
                Confidence: cap.Confidence,
                Provider: cap.Provider,
                FrameId: cap.FrameId,
                TargetRateHz: cap.TargetRateHz,
                RequiredCalibration: cap.RequiredCalibration,
                CalibrationValid: cap.CalibrationValid,
                Summary: ""
            ).Sanitized());
        }

        foreach (var tracked in DefaultTrackedCapabilities)
        {
            if (entries.Any(x => string.Equals(x.Name, tracked, StringComparison.OrdinalIgnoreCase)))
                continue;

            entries.Add(RuntimeSensorCapabilityEntry.Missing(tracked));
        }

        return new RuntimeSensorCapabilitySnapshot(
            TimestampUtc: DateTime.UtcNow,
            Capabilities: entries,
            CapabilityCount: entries.Count,
            AvailableCount: 0,
            DegradedCount: 0,
            MissingCount: 0,
            HasGlobalPosition: false,
            HasLocalPosition: false,
            HasAttitude: false,
            HasDepth: false,
            HasObstacleDetection: false,
            Summary: ""
        ).Sanitized();
    }
}