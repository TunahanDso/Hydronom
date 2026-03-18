using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// C# runtime içindeki dijital ikiz/twin durumundan türetilen IMU benzeri mesaj.
    ///
    /// Amaç:
    /// - Python tarafındaki csharp_sim IMU backend'ini beslemek
    /// - Runtime iç durumunu IMU sample formatına yakın bir yapıyla dışarı yayınlamak
    ///
    /// Notlar:
    /// - Açısal hızlar rad/s cinsindendir.
    /// - İvme alanları m/s² cinsindendir.
    /// - Roll/Pitch/Yaw açıları derece cinsindendir.
    /// - TImu alanı Unix epoch saniyesidir.
    /// - İlk sürümde ax/ay/az için basit değerler kullanılabilir; asıl kritik alanlar
    ///   gz, roll_deg, pitch_deg ve gerekirse yaw_deg bilgisidir.
    /// </summary>
    public sealed record TwinImuMessage
    {
        /// <summary>
        /// Mesaj tipi. Python TwinBus bunu "TwinImu" olarak bekler.
        /// </summary>
        public string Type { get; init; } = "TwinImu";

        /// <summary>
        /// Lineer ivme X [m/s²]
        /// </summary>
        public double Ax { get; init; }

        /// <summary>
        /// Lineer ivme Y [m/s²]
        /// </summary>
        public double Ay { get; init; }

        /// <summary>
        /// Lineer ivme Z [m/s²]
        /// </summary>
        public double Az { get; init; }

        /// <summary>
        /// Açısal hız X [rad/s]
        /// </summary>
        public double Gx { get; init; }

        /// <summary>
        /// Açısal hız Y [rad/s]
        /// </summary>
        public double Gy { get; init; }

        /// <summary>
        /// Açısal hız Z [rad/s]
        /// </summary>
        public double Gz { get; init; }

        /// <summary>
        /// Manyetometre X (opsiyonel)
        /// </summary>
        public double? Mx { get; init; }

        /// <summary>
        /// Manyetometre Y (opsiyonel)
        /// </summary>
        public double? My { get; init; }

        /// <summary>
        /// Manyetometre Z (opsiyonel)
        /// </summary>
        public double? Mz { get; init; }

        /// <summary>
        /// Roll açısı [deg]
        /// </summary>
        public double RollDeg { get; init; }

        /// <summary>
        /// Pitch açısı [deg]
        /// </summary>
        public double PitchDeg { get; init; }

        /// <summary>
        /// Yaw açısı [deg]
        /// </summary>
        public double YawDeg { get; init; }

        /// <summary>
        /// IMU zamanı benzeri epoch saniyesi.
        /// Python tarafı bunu t_imu olarak kullanabilir.
        /// </summary>
        public double TImu { get; init; }

        /// <summary>
        /// İsteğe bağlı kaynak etiketi.
        /// </summary>
        public string Source { get; init; } = "csharp-twin";

        /// <summary>
        /// Basit twin IMU mesajı üretir.
        /// </summary>
        public static TwinImuMessage Create(
            double gx,
            double gy,
            double gz,
            double rollDeg,
            double pitchDeg,
            double yawDeg,
            double ax = 0.0,
            double ay = 0.0,
            double az = 0.0,
            string source = "csharp-twin")
        {
            return new TwinImuMessage
            {
                Ax = ax,
                Ay = ay,
                Az = az,
                Gx = gx,
                Gy = gy,
                Gz = gz,
                RollDeg = rollDeg,
                PitchDeg = pitchDeg,
                YawDeg = yawDeg,
                TImu = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                Source = source
            };
        }
    }
}