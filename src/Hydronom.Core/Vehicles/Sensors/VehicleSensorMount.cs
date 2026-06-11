using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Vehicles.Sensors
{
    /// <summary>
    /// Araç üzerindeki tek bir sensörün montaj ve kabiliyet profilidir.
    ///
    /// PositionM:
    /// - Sensörün gövde koordinat sistemindeki konumu.
    ///
    /// RotationDeg:
    /// - Sensörün gövdeye göre roll/pitch/yaw yönelimi.
    ///
    /// FieldOfViewDeg:
    /// - Kamera/sonar/lidar gibi görüş alanı olan sensörler için kullanılabilir.
    ///
    /// UpdateRateHz:
    /// - Sensörün nominal veri üretim frekansı.
    /// </summary>
    public sealed record VehicleSensorMount(
        string Id,
        string Name,
        VehicleSensorKind Kind,
        Vec3 PositionM,
        Vec3 RotationDeg,
        double FieldOfViewDeg,
        double RangeM,
        double UpdateRateHz,
        bool IsPrimary,
        bool IsRequiredForAutonomy,
        bool IsEnabled,
        string SourceName,
        IReadOnlyDictionary<string, string> Tags)
    {
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Id) &&
            Kind != VehicleSensorKind.Unknown &&
            IsEnabled;

        public VehicleSensorMount Sanitized()
        {
            return this with
            {
                Id = Clean(Id, "sensor"),
                Name = Clean(Name, Id),
                PositionM = SanitizeVec(PositionM),
                RotationDeg = SanitizeVec(RotationDeg),
                FieldOfViewDeg = Clamp(FieldOfViewDeg, 0.0, 360.0, 0.0),
                RangeM = SafePositive(RangeM),
                UpdateRateHz = SafePositive(UpdateRateHz),
                SourceName = Clean(SourceName, string.Empty),
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

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
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