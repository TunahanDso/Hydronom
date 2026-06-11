using System;
using System.Collections.Generic;
using Hydronom.Core.Vehicles.Actuation;
using Hydronom.Core.Vehicles.Fleet;
using Hydronom.Core.Vehicles.Physics;
using Hydronom.Core.Vehicles.Safety;
using Hydronom.Core.Vehicles.Sensors;

namespace Hydronom.Core.Vehicles
{
    /// <summary>
    /// Hydronom'un typed ve birleşik araç profil modelidir.
    ///
    /// Senaryo runtime, simülasyon, ground station, control ve safety katmanları
    /// aracı bu model üzerinden tanır.
    ///
    /// Amaç:
    /// - Araç değiştirmek sadece geometry değiştirmek olmasın.
    /// - Aracın fiziksel gerçekliği, sensörleri, aktüatörleri, limitleri,
    ///   ortam uyumluluğu ve filo rolü tek bir profil üzerinden gelsin.
    /// </summary>
    public sealed record VehicleProfile(
        VehicleProfileManifest Manifest,
        VehicleIdentityProfile Identity,
        VehiclePhysicalProfile Physical,
        VehicleActuationProfile Actuation,
        VehicleSensorProfile Sensors,
        VehicleSimulationProfile Simulation,
        VehicleSafetyProfile Safety,
        VehicleFleetRoleProfile? FleetRole,
        VehicleParentChildLinkProfile? ParentChildLink)
    {
        public string ProfileId => Identity.ProfileId;
        public string VehicleId => Identity.VehicleId;
        public string DisplayName => Identity.DisplayName;
        public VehiclePlatformKind PlatformKind => Identity.PlatformKind;

        public bool IsUnderwater =>
            PlatformKind is VehiclePlatformKind.UnderwaterVehicle or VehiclePlatformKind.MiniRov;

        public bool IsSurface =>
            PlatformKind == VehiclePlatformKind.SurfaceVessel;

        public bool IsMiniRov =>
            PlatformKind == VehiclePlatformKind.MiniRov;

        public bool IsFleetChild =>
            ParentChildLink is not null &&
            !string.IsNullOrWhiteSpace(ParentChildLink.ParentVehicleId);

        public bool CanCarryChildVehicle =>
            FleetRole is not null &&
            FleetRole.CanCarryChildVehicle;

        public bool HasActiveThrusters =>
            Actuation is not null &&
            Actuation.HasAnyActiveThruster;

        public bool HasDepthSensor =>
            Sensors is not null &&
            Sensors.HasDepthSensor;

        public bool HasCamera =>
            Sensors is not null &&
            Sensors.HasCamera;

        public bool SupportsUnderwaterPhysics =>
            IsUnderwater &&
            Simulation is not null &&
            Simulation.IsUnderwater;

        public VehicleProfile Sanitized()
        {
            return this with
            {
                Manifest = (Manifest ?? VehicleProfileManifest.Empty).Sanitized(),
                Identity = (Identity ?? VehicleIdentityProfile.Unknown).Sanitized(),
                Physical = (Physical ?? VehiclePhysicalProfile.Unknown).Sanitized(),
                Actuation = (Actuation ?? VehicleActuationProfile.Empty).Sanitized(),
                Sensors = (Sensors ?? VehicleSensorProfile.Empty).Sanitized(),
                Simulation = (Simulation ?? VehicleSimulationProfile.Unknown).Sanitized(),
                Safety = (Safety ?? VehicleSafetyProfile.Conservative).Sanitized(),
                FleetRole = FleetRole?.Sanitized(),
                ParentChildLink = ParentChildLink?.Sanitized()
            };
        }

        public VehicleProfileValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (Manifest is null || !Manifest.IsValid)
                errors.Add("Vehicle profile manifest is invalid.");

            if (Identity is null || !Identity.IsValid)
                errors.Add("Vehicle identity profile is invalid.");

            if (Physical is null)
                errors.Add("Vehicle physical profile is missing.");
            else if (!Physical.IsValid)
                errors.Add("Vehicle physical profile is invalid.");

            if (Actuation is null)
                errors.Add("Vehicle actuation profile is missing.");
            else if (Actuation.Enabled && !Actuation.HasAnyActiveThruster)
                warnings.Add("Vehicle actuation is enabled but no active thruster was found.");

            if (Sensors is null)
                warnings.Add("Vehicle sensor profile is missing.");
            else if (Sensors.Enabled && !Sensors.HasAnySensor)
                warnings.Add("Vehicle sensor profile is enabled but no active sensor was found.");

            if (Simulation is null)
                errors.Add("Vehicle simulation profile is missing.");

            if (Safety is null)
                warnings.Add("Vehicle safety profile is missing.");

            if (Identity is not null &&
                Manifest is not null &&
                !string.Equals(Identity.ProfileId, Manifest.ProfileId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"ProfileId mismatch. Manifest='{Manifest.ProfileId}', Identity='{Identity.ProfileId}'.");
            }

            if (IsUnderwater && Sensors is not null && !Sensors.HasDepthSensor)
                warnings.Add("Underwater vehicle profile has no depth/pressure sensor.");

            if (IsUnderwater && Physical is not null && Physical.Buoyancy is not null && !Physical.Buoyancy.Enabled)
                warnings.Add("Underwater vehicle profile has buoyancy disabled.");

            if (IsMiniRov && ParentChildLink is null)
                warnings.Add("Mini ROV profile has no parent-child link profile.");

            if (ParentChildLink is not null &&
                ParentChildLink.IsLinked &&
                !string.Equals(ParentChildLink.ChildVehicleId, VehicleId, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Parent-child link ChildVehicleId='{ParentChildLink.ChildVehicleId}' does not match profile VehicleId='{VehicleId}'.");
            }

            return VehicleProfileValidationResult.From(errors, warnings);
        }
    }
}