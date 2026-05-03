using System;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// C# runtime iÃ§indeki dijital ikiz/twin durumundan tÃ¼retilen GPS benzeri mesaj.
    ///
    /// AmaÃ§:
    /// - Python tarafÄ±ndaki csharp_sim GPS backend'ini beslemek
    /// - Runtime iÃ§ durumunu GPS fix formatÄ±na yakÄ±n bir yapÄ±yla dÄ±ÅŸarÄ± yayÄ±nlamak
    ///
    /// Notlar:
    /// - Lat/Lon alanlarÄ± WGS84 derece cinsindendir.
    /// - Alt metre cinsindendir.
    /// - Fix alanÄ± basit durum kodudur:
    ///   0 = no-fix
    ///   1 = GPS
    ///   2 = DGPS
    ///   3 = yÃ¼ksek gÃ¼ven / sim-twin fix
    /// - t_gps alanÄ± Unix epoch saniyesidir.
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
        /// Ä°rtifa [m]
        /// </summary>
        public double Alt { get; init; }

        /// <summary>
        /// GPS fix seviyesi.
        /// Twin senaryosunda varsayÄ±lan olarak 3 verilebilir.
        /// </summary>
        public int Fix { get; init; } = 3;

        /// <summary>
        /// HDOP benzeri kalite deÄŸeri.
        /// Twin senaryosunda sabit kÃ¼Ã§Ã¼k bir deÄŸer kullanÄ±labilir.
        /// </summary>
        public double Hdop { get; init; } = 0.7;

        /// <summary>
        /// GPS zamanÄ± benzeri epoch saniyesi.
        /// Python tarafÄ± bunu t_gps olarak kullanÄ±r.
        /// </summary>
        public double TGps { get; init; }

        /// <summary>
        /// Ä°steÄŸe baÄŸlÄ± kaynak etiketi.
        /// Debug ve log iÃ§in faydalÄ±dÄ±r.
        /// </summary>
        public string Source { get; init; } = "csharp-twin";

        /// <summary>
        /// Mevcut UTC zamandan, basit bir twin GPS mesajÄ± Ã¼retir.
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
