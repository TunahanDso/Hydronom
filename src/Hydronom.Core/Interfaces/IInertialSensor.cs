using System;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Interfaces
{
    /// <summary>
    /// IMU (Inertial Measurement Unit) sensÃ¶rleri iÃ§in ortak arayÃ¼z.
    /// Hem fiziksel sensÃ¶rler hem de simÃ¼lasyonlar bu arayÃ¼zÃ¼ uygular.
    /// </summary>
    public interface IInertialSensor : IDisposable
    {
        /// <summary>
        /// SensÃ¶rÃ¼n tanÄ±mlayÄ±cÄ± adÄ± (Ã¶rn. "MPU6050", "BNO055", "SimIMU").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// SensÃ¶rÃ¼n saniyede veri Ã¼retme oranÄ± (Hz).
        /// </summary>
        double SampleRateHz { get; }

        /// <summary>
        /// SensÃ¶r aktif mi (baÅŸlatÄ±lmÄ±ÅŸ mÄ±)?
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// SensÃ¶r Ã¶lÃ§Ã¼mÃ¼nÃ¼ baÅŸlatÄ±r (Ã¶rneÄŸin arka planda veri okumaya baÅŸlar).
        /// </summary>
        void Start();

        /// <summary>
        /// SensÃ¶r Ã¶lÃ§Ã¼mÃ¼nÃ¼ durdurur.
        /// </summary>
        void Stop();

        /// <summary>
        /// Tek seferlik Ã¶lÃ§Ã¼m (blocking) alÄ±r.
        /// </summary>
        /// <returns>
        /// Ã–rnek veri:
        ///  - Accel: m/sÂ²  
        ///  - Gyro : rad/s (SI birimi, ImuData ile tutarlÄ±)  
        ///  - Mag  : gauss veya sensÃ¶rÃ¼n ham manyetik birimi  
        ///  - Orientation: roll/pitch/yaw (deg)
        /// </returns>
        Task<InertialSample> ReadAsync();

        /// <summary>
        /// SensÃ¶r durumunu (Ã¶r. sÄ±caklÄ±k, baÄŸlantÄ±, hatalar) dÃ¶ner.
        /// </summary>
        /// <returns>SaÄŸlÄ±k bilgisi (HealthRecord)</returns>
        HealthRecord GetHealth();

        /// <summary>
        /// Kalibrasyon yapar (Ã¶rn. ofset veya Ã¶lÃ§ek dÃ¼zeltmeleri).
        /// </summary>
        void Calibrate();
    }

    /// <summary>
    /// IMU verisi iÃ§in temel kayÄ±t tipi.
    /// Accel: m/sÂ², Gyro: rad/s, Mag: gauss, Orientation: deg.
    /// </summary>
    public readonly record struct InertialSample(
        Vec3 Accel,           // m/sÂ² â€” ivmeÃ¶lÃ§er
        Vec3 Gyro,            // rad/s â€” jiroskop (SI birimi)
        Vec3 Mag,             // gauss â€” manyetometre (opsiyonel, yoksa 0 vektÃ¶rÃ¼)
        Orientation Orientation, // roll/pitch/yaw (deg)
        DateTime TimestampUtc
    )
    {
        public static InertialSample Zero =>
            new(Vec3.Zero, Vec3.Zero, Vec3.Zero, Orientation.Zero, DateTime.UtcNow);
    }

    /// <summary>
    /// SensÃ¶r saÄŸlÄ±ÄŸÄ± / baÄŸlantÄ± durumu.
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

