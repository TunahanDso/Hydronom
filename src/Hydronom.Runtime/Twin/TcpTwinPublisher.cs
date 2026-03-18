using System;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Twin
{
    /// <summary>
    /// Runtime içindeki VehicleState bilgisini TwinGps ve TwinImu mesajlarına çevirip
    /// TcpJsonServer üzerinden Python tarafına yayınlayan köprü.
    ///
    /// Tasarım hedefleri:
    /// - Python csharp_sim backend'lerinin beklediği alan adlarıyla uyumlu JSON üretmek
    /// - GPS ve IMU yayın hızlarını ayrı ayrı kontrol edebilmek
    /// - VehicleState üzerinden basit ama tutarlı twin veri üretmek
    /// - Şimdilik dış bağımlılığı minimum tutmak
    ///
    /// Notlar:
    /// - GPS tarafında Python TwinBus küçük harfli alanlar beklediği için burada
    ///   küçük harfli anonymous object üretilir.
    /// - IMU tarafında da benzer şekilde küçük harfli alanlar üretilir.
    /// - Konum, referans enlem/boylam etrafında yerel XY -> lat/lon dönüşümü ile yayınlanır.
    /// - AngularVelocity VehicleState içinde deg/s tutulduğu için TwinImu'da rad/s'e çevrilir.
    /// </summary>
    public sealed class TcpTwinPublisher : ITwinPublisher
    {
        private readonly dynamic _server;

        /// <summary>
        /// Twin GPS için referans enlem [deg]
        /// </summary>
        public double ReferenceLatDeg { get; set; } = 41.0224;

        /// <summary>
        /// Twin GPS için referans boylam [deg]
        /// </summary>
        public double ReferenceLonDeg { get; set; } = 28.8321;

        /// <summary>
        /// Twin GPS için referans irtifa [m]
        /// </summary>
        public double ReferenceAltM { get; set; } = 0.0;

        /// <summary>
        /// GPS yayın frekansı [Hz]
        /// </summary>
        public double GpsRateHz { get; set; } = 5.0;

        /// <summary>
        /// IMU yayın frekansı [Hz]
        /// </summary>
        public double ImuRateHz { get; set; } = 20.0;

        /// <summary>
        /// Twin GPS fix seviyesi
        /// </summary>
        public int GpsFix { get; set; } = 3;

        /// <summary>
        /// Twin GPS hdop değeri
        /// </summary>
        public double GpsHdop { get; set; } = 0.7;

        /// <summary>
        /// Yayın etiketi
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
        /// Mevcut state'ten gerekli twin mesajlarını üretir ve frekans sınırlarına göre yayınlar.
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
        /// VehicleState.Position (yerel metre) bilgisini referans WGS84 noktasına göre
        /// lat/lon'a çevirir ve Python TwinBus ile uyumlu payload üretir.
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
        /// VehicleState içindeki orientation ve angular velocity bilgisinden
        /// Python TwinBus ile uyumlu IMU payload'ı üretir.
        /// </summary>
        private object BuildTwinImuPayload(VehicleState state, DateTime nowUtc)
        {
            // VehicleState.AngularVelocity deg/s tutuluyor.
            // Python fuser gz alanını rad/s bekliyor.
            var gxRad = DegToRad(state.AngularVelocity.X);
            var gyRad = DegToRad(state.AngularVelocity.Y);
            var gzRad = DegToRad(state.AngularVelocity.Z);

            // Şimdilik lineer ivme üretmiyoruz; twin için temel hedef oryantasyon + gyro.
            // İleride PhysicsIntegrator/önceki state farkı ile ivme tahmini eklenebilir.
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
        /// Yerel X/Y metre bilgisini referans enlem/boylam etrafında WGS84 dereceye çevirir.
        /// X doğu-batı, Y kuzey-güney kabul edilir.
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