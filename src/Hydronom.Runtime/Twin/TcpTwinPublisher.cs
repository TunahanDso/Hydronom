using System;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Twin
{
    /// <summary>
    /// Runtime iÃ§indeki VehicleState bilgisini TwinGps ve TwinImu mesajlarÄ±na Ã§evirip
    /// TcpJsonServer Ã¼zerinden Python tarafÄ±na yayÄ±nlayan kÃ¶prÃ¼.
    ///
    /// TasarÄ±m hedefleri:
    /// - Python csharp_sim backend'lerinin beklediÄŸi alan adlarÄ±yla uyumlu JSON Ã¼retmek
    /// - GPS ve IMU yayÄ±n hÄ±zlarÄ±nÄ± ayrÄ± ayrÄ± kontrol edebilmek
    /// - VehicleState Ã¼zerinden basit ama tutarlÄ± twin veri Ã¼retmek
    /// - Åimdilik dÄ±ÅŸ baÄŸÄ±mlÄ±lÄ±ÄŸÄ± minimum tutmak
    ///
    /// Notlar:
    /// - GPS tarafÄ±nda Python TwinBus kÃ¼Ã§Ã¼k harfli alanlar beklediÄŸi iÃ§in burada
    ///   kÃ¼Ã§Ã¼k harfli anonymous object Ã¼retilir.
    /// - IMU tarafÄ±nda da benzer ÅŸekilde kÃ¼Ã§Ã¼k harfli alanlar Ã¼retilir.
    /// - Konum, referans enlem/boylam etrafÄ±nda yerel XY -> lat/lon dÃ¶nÃ¼ÅŸÃ¼mÃ¼ ile yayÄ±nlanÄ±r.
    /// - AngularVelocity VehicleState iÃ§inde deg/s tutulduÄŸu iÃ§in TwinImu'da rad/s'e Ã§evrilir.
    /// </summary>
    public sealed class TcpTwinPublisher : ITwinPublisher
    {
        private readonly dynamic _server;

        /// <summary>
        /// Twin GPS iÃ§in referans enlem [deg]
        /// </summary>
        public double ReferenceLatDeg { get; set; } = 41.0224;

        /// <summary>
        /// Twin GPS iÃ§in referans boylam [deg]
        /// </summary>
        public double ReferenceLonDeg { get; set; } = 28.8321;

        /// <summary>
        /// Twin GPS iÃ§in referans irtifa [m]
        /// </summary>
        public double ReferenceAltM { get; set; } = 0.0;

        /// <summary>
        /// GPS yayÄ±n frekansÄ± [Hz]
        /// </summary>
        public double GpsRateHz { get; set; } = 5.0;

        /// <summary>
        /// IMU yayÄ±n frekansÄ± [Hz]
        /// </summary>
        public double ImuRateHz { get; set; } = 20.0;

        /// <summary>
        /// Twin GPS fix seviyesi
        /// </summary>
        public int GpsFix { get; set; } = 3;

        /// <summary>
        /// Twin GPS hdop deÄŸeri
        /// </summary>
        public double GpsHdop { get; set; } = 0.7;

        /// <summary>
        /// YayÄ±n etiketi
        /// </summary>
        public string SourceName { get; set; } = "csharp-twin";

        private DateTime _lastGpsUtc = DateTime.MinValue;
        private DateTime _lastImuUtc = DateTime.MinValue;

        private const double EarthMetersPerDegLat = 111_320.0;

        public TcpTwinPublisher(object tcpJsonServer)
        {
            _server = tcpJsonServer ?? throw new ArgumentNullException(nameof(tcpJsonServer));
        }

        /// <summary>
        /// Mevcut state'ten gerekli twin mesajlarÄ±nÄ± Ã¼retir ve frekans sÄ±nÄ±rlarÄ±na gÃ¶re yayÄ±nlar.
        /// </summary>
        public async Task PublishAsync(VehicleState state, CancellationToken ct = default)
        {
            Console.WriteLine($"[TWIN-DBG] pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) yaw={state.Orientation.YawDeg:F1}");
            if (ct.IsCancellationRequested)
                return;

            var nowUtc = DateTime.UtcNow;

            if (ShouldPublish(nowUtc, _lastGpsUtc, GpsRateHz))
            {
                var gps = BuildTwinGpsPayload(state, nowUtc);
                await _server.BroadcastAsync(gps);
                _lastGpsUtc = nowUtc;
            }

            if (ShouldPublish(nowUtc, _lastImuUtc, ImuRateHz))
            {
                var imu = BuildTwinImuPayload(state, nowUtc);
                await _server.BroadcastAsync(imu);
                _lastImuUtc = nowUtc;
            }
        }

        /// <summary>
        /// VehicleState.Position (yerel metre) bilgisini referans WGS84 noktasÄ±na gÃ¶re
        /// lat/lon'a Ã§evirir ve Python TwinBus ile uyumlu payload Ã¼retir.
        /// </summary>
        private object BuildTwinGpsPayload(VehicleState state, DateTime nowUtc)
        {
            var (lat, lon) = LocalMetersToLatLon(
                ReferenceLatDeg,
                ReferenceLonDeg,
                state.Position.X,
                state.Position.Y);

            return new
            {
                type = "TwinGps",
                lat,
                lon,
                alt = ReferenceAltM + state.Position.Z,
                fix = GpsFix,
                hdop = GpsHdop,
                t_gps = ToUnixSeconds(nowUtc),
                source = SourceName
            };
        }

        /// <summary>
        /// VehicleState iÃ§indeki orientation ve angular velocity bilgisinden
        /// Python TwinBus ile uyumlu IMU payload'Ä± Ã¼retir.
        /// </summary>
        private object BuildTwinImuPayload(VehicleState state, DateTime nowUtc)
        {
            // VehicleState.AngularVelocity deg/s tutuluyor.
            // Python fuser gz alanÄ±nÄ± rad/s bekliyor.
            var gxRad = DegToRad(state.AngularVelocity.X);
            var gyRad = DegToRad(state.AngularVelocity.Y);
            var gzRad = DegToRad(state.AngularVelocity.Z);

            // Åimdilik lineer ivme Ã¼retmiyoruz; twin iÃ§in temel hedef oryantasyon + gyro.
            // Ä°leride PhysicsIntegrator/Ã¶nceki state farkÄ± ile ivme tahmini eklenebilir.
            return new
            {
                type = "TwinImu",
                ax = 0.0,
                ay = 0.0,
                az = 0.0,
                gx = gxRad,
                gy = gyRad,
                gz = gzRad,
                roll_deg = state.Orientation.RollDeg,
                pitch_deg = state.Orientation.PitchDeg,
                yaw_deg = state.Orientation.YawDeg,
                t_imu = ToUnixSeconds(nowUtc),
                source = SourceName
            };
        }

        /// <summary>
        /// Yerel X/Y metre bilgisini referans enlem/boylam etrafÄ±nda WGS84 dereceye Ã§evirir.
        /// X doÄŸu-batÄ±, Y kuzey-gÃ¼ney kabul edilir.
        /// </summary>
        private static (double lat, double lon) LocalMetersToLatLon(
            double refLatDeg,
            double refLonDeg,
            double xMeters,
            double yMeters)
        {
            var dLat = yMeters / EarthMetersPerDegLat;

            var cosLat = Math.Cos(refLatDeg * Math.PI / 180.0);
            if (Math.Abs(cosLat) < 1e-9)
                cosLat = 1e-9;

            var dLon = xMeters / (EarthMetersPerDegLat * cosLat);

            return (refLatDeg + dLat, refLonDeg + dLon);
        }

        private static bool ShouldPublish(DateTime nowUtc, DateTime lastUtc, double rateHz)
        {
            if (rateHz <= 0)
                return false;

            if (lastUtc == DateTime.MinValue)
                return true;

            var minPeriod = TimeSpan.FromSeconds(1.0 / rateHz);
            return (nowUtc - lastUtc) >= minPeriod;
        }

        private static double ToUnixSeconds(DateTime utc)
            => new DateTimeOffset(utc).ToUnixTimeMilliseconds() / 1000.0;

        private static double DegToRad(double deg)
            => deg * Math.PI / 180.0;
    }
}
