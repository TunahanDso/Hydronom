using Hydronom.Core.Vehicles;
using Hydronom.Runtime.Vehicles.Registry;

namespace Hydronom.Runtime.Vehicles
{
    /// <summary>
    /// Runtime başlangıcında yüklenen vehicle profile durumunu taşır.
    ///
    /// Bu binding:
    /// - Registry
    /// - ActiveVehicleContext
    /// - Yüklenen profil listesi
    /// - Seçilen profil id
    /// bilgilerini tek yerde toplar.
    /// </summary>
    public sealed record VehicleProfileRuntimeBinding(
        bool Enabled,
        string ProfilesRootDirectory,
        string? RequestedProfileId,
        VehicleProfileRegistry Registry,
        ActiveVehicleContext ActiveContext,
        IReadOnlyList<VehicleProfile> LoadedProfiles)
    {
        public bool HasActiveProfile =>
            Enabled &&
            ActiveContext.HasProfile;

        public VehicleProfile? ActiveProfile =>
            ActiveContext.Profile;

        public string BuildSummary()
        {
            if (!Enabled)
                return "[VEHICLE-PROFILE] Disabled.";

            var requested = string.IsNullOrWhiteSpace(RequestedProfileId)
                ? "auto"
                : RequestedProfileId.Trim();

            if (!HasActiveProfile)
            {
                return
                    "[VEHICLE-PROFILE] Enabled but no active profile selected. " +
                    $"requested={requested} loaded={LoadedProfiles.Count} root={ProfilesRootDirectory}";
            }

            return
                "[VEHICLE-PROFILE] Active " +
                $"requested={requested} " +
                $"loaded={LoadedProfiles.Count} " +
                $"vehicleId={ActiveContext.VehicleId} " +
                $"profileId={ActiveContext.ProfileId} " +
                $"platform={ActiveContext.PlatformKind} " +
                $"capability={ActiveContext.CapabilityProfile.Summary}";
        }
    }
}