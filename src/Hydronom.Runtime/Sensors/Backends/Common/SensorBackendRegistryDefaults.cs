using Hydronom.Runtime.Sensors.Backends.Sim;
using Hydronom.Runtime.Sensors.Gps;
using Hydronom.Runtime.Sensors.Imu;
using Hydronom.Runtime.Sensors.Sim;

namespace Hydronom.Runtime.Sensors.Backends.Common;

/// <summary>
/// SensorBackendRegistry için varsayılan backend kayıtları.
///
/// Bu dosyanın amacı:
/// - Registry'nin sim sensör backend anahtarlarını bilmesini sağlamak
/// - Runtime builder'ın backend'leri string key üzerinden oluşturabilmesini sağlamak
/// - CSharpPrimary sensör runtime auto-wiring yolunu başlatmak
/// </summary>
public static class SensorBackendRegistryDefaults
{
    /// <summary>
    /// C# Primary simülasyon backend'lerini registry'ye ekler.
    ///
    /// Şimdilik:
    /// - sim_imu
    /// - sim_gps
    /// - sim_lidar
    ///
    /// Not:
    /// sim_lidar default factory ile worldModel/truthProvider olmadan da oluşturulabilir.
    /// Gerçek raycast doğrulaması için test içinde özel registry override edilerek
    /// truthProvider + RuntimeWorldModel verilmelidir.
    /// </summary>
    public static SensorBackendRegistry RegisterDefaultSimulationBackends(
        this SensorBackendRegistry registry,
        SimSensorClock? sharedClock = null,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(registry);

        /*
         * Aynı clock'u paylaşmaları önemli:
         * IMU/GPS/LiDAR farklı zaman gerçekliği üretmesin.
         */
        var clock = sharedClock ?? new SimSensorClock();

        registry.Register(
            key: "sim_imu",
            factory: _ => new SimImuSensor(clock: clock),
            replaceExisting: replaceExisting
        );

        registry.Register(
            key: "sim_gps",
            factory: _ => new SimGpsSensor(clock: clock),
            replaceExisting: replaceExisting
        );

        registry.Register(
            key: "sim_lidar",
            factory: _ => new SimLidarBackend(clock: clock),
            replaceExisting: replaceExisting
        );

        return registry;
    }
}