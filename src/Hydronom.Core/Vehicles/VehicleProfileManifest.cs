using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Bir Vehicle Profile Package klasörünün giriş dosyasıdır.
    ///
    /// manifest.json şunu söyler:
    /// - Paket kimliği nedir?
    /// - Paket hangi Hydronom sürümü/şeması için?
    /// - Hangi alt JSON dosyaları bu pakete dahildir?
    /// - Paket hangi platform ailesine aittir?
    /// </summary>
    public sealed record VehicleProfileManifest(
        string Schema,
        string ProfileId,
        string Name,
        string Version,
        VehiclePlatformKind PlatformKind,
        IReadOnlyList<string> Includes,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleProfileManifest Empty { get; } = new(
            Schema: "hydronom.vehicle-profile.v1",
            ProfileId: "empty",
            Name: "Empty Vehicle Profile",
            Version: "0.0.0",
            PlatformKind: VehiclePlatformKind.Unknown,
            Includes: Array.Empty<string>(),
            Tags: new Dictionary<string, string>());

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Schema) &&
            !string.IsNullOrWhiteSpace(ProfileId) &&
            Includes is not null;

        public VehicleProfileManifest Sanitized()
        {
            return this with
            {
                Schema = Clean(Schema, "hydronom.vehicle-profile.v1"),
                ProfileId = Clean(ProfileId, "unknown_vehicle_profile"),
                Name = Clean(Name, ProfileId),
                Version = Clean(Version, "0.0.0"),
                Includes = Includes?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public bool IncludesFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return Includes.Any(x =>
                string.Equals(x, fileName, StringComparison.OrdinalIgnoreCase));
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}