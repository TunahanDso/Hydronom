using System.Text.Json;
using Hydronom.Core.Vehicles;
using Hydronom.Core.Vehicles.Actuation;
using Hydronom.Core.Vehicles.Fleet;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.Vehicles.Safety;
using Hydronom.Core.Vehicles.Sensors;

namespace Hydronom.Runtime.Vehicles.Loading
{
    /// <summary>
    /// Vehicle Profile Package klasörlerini diskten okuyan ana loader'dır.
    ///
    /// Bir profil klasörü şu mantıktadır:
    ///
    /// hydronom_uuv_main_2026/
    ///   manifest.json
    ///   identity.json
    ///   physical.json
    ///   buoyancy.json
    ///   hydrodynamics.json
    ///   actuation.json
    ///   sensors.json
    ///   simulation.json
    ///   safety.json
    ///
    /// Loader iki seviyede çalışır:
    /// - LoadPackage: ham JSON bloklarını okur.
    /// - LoadProfile: typed VehicleProfile üretir.
    /// </summary>
    public sealed class VehicleProfilePackageLoader
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public VehicleProfilePackageLoader(JsonSerializerOptions? jsonOptions = null)
        {
            _jsonOptions = jsonOptions ?? VehicleProfileJsonOptions.Create();
        }

        public VehicleProfilePackage LoadPackage(string packageDirectory)
        {
            if (string.IsNullOrWhiteSpace(packageDirectory))
                throw new ArgumentException("Vehicle profile package directory is empty.", nameof(packageDirectory));

            var root = Path.GetFullPath(packageDirectory);

            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Vehicle profile package directory was not found: {root}");

            var manifestPath = Path.Combine(root, "manifest.json");

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Vehicle profile manifest was not found: {manifestPath}", manifestPath);

            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<VehicleProfileManifest>(manifestJson, _jsonOptions)
                ?? VehicleProfileManifest.Empty;

            manifest = manifest.Sanitized();

            return new VehicleProfilePackage(
                RootDirectory: root,
                Manifest: manifest,
                IdentityJson: ReadOptional(root, "identity.json"),
                PhysicalJson: ReadOptional(root, "physical.json"),
                BuoyancyJson: ReadOptional(root, "buoyancy.json"),
                HydrodynamicsJson: ReadOptional(root, "hydrodynamics.json"),
                ActuationJson: ReadOptional(root, "actuation.json"),
                SensorsJson: ReadOptional(root, "sensors.json"),
                ControlJson: ReadOptional(root, "control.json"),
                SimulationJson: ReadOptional(root, "simulation.json"),
                CommunicationJson: ReadOptional(root, "communication.json"),
                TetherJson: ReadOptional(root, "tether.json"),
                SafetyJson: ReadOptional(root, "safety.json"));
        }

        public VehicleProfile LoadProfile(string packageDirectory)
        {
            var package = LoadPackage(packageDirectory);

            var identity = DeserializeOrDefault(
                package.IdentityJson,
                VehicleIdentityProfile.Unknown).Sanitized();

            var physical = DeserializeOrDefault(
                package.PhysicalJson,
                VehiclePhysicalProfile.Unknown).Sanitized();

            var buoyancy = DeserializeOrDefault(
                package.BuoyancyJson,
                physical.Buoyancy ?? VehicleBuoyancyProfile.Disabled).Sanitized();

            var hydrodynamics = DeserializeOrDefault(
                package.HydrodynamicsJson,
                physical.Hydrodynamics ?? VehicleHydrodynamicProfile.Disabled).Sanitized();

            physical = physical with
            {
                Buoyancy = buoyancy,
                Hydrodynamics = hydrodynamics
            };

            var actuation = DeserializeOrDefault(
                package.ActuationJson,
                VehicleActuationProfile.Empty).Sanitized();

            var sensors = DeserializeOrDefault(
                package.SensorsJson,
                VehicleSensorProfile.Empty).Sanitized();

            var simulation = DeserializeOrDefault(
                package.SimulationJson,
                VehicleSimulationProfile.Unknown).Sanitized();

            var safety = DeserializeOrDefault(
                package.SafetyJson,
                VehicleSafetyProfile.Conservative).Sanitized();

            var fleetRole = DeserializeOptional<VehicleFleetRoleProfile>(
                ReadOptional(package.RootDirectory, "fleet.json"))?.Sanitized();

            var parentChild = DeserializeOptional<VehicleParentChildLinkProfile>(
                package.TetherJson)?.Sanitized();

            var profile = new VehicleProfile(
                Manifest: package.Manifest,
                Identity: identity,
                Physical: physical,
                Actuation: actuation,
                Sensors: sensors,
                Simulation: simulation,
                Safety: safety,
                FleetRole: fleetRole,
                ParentChildLink: parentChild);

            return profile.Sanitized();
        }

        public IReadOnlyList<VehicleProfile> LoadProfilesFromRoot(string profilesRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(profilesRootDirectory))
                throw new ArgumentException("Vehicle profiles root directory is empty.", nameof(profilesRootDirectory));

            var root = Path.GetFullPath(profilesRootDirectory);

            if (!Directory.Exists(root))
                return Array.Empty<VehicleProfile>();

            var profiles = new List<VehicleProfile>();

            foreach (var directory in Directory.GetDirectories(root))
            {
                var manifestPath = Path.Combine(directory, "manifest.json");

                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    profiles.Add(LoadProfile(directory));
                }
                catch
                {
                    /*
                     * İlk sürümde bozuk profil tüm runtime'ı düşürmesin.
                     * Registry tarafında validasyon sonucu ayrıca görülebilir.
                     */
                }
            }

            return profiles
                .OrderBy(x => x.DisplayName)
                .ThenBy(x => x.ProfileId)
                .ToArray();
        }

        private T DeserializeOrDefault<T>(string? json, T fallback)
        {
            if (string.IsNullOrWhiteSpace(json))
                return fallback;

            try
            {
                return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private T? DeserializeOptional<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch
            {
                return default;
            }
        }

        private static string? ReadOptional(string root, string fileName)
        {
            var path = Path.Combine(root, fileName);

            return File.Exists(path)
                ? File.ReadAllText(path)
                : null;
        }
    }
}