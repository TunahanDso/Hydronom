using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// C# runtime iÃ§indeki dijital ikiz/twin durumundan tÃ¼retilen IMU benzeri mesaj.
    ///
    /// AmaÃ§:
    /// - Python tarafÄ±ndaki csharp_sim IMU backend'ini beslemek
    /// - Runtime iÃ§ durumunu IMU sample formatÄ±na yakÄ±n bir yapÄ±yla dÄ±ÅŸarÄ± yayÄ±nlamak
    ///
    /// Notlar:
    /// - AÃ§Ä±sal hÄ±zlar rad/s cinsindendir.
    /// - Ä°vme alanlarÄ± m/sÂ² cinsindendir.
    /// - Roll/Pitch/Yaw aÃ§Ä±larÄ± derece cinsindendir.
    /// - TImu alanÄ± Unix epoch saniyesidir.
    /// - Ä°lk sÃ¼rÃ¼mde ax/ay/az iÃ§in basit deÄŸerler kullanÄ±labilir; asÄ±l kritik alanlar
    ///   gz, roll_deg, pitch_deg ve gerekirse yaw_deg bilgisidir.
    /// </summary>
    public sealed record TwinImuMessage
    {
        /// <summary>
        /// Mesaj tipi. Python TwinBus bunu "TwinImu" olarak bekler.
        /// </summary>
        public string Type { get; init; } = "TwinImu";

        /// <summary>
        /// Lineer ivme X [m/sÂ²]
        /// </summary>
        public double Ax { get; init; }

        /// <summary>
        /// Lineer ivme Y [m/sÂ²]
        /// </summary>
        public double Ay { get; init; }

        /// <summary>
        /// Lineer ivme Z [m/sÂ²]
        /// </summary>
        public double Az { get; init; }

        /// <summary>
        /// AÃ§Ä±sal hÄ±z X [rad/s]
        /// </summary>
        public double Gx { get; init; }

        /// <summary>
        /// AÃ§Ä±sal hÄ±z Y [rad/s]
        /// </summary>
        public double Gy { get; init; }

        /// <summary>
        /// AÃ§Ä±sal hÄ±z Z [rad/s]
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
        /// Roll aÃ§Ä±sÄ± [deg]
        /// </summary>
        public double RollDeg { get; init; }

        /// <summary>
        /// Pitch aÃ§Ä±sÄ± [deg]
        /// </summary>
        public double PitchDeg { get; init; }

        /// <summary>
        /// Yaw aÃ§Ä±sÄ± [deg]
        /// </summary>
        public double YawDeg { get; init; }

        /// <summary>
        /// IMU zamanÄ± benzeri epoch saniyesi.
        /// Python tarafÄ± bunu t_imu olarak kullanabilir.
        /// </summary>
        public double TImu { get; init; }

        /// <summary>
        /// Ä°steÄŸe baÄŸlÄ± kaynak etiketi.
        /// </summary>
        public string Source { get; init; } = "csharp-twin";

        /// <summary>
        /// Basit twin IMU mesajÄ± Ã¼retir.
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
