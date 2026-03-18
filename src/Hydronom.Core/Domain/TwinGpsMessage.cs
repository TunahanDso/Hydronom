using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// C# runtime içindeki dijital ikiz/twin durumundan türetilen GPS benzeri mesaj.
    ///
    /// Amaç:
    /// - Python tarafındaki csharp_sim GPS backend'ini beslemek
    /// - Runtime iç durumunu GPS fix formatına yakın bir yapıyla dışarı yayınlamak
    ///
    /// Notlar:
    /// - Lat/Lon alanları WGS84 derece cinsindendir.
    /// - Alt metre cinsindendir.
    /// - Fix alanı basit durum kodudur:
    ///   0 = no-fix
    ///   1 = GPS
    ///   2 = DGPS
    ///   3 = yüksek güven / sim-twin fix
    /// - t_gps alanı Unix epoch saniyesidir.
    /// </summary>
    public sealed record TwinGpsMessage
    {
        /// <summary>
        /// Mesaj tipi. Python TwinBus bunu "TwinGps" olarak bekler.
        /// </summary>
        public string Type { get; init; } = "TwinGps";

        /// <summary>
        /// Enlem [deg]
        /// </summary>
        public double Lat { get; init; }

        /// <summary>
        /// Boylam [deg]
        /// </summary>
        public double Lon { get; init; }

        /// <summary>
        /// İrtifa [m]
        /// </summary>
        public double Alt { get; init; }

        /// <summary>
        /// GPS fix seviyesi.
        /// Twin senaryosunda varsayılan olarak 3 verilebilir.
        /// </summary>
        public int Fix { get; init; } = 3;

        /// <summary>
        /// HDOP benzeri kalite değeri.
        /// Twin senaryosunda sabit küçük bir değer kullanılabilir.
        /// </summary>
        public double Hdop { get; init; } = 0.7;

        /// <summary>
        /// GPS zamanı benzeri epoch saniyesi.
        /// Python tarafı bunu t_gps olarak kullanır.
        /// </summary>
        public double TGps { get; init; }

        /// <summary>
        /// İsteğe bağlı kaynak etiketi.
        /// Debug ve log için faydalıdır.
        /// </summary>
        public string Source { get; init; } = "csharp-twin";

        /// <summary>
        /// Mevcut UTC zamandan, basit bir twin GPS mesajı üretir.
        /// </summary>
        public static TwinGpsMessage Create(
            double lat,
            double lon,
            double alt,
            int fix = 3,
            double hdop = 0.7,
            string source = "csharp-twin")
        {
            return new TwinGpsMessage
            {
                Lat = lat,
                Lon = lon,
                Alt = alt,
                Fix = fix,
                Hdop = hdop,
                TGps = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                Source = source
            };
        }
    }
}