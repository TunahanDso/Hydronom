using Hydronom.Runtime.Sensors.Gps;
using Hydronom.Runtime.Sensors.Imu;
using Hydronom.Runtime.Sensors.Sim;

namespace Hydronom.Runtime.Sensors.Backends.Common;

/// <summary>
/// SensorBackendRegistry için varsayılan backend kayıtları.
///
/// Bu dosyanın amacı:
/// - Registry'nin "sim_imu" ve "sim_gps" anahtarlarını bilmesini sağlamak
/// - Runtime builder'ın backend'leri string key üzerinden oluşturabilmesini sağlamak
/// - CSharpPrimary sensör runtime auto-wiring yolunu başlatmak
///
/// Not:
/// SimImuSensor ve SimGpsSensor şu an hâlâ procedural sim veri üretir.
/// Bir sonraki fazda bu sensörler PhysicsTruthProvider üzerinden beslenecek.
/// </summary>
public static class SensorBackendRegistryDefaults
{
    /// <summary>
    /// C# Primary simülasyon backend'lerini registry'ye ekler.
    ///
    /// Şimdilik:
    /// - sim_imu
    /// - sim_gps
    ///
    /// İleride:
    /// - sim_lidar
    /// - sim_camera
    /// - sim_depth
    /// - sim_sonar
    /// - sim_dvl
    /// gibi backend'ler de buraya eklenecek.
    /// </summary>
    public static SensorBackendRegistry RegisterDefaultSimulationBackends(
        this SensorBackendRegistry registry,
        SimSensorClock? sharedClock = null,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(registry);

        /*
         * Aynı clock'u paylaşmaları önemli:
         * IMU ve GPS farklı zaman gerçekliği üretmesin.
         * Şu an procedural simde bile ortak sim zamanı kullanılır.
         * PhysicsTruthProvider'a geçtiğimizde de bu disiplin korunacak.
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

        return registry;
    }
}