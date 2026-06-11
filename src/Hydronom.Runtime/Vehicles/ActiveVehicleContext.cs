using Hydronom.Core.Control;
using Hydronom.Core.Vehicles;
using Hydronom.Core.Vehicles.Actuation;

namespace Hydronom.Runtime.Vehicles
{
    /// <summary>
    /// Runtime içinde aktif seçilmiş aracı ve ondan türetilmiş kabiliyet profilini taşır.
    ///
    /// Bu context:
    /// - Runtime startup
    /// - ScenarioRuntime
    /// - PlatformControlModule
    /// - GroundStation/Fleet
    /// tarafları arasında köprü görevi görür.
    /// </summary>
    public sealed class ActiveVehicleContext
    {
        public VehicleProfile? Profile { get; private set; }

        public VehicleCapabilityProfile CapabilityProfile { get; private set; } =
            VehicleCapabilityProfile.Unknown;

        public string VehicleId =>
            Profile?.VehicleId ?? "unknown-vehicle";

        public string ProfileId =>
            Profile?.ProfileId ?? "unknown_vehicle_profile";

        public VehiclePlatformKind PlatformKind =>
            Profile?.PlatformKind ?? VehiclePlatformKind.Unknown;

        public bool HasProfile => Profile is not null;

        public bool IsUnderwater =>
            Profile?.IsUnderwater == true;

        public bool IsMiniRov =>
            Profile?.IsMiniRov == true;

        public void SetProfile(VehicleProfile? profile)
        {
            Profile = profile?.Sanitized();

            CapabilityProfile = VehicleCapabilityProfileFactory
                .FromVehicleProfile(Profile)
                .Sanitized();
        }

        public void Clear()
        {
            Profile = null;
            CapabilityProfile = VehicleCapabilityProfile.Unknown;
        }

        public string BuildSummary()
        {
            if (Profile is null)
                return "ACTIVE_VEHICLE none";

            return
                $"ACTIVE_VEHICLE vehicleId={Profile.VehicleId} " +
                $"profileId={Profile.ProfileId} " +
                $"platform={Profile.PlatformKind} " +
                $"underwater={Profile.IsUnderwater} " +
                $"miniRov={Profile.IsMiniRov} " +
                $"thrusters={Profile.HasActiveThrusters} " +
                $"depthSensor={Profile.HasDepthSensor} " +
                $"camera={Profile.HasCamera} " +
                $"capability={CapabilityProfile.Summary}";
        }
    }
}