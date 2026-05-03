namespace Hydronom.GroundStation.MissionCompatibility;

using Hydronom.Core.Fleet;

/// <summary>
/// Bir aracÄ±n belirli bir gÃ¶rev tipine ve capability gereksinimlerine gÃ¶re uygunluÄŸunu deÄŸerlendirir.
/// </summary>
public sealed class MissionCompatibilityEvaluator
{
    private readonly VehicleCapabilityMatcher _capabilityMatcher;

    public MissionCompatibilityEvaluator(VehicleCapabilityMatcher? capabilityMatcher = null)
    {
        _capabilityMatcher = capabilityMatcher ?? new VehicleCapabilityMatcher();
    }

    /// <summary>
    /// Tek bir aracÄ± gÃ¶rev uyumluluÄŸu aÃ§Ä±sÄ±ndan deÄŸerlendirir.
    /// </summary>
    public MissionCompatibilityResult Evaluate(
        VehicleNodeStatus? vehicle,
        string missionType,
        IReadOnlyList<string>? allowedVehicleTypes = null,
        IReadOnlyList<MissionCapabilityRequirement>? capabilityRequirements = null,
        bool requireOnline = true,
        bool allowSimulation = true)
    {
        var issues = new List<MissionCompatibilityIssue>();
        var matchedCapabilities = new List<string>();
        var missingRequiredCapabilities = new List<string>();

        capabilityRequirements ??= Array.Empty<MissionCapabilityRequirement>();
        allowedVehicleTypes ??= Array.Empty<string>();

        if (vehicle is null)
        {
            issues.Add(MissionCompatibilityIssue.Blocking(
                "VEHICLE_NULL",
                "Vehicle status is null."));

            return MissionCompatibilityResult.Rejected(
                null,
                missionType,
                issues);
        }

        var vehicleId = vehicle.Identity.NodeId;
        var vehicleType = vehicle.Identity.VehicleType;

        if (requireOnline && !vehicle.IsOnline)
        {
            issues.Add(MissionCompatibilityIssue.Blocking(
                "VEHICLE_OFFLINE",
                $"Vehicle '{vehicleId}' is offline."));
        }

        if (!allowSimulation && vehicle.Identity.IsSimulation)
        {
            issues.Add(MissionCompatibilityIssue.Blocking(
                "SIMULATION_NOT_ALLOWED",
                $"Vehicle '{vehicleId}' is a simulation node but mission requires real hardware."));
        }

        if (allowedVehicleTypes.Count > 0 &&
            !allowedVehicleTypes.Any(x => string.Equals(x, vehicleType, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(MissionCompatibilityIssue.Blocking(
                "VEHICLE_TYPE_NOT_ALLOWED",
                $"Vehicle type '{vehicleType}' is not allowed for mission type '{missionType}'."));
        }

        var score = 0.0;
        var maxScore = 0.0;

        foreach (var requirement in capabilityRequirements)
        {
            if (requirement is null)
                continue;

            maxScore += Math.Max(0, requirement.Weight);

            var matched = _capabilityMatcher.Matches(
                vehicle,
                requirement,
                out var matchedCapability,
                out var issue);

            if (matched && matchedCapability is not null)
            {
                matchedCapabilities.Add(matchedCapability.Name);
                score += Math.Max(0, requirement.Weight);
                continue;
            }

            if (issue is not null)
                issues.Add(issue);

            if (requirement.Required)
                missingRequiredCapabilities.Add(requirement.Name);
        }

        var normalizedScore = maxScore <= 0
            ? 100
            : Math.Round((score / maxScore) * 100.0, 2);

        var blocking = issues.Any(x => x.IsBlocking);
        var compatible = !blocking;

        var reason = compatible
            ? $"Vehicle '{vehicleId}' is compatible with mission '{missionType}'. Score={normalizedScore:0.##}."
            : $"Vehicle '{vehicleId}' is not compatible with mission '{missionType}'. Score={normalizedScore:0.##}.";

        return new MissionCompatibilityResult
        {
            VehicleId = vehicleId,
            VehicleType = vehicleType,
            MissionType = missionType,
            IsCompatible = compatible,
            Score = normalizedScore,
            Reason = reason,
            Issues = issues,
            MatchedCapabilities = matchedCapabilities
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            MissingRequiredCapabilities = missingRequiredCapabilities
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    /// <summary>
    /// Bir filo snapshot'Ä± iÃ§inden gÃ¶reve en uygun araÃ§larÄ± skor sÄ±rasÄ±yla dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public IReadOnlyList<MissionCompatibilityResult> RankVehicles(
        IReadOnlyList<VehicleNodeStatus> fleetSnapshot,
        string missionType,
        IReadOnlyList<string>? allowedVehicleTypes = null,
        IReadOnlyList<MissionCapabilityRequirement>? capabilityRequirements = null,
        bool requireOnline = true,
        bool allowSimulation = true)
    {
        fleetSnapshot ??= Array.Empty<VehicleNodeStatus>();

        return fleetSnapshot
            .Select(vehicle => Evaluate(
                vehicle,
                missionType,
                allowedVehicleTypes,
                capabilityRequirements,
                requireOnline,
                allowSimulation))
            .OrderByDescending(x => x.IsCompatible)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.VehicleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
