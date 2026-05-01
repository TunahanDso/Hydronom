namespace Hydronom.GroundStation.MissionCompatibility;

using Hydronom.Core.Fleet;

/// <summary>
/// Araç capability listesi ile görev capability gereksinimlerini eşleştirir.
/// </summary>
public sealed class VehicleCapabilityMatcher
{
    /// <summary>
    /// Tek bir capability gereksinimini araç üzerinde arar.
    /// </summary>
    public bool Matches(
        VehicleNodeStatus vehicle,
        MissionCapabilityRequirement requirement,
        out VehicleCapability? matchedCapability,
        out MissionCompatibilityIssue? issue)
    {
        matchedCapability = null;
        issue = null;

        if (vehicle is null)
        {
            issue = MissionCompatibilityIssue.Blocking(
                "VEHICLE_NULL",
                "Vehicle status is null.");

            return false;
        }

        if (requirement is null || string.IsNullOrWhiteSpace(requirement.Name))
        {
            issue = MissionCompatibilityIssue.Warning(
                "REQUIREMENT_EMPTY",
                "Capability requirement is empty.");

            return false;
        }

        var capability = vehicle.Capabilities.FirstOrDefault(x =>
            string.Equals(x.Name, requirement.Name, StringComparison.OrdinalIgnoreCase));

        if (capability is null)
        {
            issue = requirement.Required
                ? MissionCompatibilityIssue.Blocking(
                    "REQUIRED_CAPABILITY_MISSING",
                    $"Required capability '{requirement.Name}' is missing.")
                : MissionCompatibilityIssue.Warning(
                    "PREFERRED_CAPABILITY_MISSING",
                    $"Preferred capability '{requirement.Name}' is missing.");

            return false;
        }

        matchedCapability = capability;

        if (requirement.RequireEnabled && !capability.IsEnabled)
        {
            issue = requirement.Required
                ? MissionCompatibilityIssue.Blocking(
                    "CAPABILITY_DISABLED",
                    $"Capability '{requirement.Name}' exists but is disabled.")
                : MissionCompatibilityIssue.Warning(
                    "PREFERRED_CAPABILITY_DISABLED",
                    $"Preferred capability '{requirement.Name}' exists but is disabled.");

            return false;
        }

        if (requirement.RequireHealthy &&
            !string.Equals(capability.Health, "OK", StringComparison.OrdinalIgnoreCase))
        {
            issue = requirement.Required
                ? MissionCompatibilityIssue.Blocking(
                    "CAPABILITY_UNHEALTHY",
                    $"Capability '{requirement.Name}' is not healthy. Health={capability.Health}.")
                : MissionCompatibilityIssue.Warning(
                    "PREFERRED_CAPABILITY_UNHEALTHY",
                    $"Preferred capability '{requirement.Name}' is not healthy. Health={capability.Health}.");

            return false;
        }

        return true;
    }
}