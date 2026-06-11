using Hydronom.Core.Vehicles;

namespace Hydronom.Runtime.Vehicles.Registry
{
    /// <summary>
    /// Runtime içinde yüklü araç profillerini tutan registry'dir.
    ///
    /// ScenarioRuntime, GroundStation, Ops ve Control tarafları buradan profil bulabilir.
    /// </summary>
    public sealed class VehicleProfileRegistry
    {
        private readonly Dictionary<string, VehicleProfile> _profilesByProfileId =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, VehicleProfile> _profilesByVehicleId =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly object _sync = new();

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _profilesByProfileId.Count;
                }
            }
        }

        public bool Register(VehicleProfile? profile)
        {
            if (profile is null)
                return false;

            profile = profile.Sanitized();

            var validation = profile.Validate();

            if (!validation.IsValid)
                return false;

            lock (_sync)
            {
                _profilesByProfileId[profile.ProfileId] = profile;
                _profilesByVehicleId[profile.VehicleId] = profile;
            }

            return true;
        }

        public int RegisterMany(IEnumerable<VehicleProfile>? profiles)
        {
            if (profiles is null)
                return 0;

            var count = 0;

            foreach (var profile in profiles)
            {
                if (Register(profile))
                    count++;
            }

            return count;
        }

        public bool TryGetByProfileId(string profileId, out VehicleProfile? profile)
        {
            profile = null;

            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            lock (_sync)
            {
                return _profilesByProfileId.TryGetValue(profileId.Trim(), out profile);
            }
        }

        public bool TryGetByVehicleId(string vehicleId, out VehicleProfile? profile)
        {
            profile = null;

            if (string.IsNullOrWhiteSpace(vehicleId))
                return false;

            lock (_sync)
            {
                return _profilesByVehicleId.TryGetValue(vehicleId.Trim(), out profile);
            }
        }

        public IReadOnlyList<VehicleProfile> GetSnapshot()
        {
            lock (_sync)
            {
                return _profilesByProfileId.Values
                    .OrderBy(x => x.DisplayName)
                    .ThenBy(x => x.ProfileId)
                    .ToArray();
            }
        }

        public IReadOnlyList<VehicleProfile> GetByPlatformKind(VehiclePlatformKind platformKind)
        {
            lock (_sync)
            {
                return _profilesByProfileId.Values
                    .Where(x => x.PlatformKind == platformKind)
                    .OrderBy(x => x.DisplayName)
                    .ThenBy(x => x.ProfileId)
                    .ToArray();
            }
        }

        public IReadOnlyList<VehicleProfile> GetUnderwaterProfiles()
        {
            lock (_sync)
            {
                return _profilesByProfileId.Values
                    .Where(x => x.IsUnderwater)
                    .OrderBy(x => x.DisplayName)
                    .ThenBy(x => x.ProfileId)
                    .ToArray();
            }
        }

        public bool RemoveByProfileId(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            lock (_sync)
            {
                if (!_profilesByProfileId.TryGetValue(profileId.Trim(), out var profile))
                    return false;

                _profilesByProfileId.Remove(profile.ProfileId);
                _profilesByVehicleId.Remove(profile.VehicleId);

                return true;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _profilesByProfileId.Clear();
                _profilesByVehicleId.Clear();
            }
        }
    }
}