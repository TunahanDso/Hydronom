namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// DÃ¼nya nesneleri iÃ§in gÃ¶rsel/fiziksel materyal tanÄ±mÄ±.
    ///
    /// Bu model Ops Ã§izimi, sim sensÃ¶r tepkisi ve ileride fizik/Ã§arpÄ±ÅŸma modellemesi iÃ§in
    /// ortak bir aÃ§Ä±klama taÅŸÄ±r.
    /// </summary>
    public readonly record struct SimWorldMaterial(
        string MaterialId,
        string DisplayName,
        string ColorHex,
        double Opacity,
        bool IsCollidable,
        bool IsDetectable,
        bool IsReflective,
        bool IsTransparent,
        double Roughness,
        double SensorReflectivity
    )
    {
        public static SimWorldMaterial Default => new(
            MaterialId: "default",
            DisplayName: "Default",
            ColorHex: "#9CA3AF",
            Opacity: 1.0,
            IsCollidable: true,
            IsDetectable: true,
            IsReflective: false,
            IsTransparent: false,
            Roughness: 0.5,
            SensorReflectivity: 0.5
        );

        public static SimWorldMaterial Water => new(
            MaterialId: "water",
            DisplayName: "Water",
            ColorHex: "#38BDF8",
            Opacity: 0.45,
            IsCollidable: false,
            IsDetectable: true,
            IsReflective: true,
            IsTransparent: true,
            Roughness: 0.15,
            SensorReflectivity: 0.25
        );

        public static SimWorldMaterial Obstacle => new(
            MaterialId: "obstacle",
            DisplayName: "Obstacle",
            ColorHex: "#EF4444",
            Opacity: 1.0,
            IsCollidable: true,
            IsDetectable: true,
            IsReflective: true,
            IsTransparent: false,
            Roughness: 0.8,
            SensorReflectivity: 0.9
        );

        public static SimWorldMaterial MissionTarget => new(
            MaterialId: "mission_target",
            DisplayName: "Mission Target",
            ColorHex: "#22C55E",
            Opacity: 1.0,
            IsCollidable: false,
            IsDetectable: true,
            IsReflective: true,
            IsTransparent: false,
            Roughness: 0.4,
            SensorReflectivity: 0.7
        );

        public SimWorldMaterial Sanitized()
        {
            return new SimWorldMaterial(
                MaterialId: Normalize(MaterialId, "default"),
                DisplayName: Normalize(DisplayName, "Default"),
                ColorHex: NormalizeColor(ColorHex),
                Opacity: Clamp01(Opacity),
                IsCollidable: IsCollidable,
                IsDetectable: IsDetectable,
                IsReflective: IsReflective,
                IsTransparent: IsTransparent,
                Roughness: Clamp01(Roughness),
                SensorReflectivity: Clamp01(SensorReflectivity)
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "#9CA3AF";

            var color = value.Trim();

            if (!color.StartsWith("#"))
                color = "#" + color;

            return color.Length == 7 ? color : "#9CA3AF";
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}
