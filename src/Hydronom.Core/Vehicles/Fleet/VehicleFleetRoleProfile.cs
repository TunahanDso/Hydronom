using System;

namespace Hydronom.Core.Vehicles.Fleet
{
    /// <summary>
    /// Aracın filo içerisindeki rol profilidir.
    ///
    /// Örnek roller:
    /// - main_uuv
    /// - mini_rov
    /// - surface_support
    /// - relay
    /// - observer
    /// - leader
    /// - follower
    /// </summary>
    public sealed record VehicleFleetRoleProfile(
        string Role,
        bool CanLeadMission,
        bool CanReceiveMission,
        bool CanRelayMessages,
        bool CanCarryChildVehicle,
        bool CanBeCarriedByParent,
        int Priority,
        IReadOnlyList<string> SupportedMissionTypes,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleFleetRoleProfile Standalone { get; } = new(
            Role: "standalone",
            CanLeadMission: true,
            CanReceiveMission: true,
            CanRelayMessages: false,
            CanCarryChildVehicle: false,
            CanBeCarriedByParent: false,
            Priority: 0,
            SupportedMissionTypes: Array.Empty<string>(),
            Tags: new Dictionary<string, string>());

        public VehicleFleetRoleProfile Sanitized()
        {
            return this with
            {
                Role = Clean(Role, "standalone"),
                Priority = Math.Clamp(Priority, -1000, 1000),
                SupportedMissionTypes = SupportedMissionTypes?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    ?? Array.Empty<string>(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public bool SupportsMissionType(string missionType)
        {
            if (string.IsNullOrWhiteSpace(missionType))
                return false;

            if (SupportedMissionTypes.Count == 0)
                return true;

            return SupportedMissionTypes.Any(x =>
                string.Equals(x, missionType, StringComparison.OrdinalIgnoreCase));
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}