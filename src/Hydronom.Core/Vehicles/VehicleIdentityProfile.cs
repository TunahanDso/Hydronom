using System;

namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Araç profil paketinin kimlik bölümüdür.
    ///
    /// Bu model "bu araç kim?" sorusunu cevaplar.
    /// Fiziksel davranış, sensör, aktüatör veya safety bilgisi burada tutulmaz.
    /// </summary>
    public sealed record VehicleIdentityProfile(
        string ProfileId,
        string VehicleId,
        string DisplayName,
        string Manufacturer,
        string Model,
        string Revision,
        VehiclePlatformKind PlatformKind,
        string Role,
        string Description)
    {
        public static VehicleIdentityProfile Unknown { get; } = new(
            ProfileId: "unknown_vehicle_profile",
            VehicleId: "unknown-vehicle",
            DisplayName: "Unknown Vehicle",
            Manufacturer: "Unknown",
            Model: "Unknown",
            Revision: "0.0.0",
            PlatformKind: VehiclePlatformKind.Unknown,
            Role: "unknown",
            Description: "Unknown vehicle identity.");

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ProfileId) &&
            !string.IsNullOrWhiteSpace(VehicleId) &&
            PlatformKind != VehiclePlatformKind.Unknown;

        public VehicleIdentityProfile Sanitized()
        {
            return this with
            {
                ProfileId = Clean(ProfileId, "unknown_vehicle_profile"),
                VehicleId = Clean(VehicleId, "unknown-vehicle"),
                DisplayName = Clean(DisplayName, VehicleId),
                Manufacturer = Clean(Manufacturer, "Unknown"),
                Model = Clean(Model, "Unknown"),
                Revision = Clean(Revision, "0.0.0"),
                Role = Clean(Role, "vehicle"),
                Description = Clean(Description, string.Empty)
            };
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}