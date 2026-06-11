using Hydronom.Core.Simulation.Environment;

namespace Hydronom.Core.Vehicles.Physics
{
    /// <summary>
    /// Aracın simülasyonda hangi fiziksel ortam ve modelle çalışacağını tanımlar.
    ///
    /// Örnek:
    /// - SurfaceWater: tekne / su üstü araç
    /// - Underwater: UUV / Mini ROV
    /// - Ground: kara aracı
    /// - Air: hava aracı
    /// </summary>
    public sealed record VehicleSimulationProfile(
        SimMediumKind Medium,
        string PhysicsModel,
        double DefaultTimeStepSeconds,
        double MaxSimulationSpeedMps,
        double MaxSimulationAngularSpeedDegps,
        bool EnableGravity,
        bool EnableBuoyancy,
        bool EnableHydrodynamicDrag,
        bool EnableCollision,
        bool EnableTetherSimulation)
    {
        public static VehicleSimulationProfile Unknown { get; } = new(
            Medium: SimMediumKind.Unknown,
            PhysicsModel: "unknown",
            DefaultTimeStepSeconds: 0.02,
            MaxSimulationSpeedMps: 1.0,
            MaxSimulationAngularSpeedDegps: 90.0,
            EnableGravity: true,
            EnableBuoyancy: false,
            EnableHydrodynamicDrag: false,
            EnableCollision: true,
            EnableTetherSimulation: false);

        public bool IsUnderwater => Medium == SimMediumKind.Underwater;
        public bool IsSurfaceWater => Medium == SimMediumKind.SurfaceWater;

        public VehicleSimulationProfile Sanitized()
        {
            return this with
            {
                PhysicsModel = Clean(PhysicsModel, "generic"),
                DefaultTimeStepSeconds = Clamp(DefaultTimeStepSeconds, 0.001, 0.25, 0.02),
                MaxSimulationSpeedMps = Clamp(MaxSimulationSpeedMps, 0.05, 50.0, 1.0),
                MaxSimulationAngularSpeedDegps = Clamp(MaxSimulationAngularSpeedDegps, 1.0, 720.0, 90.0)
            };
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private static double Clamp(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }
    }
}