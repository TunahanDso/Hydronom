using System;

namespace Hydronom.Core.Vehicles.Sensors
{
    /// <summary>
    /// Aracın tüm sensör profilidir.
    ///
    /// Bu profil:
    /// - Görev uyumluluğu
    /// - Simülasyon sensör üretimi
    /// - Ground Station araç kartları
    /// - Safety / failsafe kontrolleri
    /// için kullanılır.
    /// </summary>
    public sealed record VehicleSensorProfile(
        bool Enabled,
        IReadOnlyList<VehicleSensorMount> Sensors,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleSensorProfile Empty { get; } = new(
            Enabled: false,
            Sensors: Array.Empty<VehicleSensorMount>(),
            Tags: new Dictionary<string, string>());

        public IReadOnlyList<VehicleSensorMount> ActiveSensors =>
            Sensors?
                .Where(x => x is not null && x.Sanitized().IsValid)
                .Select(x => x.Sanitized())
                .ToArray()
            ?? Array.Empty<VehicleSensorMount>();

        public bool HasAnySensor => ActiveSensors.Count > 0;

        public bool HasImu =>
            HasAny(
                VehicleSensorKind.Imu,
                VehicleSensorKind.Gyroscope,
                VehicleSensorKind.Accelerometer);

        public bool HasGps =>
            HasAny(
                VehicleSensorKind.Gps,
                VehicleSensorKind.Gnss,
                VehicleSensorKind.RtkGps);

        public bool HasDepthSensor =>
            HasAny(
                VehicleSensorKind.DepthSensor,
                VehicleSensorKind.PressureSensor);

        public bool HasCamera =>
            HasAny(
                VehicleSensorKind.Camera,
                VehicleSensorKind.StereoCamera,
                VehicleSensorKind.ThermalCamera);

        public bool HasSonar =>
            HasAny(
                VehicleSensorKind.Sonar,
                VehicleSensorKind.ImagingSonar,
                VehicleSensorKind.Dvl);

        public bool HasLeakSensor =>
            HasAny(VehicleSensorKind.LeakSensor);

        public VehicleSensorProfile Sanitized()
        {
            return this with
            {
                Sensors = Sensors?
                    .Where(x => x is not null)
                    .Select(x => x.Sanitized())
                    .ToArray()
                    ?? Array.Empty<VehicleSensorMount>(),
                Tags = Tags?
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .ToDictionary(
                        x => x.Key.Trim(),
                        x => x.Value?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public bool HasAny(params VehicleSensorKind[] kinds)
        {
            if (kinds is null || kinds.Length == 0)
                return false;

            return ActiveSensors.Any(sensor =>
                kinds.Contains(sensor.Kind));
        }

        public VehicleSensorMount? GetPrimary(VehicleSensorKind kind)
        {
            return ActiveSensors
                .Where(x => x.Kind == kind)
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.UpdateRateHz)
                .FirstOrDefault();
        }

        public IReadOnlyList<VehicleSensorMount> GetByKind(VehicleSensorKind kind)
        {
            return ActiveSensors
                .Where(x => x.Kind == kind)
                .ToArray();
        }
    }
}