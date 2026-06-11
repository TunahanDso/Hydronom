using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Actuation
{
    /// <summary>
    /// Tek bir thruster / motor / itki elemanının profilidir.
    ///
    /// PositionM:
    /// - Gövde koordinat sistemine göre motorun konumu.
    ///
    /// Direction:
    /// - Motorun pozitif komutta kuvvet uyguladığı yön.
    /// - Örnek:
    ///   X ileri, Y sağ, Z yukarı kabul edilirse:
    ///   ileri motor için Direction = (1, 0, 0)
    ///   dikey motor için Direction = (0, 0, 1)
    ///
    /// CanReverse:
    /// - ESC/motor fiziksel olarak ters itki üretebiliyor mu?
    /// </summary>
    public sealed record VehicleThrusterProfile(
        string Id,
        string Name,
        VehicleActuatorKind Kind,
        Vec3 PositionM,
        Vec3 Direction,
        double MaxForwardThrustN,
        double MaxReverseThrustN,
        bool CanReverse,
        bool IsEnabled,
        string Channel,
        IReadOnlyDictionary<string, string> Tags)
    {
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Id) &&
            Kind is VehicleActuatorKind.Thruster or VehicleActuatorKind.Propeller &&
            IsEnabled &&
            MaxForwardThrustN > 0.0 &&
            DirectionMagnitude > 1e-6;

        public double DirectionMagnitude =>
            Math.Sqrt(
                Direction.X * Direction.X +
                Direction.Y * Direction.Y +
                Direction.Z * Direction.Z);

        public Vec3 NormalizedDirection
        {
            get
            {
                var mag = DirectionMagnitude;

                if (!double.IsFinite(mag) || mag <= 1e-6)
                    return Vec3.Zero;

                return new Vec3(
                    Direction.X / mag,
                    Direction.Y / mag,
                    Direction.Z / mag);
            }
        }

        public VehicleThrusterProfile Sanitized()
        {
            return this with
            {
                Id = Clean(Id, "thruster"),
                Name = Clean(Name, Id),
                Kind = Kind == VehicleActuatorKind.Unknown
                    ? VehicleActuatorKind.Thruster
                    : Kind,
                PositionM = SanitizeVec(PositionM),
                Direction = SanitizeVec(Direction),
                MaxForwardThrustN = SafePositive(MaxForwardThrustN),
                MaxReverseThrustN = CanReverse
                    ? SafePositive(MaxReverseThrustN)
                    : 0.0,
                Channel = Clean(Channel, string.Empty),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private static double SafePositive(double value)
        {
            return double.IsFinite(value)
                ? Math.Max(0.0, value)
                : 0.0;
        }

        private static Vec3 SanitizeVec(Vec3 value)
        {
            return new Vec3(
                double.IsFinite(value.X) ? value.X : 0.0,
                double.IsFinite(value.Y) ? value.Y : 0.0,
                double.IsFinite(value.Z) ? value.Z : 0.0);
        }
    }
}