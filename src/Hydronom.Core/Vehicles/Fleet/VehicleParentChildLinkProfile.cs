using System;

namespace Hydronom.Core.Vehicles.Fleet
{
    /// <summary>
    /// Ana araç - çocuk araç ilişkisini tanımlar.
    ///
    /// Su altı yarışması için örnek:
    /// - Parent: hydronom-uuv-main
    /// - Child: hydronom-mini-rov
    ///
    /// Bu ilişki sadece görsel bir bağlantı değildir:
    /// - Mini ROV nereden deploy edilir?
    /// - Haberleşme ana araç üzerinden mi gider?
    /// - Tether uzunluğu nedir?
    /// - Çocuk araç parent olmadan çalışabilir mi?
    /// gibi operasyon kurallarını taşır.
    /// </summary>
    public sealed record VehicleParentChildLinkProfile(
        string ParentVehicleId,
        string ChildVehicleId,
        string LinkType,
        bool IsDeployable,
        bool RequiresParentOnline,
        bool RoutesCommunicationThroughParent,
        double MaxSeparationM,
        double TetherLengthM,
        IReadOnlyDictionary<string, string> Tags)
    {
        public static VehicleParentChildLinkProfile None { get; } = new(
            ParentVehicleId: string.Empty,
            ChildVehicleId: string.Empty,
            LinkType: "none",
            IsDeployable: false,
            RequiresParentOnline: false,
            RoutesCommunicationThroughParent: false,
            MaxSeparationM: 0.0,
            TetherLengthM: 0.0,
            Tags: new Dictionary<string, string>());

        public bool IsLinked =>
            !string.IsNullOrWhiteSpace(ParentVehicleId) &&
            !string.IsNullOrWhiteSpace(ChildVehicleId) &&
            !string.Equals(LinkType, "none", StringComparison.OrdinalIgnoreCase);

        public VehicleParentChildLinkProfile Sanitized()
        {
            return this with
            {
                ParentVehicleId = Clean(ParentVehicleId, string.Empty),
                ChildVehicleId = Clean(ChildVehicleId, string.Empty),
                LinkType = Clean(LinkType, "none"),
                MaxSeparationM = SafePositive(MaxSeparationM),
                TetherLengthM = SafePositive(TetherLengthM),
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
    }
}