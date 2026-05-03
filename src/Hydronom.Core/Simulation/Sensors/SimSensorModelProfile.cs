using Hydronom.Core.Simulation.Faults;

namespace Hydronom.Core.Simulation.Sensors
{
    /// <summary>
    /// Sim sensÃ¶rÃ¼n Ã¶lÃ§Ã¼m Ã¼retme davranÄ±ÅŸÄ±nÄ± tek yerde toplayan profil.
    ///
    /// Noise, timing ve fault injection bilgisi birlikte tutulur.
    /// Sim IMU, GPS, LiDAR, kamera, sonar, encoder gibi sensÃ¶r backend'leri bu profili kullanabilir.
    /// </summary>
    public readonly record struct SimSensorModelProfile(
        string SensorId,
        string SensorKind,
        string FrameId,
        SimSensorNoiseProfile Noise,
        SimSensorTimingProfile Timing,
        SimSensorFaultProfile Faults,
        bool Enabled
    )
    {
        public static SimSensorModelProfile Create(
            string sensorId,
            string sensorKind,
            string frameId,
            SimSensorTimingProfile timing
        )
        {
            return new SimSensorModelProfile(
                SensorId: Normalize(sensorId, "sim_sensor0"),
                SensorKind: Normalize(sensorKind, "generic"),
                FrameId: Normalize(frameId, "base_link"),
                Noise: SimSensorNoiseProfile.Mild,
                Timing: timing.Sanitized(),
                Faults: SimSensorFaultProfile.None,
                Enabled: true
            );
        }

        public SimSensorModelProfile Sanitized()
        {
            return new SimSensorModelProfile(
                SensorId: Normalize(SensorId, "sim_sensor0"),
                SensorKind: Normalize(SensorKind, "generic"),
                FrameId: Normalize(FrameId, "base_link"),
                Noise: Noise.Sanitized(),
                Timing: Timing.Sanitized(),
                Faults: Faults.Sanitized(),
                Enabled: Enabled
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
