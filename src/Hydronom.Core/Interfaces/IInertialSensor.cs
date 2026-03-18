using System;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// IMU (Inertial Measurement Unit) sensörleri için ortak arayüz.
    /// Hem fiziksel sensörler hem de simülasyonlar bu arayüzü uygular.
    /// </summary>
    public interface IInertialSensor : IDisposable
    {
        /// <summary>
        /// Sensörün tanımlayıcı adı (örn. "MPU6050", "BNO055", "SimIMU").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sensörün saniyede veri üretme oranı (Hz).
        /// </summary>
        double SampleRateHz { get; }

        /// <summary>
        /// Sensör aktif mi (başlatılmış mı)?
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Sensör ölçümünü başlatır (örneğin arka planda veri okumaya başlar).
        /// </summary>
        void Start();

        /// <summary>
        /// Sensör ölçümünü durdurur.
        /// </summary>
        void Stop();

        /// <summary>
        /// Tek seferlik ölçüm (blocking) alır.
        /// </summary>
        /// <returns>
        /// Örnek veri:
        ///  - Accel: m/s²  
        ///  - Gyro : rad/s (SI birimi, ImuData ile tutarlı)  
        ///  - Mag  : gauss veya sensörün ham manyetik birimi  
        ///  - Orientation: roll/pitch/yaw (deg)
        /// </returns>
        Task<InertialSample> ReadAsync();

        /// <summary>
        /// Sensör durumunu (ör. sıcaklık, bağlantı, hatalar) döner.
        /// </summary>
        /// <returns>Sağlık bilgisi (HealthRecord)</returns>
        HealthRecord GetHealth();

        /// <summary>
        /// Kalibrasyon yapar (örn. ofset veya ölçek düzeltmeleri).
        /// </summary>
        void Calibrate();
    }

    /// <summary>
    /// IMU verisi için temel kayıt tipi.
    /// Accel: m/s², Gyro: rad/s, Mag: gauss, Orientation: deg.
    /// </summary>
    public readonly record struct InertialSample(
        Vec3 Accel,           // m/s² — ivmeölçer
        Vec3 Gyro,            // rad/s — jiroskop (SI birimi)
        Vec3 Mag,             // gauss — manyetometre (opsiyonel, yoksa 0 vektörü)
        Orientation Orientation, // roll/pitch/yaw (deg)
        DateTime TimestampUtc
    )
    {
        public static InertialSample Zero =>
            new(Vec3.Zero, Vec3.Zero, Vec3.Zero, Orientation.Zero, DateTime.UtcNow);
    }

    /// <summary>
    /// Sensör sağlığı / bağlantı durumu.
    /// </summary>
    public readonly record struct HealthRecord(
        bool IsOnline,
        double TemperatureC,
        string Status,
        DateTime LastUpdateUtc
    )
    {
        public static HealthRecord Default =>
            new(true, 25.0, "OK", DateTime.UtcNow);
    }
}
