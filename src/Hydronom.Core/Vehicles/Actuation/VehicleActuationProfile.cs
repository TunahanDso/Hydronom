using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Actuation
{
    /// <summary>
    /// Aracın tüm aktüasyon profilidir.
    ///
    /// İlk sürümde ana eksen thruster listesidir.
    /// Bu profil, VehicleCapabilityProfileFactory tarafından okunarak:
    /// - reverse var mı?
    /// - lateral force üretilebilir mi?
    /// - vertical force üretilebilir mi?
    /// - yaw moment üretilebilir mi?
    /// gibi kabiliyetler türetilir.
    /// </summary>
    public sealed record VehicleActuationProfile(
        bool Enabled,
        IReadOnlyList<VehicleThrusterProfile> Thrusters,
        bool HasRudder,
        bool HasDifferentialThrust,
        bool HasVectoring,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleActuationProfile Empty { get; } = new(
            Enabled: false,
            Thrusters: Array.Empty<VehicleThrusterProfile>(),
            HasRudder: false,
            HasDifferentialThrust: false,
            HasVectoring: false,
            Tags: new Dictionary<string, string>());

        public IReadOnlyList<VehicleThrusterProfile> ActiveThrusters =>
            Thrusters?
                .Where(x => x is not null && x.Sanitized().IsValid)
                .Select(x => x.Sanitized())
                .ToArray()
            ?? Array.Empty<VehicleThrusterProfile>();

        public bool HasAnyActiveThruster => ActiveThrusters.Count > 0;

        public bool HasReverseAuthority =>
            ActiveThrusters.Any(x => x.CanReverse && x.MaxReverseThrustN > 0.0);

        public bool HasVerticalAuthority =>
            ActiveThrusters.Any(x => Math.Abs(x.NormalizedDirection.Z) >= 0.50);

        public bool HasLateralAuthority =>
            ActiveThrusters.Any(x => Math.Abs(x.NormalizedDirection.Y) >= 0.35);

        public VehicleActuationProfile Sanitized()
        {
            return this with
            {
                Thrusters = Thrusters?
                    .Where(x => x is not null)
                    .Select(x => x.Sanitized())
                    .ToArray()
                    ?? Array.Empty<VehicleThrusterProfile>(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}