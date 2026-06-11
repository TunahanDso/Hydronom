using Hydronom.Core.Vehicles;
using Hydronom.Runtime.Vehicles.Loading;
using Hydronom.Runtime.Vehicles.Registry;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Vehicles
{
    /// <summary>
    /// Runtime startup sırasında Vehicle Profile Package sistemini ayağa kaldırır.
    ///
    /// Config önceliği:
    /// 1) VehicleProfile:ProfileId
    /// 2) ScenarioRuntime:VehicleProfileId
    /// 3) VehicleProfile:VehicleId
    /// 4) ScenarioRuntime:VehicleId
    /// 5) Runtime:TelemetrySummary:VehicleId
    /// 6) Varsayılan surface profile
    /// </summary>
    public static class VehicleProfileRuntimeBootstrapper
    {
        private const string DefaultSurfaceProfileId = "hydronom_surface_mk1";

        public static VehicleProfileRuntimeBinding Bootstrap(
            IConfiguration config,
            string? baseDirectory = null)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            var enabled = ReadBool(config, "VehicleProfile:Enabled", true);

            var registry = new VehicleProfileRegistry();
            var activeContext = new ActiveVehicleContext();

            var profilesRoot = ResolveProfilesRoot(
                config,
                baseDirectory ?? AppContext.BaseDirectory);

            if (!enabled)
            {
                return new VehicleProfileRuntimeBinding(
                    Enabled: false,
                    ProfilesRootDirectory: profilesRoot,
                    RequestedProfileId: null,
                    Registry: registry,
                    ActiveContext: activeContext,
                    LoadedProfiles: Array.Empty<VehicleProfile>());
            }

            var loader = new VehicleProfilePackageLoader();
            var loadedProfiles = loader.LoadProfilesFromRoot(profilesRoot);

            registry.RegisterMany(loadedProfiles);

            var requestedProfileId = ResolveRequestedProfileId(config);
            var requestedVehicleId = ResolveRequestedVehicleId(config);

            VehicleProfile? selected = null;

            if (!string.IsNullOrWhiteSpace(requestedProfileId))
                registry.TryGetByProfileId(requestedProfileId, out selected);

            if (selected is null && !string.IsNullOrWhiteSpace(requestedVehicleId))
                registry.TryGetByVehicleId(requestedVehicleId, out selected);

            if (selected is null)
                registry.TryGetByProfileId(DefaultSurfaceProfileId, out selected);

            activeContext.SetProfile(selected);

            return new VehicleProfileRuntimeBinding(
                Enabled: true,
                ProfilesRootDirectory: profilesRoot,
                RequestedProfileId: requestedProfileId,
                Registry: registry,
                ActiveContext: activeContext,
                LoadedProfiles: loadedProfiles);
        }

        private static string ResolveProfilesRoot(
            IConfiguration config,
            string baseDirectory)
        {
            var configured = config["VehicleProfile:ProfilesRoot"];

            if (!string.IsNullOrWhiteSpace(configured))
            {
                var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());

                if (Path.IsPathRooted(expanded))
                    return Path.GetFullPath(expanded);

                var repoRoot = FindRepoRoot(baseDirectory);

                if (!string.IsNullOrWhiteSpace(repoRoot))
                    return Path.GetFullPath(Path.Combine(repoRoot, expanded));

                return Path.GetFullPath(Path.Combine(baseDirectory, expanded));
            }

            var root = FindRepoRoot(baseDirectory);

            if (!string.IsNullOrWhiteSpace(root))
            {
                return Path.Combine(
                    root,
                    "src",
                    "Hydronom.Runtime",
                    "Vehicles",
                    "Profiles");
            }

            return Path.Combine(
                baseDirectory,
                "Vehicles",
                "Profiles");
        }

        private static string? ResolveRequestedProfileId(IConfiguration config)
        {
            var direct = config["VehicleProfile:ProfileId"];

            if (!string.IsNullOrWhiteSpace(direct))
                return direct.Trim();

            var scenario = config["ScenarioRuntime:VehicleProfileId"];

            if (!string.IsNullOrWhiteSpace(scenario))
                return scenario.Trim();

            return null;
        }

        private static string? ResolveRequestedVehicleId(IConfiguration config)
        {
            var direct = config["VehicleProfile:VehicleId"];

            if (!string.IsNullOrWhiteSpace(direct))
                return direct.Trim();

            var scenario = config["ScenarioRuntime:VehicleId"];

            if (!string.IsNullOrWhiteSpace(scenario))
                return scenario.Trim();

            var telemetry = config["Runtime:TelemetrySummary:VehicleId"];

            if (!string.IsNullOrWhiteSpace(telemetry))
                return telemetry.Trim();

            return null;
        }

        private static string? FindRepoRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);

            while (dir is not null)
            {
                var slnPath = Path.Combine(dir.FullName, "Hydronom.sln");

                if (File.Exists(slnPath))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }

        private static bool ReadBool(
            IConfiguration config,
            string key,
            bool fallback)
        {
            var raw = config[key];

            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            return bool.TryParse(raw, out var value)
                ? value
                : fallback;
        }
    }
}