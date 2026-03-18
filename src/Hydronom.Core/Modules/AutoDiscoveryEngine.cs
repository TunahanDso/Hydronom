// File: Hydronom.Core/Modules/AutoDiscoveryEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;              // Vec3, ThrusterDesc
using ThrusterDesc = Hydronom.Core.Domain.ThrusterDesc;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// PWM-IMU (Accel + Gyro) tabanlı otomatik motor keşfi.
    /// PWM kanalına darbeler verip:
    /// 1. Lineer İvme (Accel) -> İtki Yönü (ForceDir)
    /// 2. Açısal İvme (Gyro)  -> Motor Konumu (Position - Tork analizi ile)
    /// çıkarımlarını yapar. Platform bağımsızdır (Tekne, ROV, vb.).
    /// 
    /// Notlar:
    /// - Z ekseni (heave thruster) desteklenir.
    /// - Hiç motor bulunamazsa simülasyon için profil + rastgele tabanlı
    ///   fallback thruster dizilimi üretebilir.
    /// </summary>
    public class AutoDiscoveryEngine
    {
        /// <summary>
        /// Platform tipi: Yüzey teknesi, denizaltı, ROV vb.
        /// Fallback (sim) diziliminde ve bazı heuristiklerde kullanılır.
        /// </summary>
        public enum VehicleProfile
        {
            Unknown = 0,
            Surface = 1,   // Tekne, USV
            Submarine = 2, // Denizaltı, AUV
            Rov = 3        // 6-DoF ROV
        }

        public record ChannelSample(int Channel, Vec3 Position, Vec3 ForceDirection, double Confidence);

        private readonly List<ChannelSample> _results = new();

        // Sadece Accel yetmez, Tork hesabı için Gyro da lazım
        private readonly List<(Vec3 Accel, Vec3 Gyro)> _imuSamples = new();
        private readonly int _maxSamples;

        // Fallback parametreleri (Fizik hesabı yetersiz kalırsa kullanılır)
        // AssumedMotorCount <= 0 ise motor sayısı profil + rastgele seçilir.
        public int AssumedMotorCount { get; set; } = 0;
        public double AssumedRadius { get; set; } = 0.5;

        /// <summary>
        /// Heave (dikey) thruster'ların merkeze göre yaklaşık yüksekliği.
        /// Submarine/ROV profillerinde kullanılır.
        /// </summary>
        public double AssumedHeaveHeight { get; set; } = 0.3;

        /// <summary>
        /// Keşif algoritmasının çalıştığı platform tipi.
        /// Varsayılan Surface (tekne).
        /// </summary>
        public VehicleProfile Profile { get; set; } = VehicleProfile.Surface;

        public IReadOnlyList<ChannelSample> Results => _results;

        /// <summary>
        /// İstenirse ham IMU örneklerine dışarıdan da bakılabilsin diye expose ettik.
        /// (Örn. debug, grafik çizimi, ileri analiz vs.)
        /// </summary>
        public IReadOnlyList<(Vec3 Accel, Vec3 Gyro)> ImuSamples => _imuSamples;

        public AutoDiscoveryEngine(int maxSamples = 200)
        {
            _maxSamples = maxSamples;
        }

        /// <summary>
        /// Yeni IMU verisi (İvme + Dönüş Hızı) geldiğinde çağrılır.
        /// </summary>
        public void AddImuSample(Vec3 accel, Vec3 gyro)
        {
            if (_imuSamples.Count >= _maxSamples)
                _imuSamples.RemoveAt(0);
            _imuSamples.Add((accel, gyro));
        }

        /// <summary>
        /// PWM taraması sırasında belirli bir kanalın fiziksel etkisini analiz eder.
        /// </summary>
        /// <param name="before">Test öncesi gürültü profili</param>
        /// <param name="after">Test sırasındaki tepki profili</param>
        public void RegisterChannelEffect(
            int channel,
            IEnumerable<(Vec3 Accel, Vec3 Gyro)> before,
            IEnumerable<(Vec3 Accel, Vec3 Gyro)> after)
        {
            if (before is null || after is null) return;

            // ÖNEMLİ: before/after bir stream/iterator olabilir, birden fazla enumerate etmeyelim.
            var beforeArr = before as (Vec3 Accel, Vec3 Gyro)[] ?? before.ToArray();
            var afterArr  = after  as (Vec3 Accel, Vec3 Gyro)[] ?? after.ToArray();

            if (beforeArr.Length == 0 || afterArr.Length == 0)
            {
                Console.WriteLine($"[DISCOVERY] Channel {channel}: empty before/after samples, skipping.");
                return;
            }

            // 1. Ortalama Gürültü ve Sinyal seviyelerini çıkar
            var avgAccelBefore = Average(beforeArr.Select(x => x.Accel));
            var avgAccelAfter  = Average(afterArr.Select(x => x.Accel));

            var avgGyroBefore  = Average(beforeArr.Select(x => x.Gyro));
            var avgGyroAfter   = Average(afterArr.Select(x => x.Gyro));

            // 2. Delta (Net Etki) Hesapla
            var deltaForce  = avgAccelAfter - avgAccelBefore; // F (Kuvvet Vektörü) ~ ivme farkı
            var deltaTorque = avgGyroAfter  - avgGyroBefore;  // Tau (Tork Vektörü) ~ açısal hız farkı

            var magForce  = deltaForce.Length;
            var magTorque = deltaTorque.Length;

            // Yeterli itki yoksa çöp veri üretme
            if (magForce < 1e-3)
            {
                Console.WriteLine($"[DISCOVERY] Channel {channel} -> No significant thrust detected (|Δa|={magForce:F4}).");
                return;
            }

            var forceDir = deltaForce / magForce; // Normalize İtki Yönü

            // 3. Konum Kestirimi (3D fizik + fallback)
            Vec3 estimatedPos;
            if (magTorque > 0.05) // Eşik değer: Yeterli dönüş var mı?
            {
                estimatedPos = CalculatePositionFromPhysics(forceDir, deltaTorque);
            }
            else
            {
                // Dönüş yoksa (Motor tam merkezde veya veri gürültülü) -> Varsayılan dizilim
                estimatedPos = EstimateFallbackPosition(channel, forceDir);
            }

            var confidence = Math.Clamp(magForce, 0.0, 100.0); // güven skorunu normalize tut (opsiyonel)

            var sample = new ChannelSample(
                Channel: channel,
                Position: estimatedPos,
                ForceDirection: forceDir,
                Confidence: confidence
            );

            _results.Add(sample);
            Console.WriteLine($"[DISCOVERY] CH{channel}: Dir={Fmt(forceDir)} Pos={Fmt(estimatedPos)} (Conf={confidence:F2}, |Δτ|={magTorque:F3})");
        }

        /// <summary>
        /// Tork = Konum x Kuvvet (tau = r x F) prensibini tersine işleterek
        /// motorun yaklaşık konumunu çıkarır.
        /// 
        /// Güncelleme:
        /// - Eğer F vektörünün Z bileşeni baskınsa -> heave thruster varsay
        ///   ve Tau.X / Tau.Y kullanarak X/Y konumunu kestirmeye çalış.
        /// - Diğer durumlarda XY düzleminde analiz.
        /// </summary>
        private Vec3 CalculatePositionFromPhysics(Vec3 F, Vec3 Tau)
        {
            const double eps = 1e-6;

            // Heave thruster mı? (Z ekseni baskın)
            if (Math.Abs(F.Z) >= Math.Abs(F.X) &&
                Math.Abs(F.Z) >= Math.Abs(F.Y) &&
                Math.Abs(F.Z) > 1e-3)
            {
                // Heave thruster modeli:
                // F ≈ (0, 0, Fz)
                // Tau = r x F => 
                //   τx ≈  y * Fz
                //   τy ≈ -x * Fz
                //   τz ≈  0
                // buradan:
                //   y ≈ τx / Fz
                //   x ≈ -τy / Fz
                var fz = F.Z;
                if (Math.Abs(fz) < eps)
                    fz = Math.Sign(fz == 0 ? 1.0 : fz) * eps; // Bölme hatası koruması

                double x = -Tau.Y / fz;
                double y =  Tau.X / fz;

                // Heave thruster'ı merkezin biraz üstünde veya altında varsay
                double z = Math.Sign(F.Z == 0 ? 1.0 : F.Z) * AssumedHeaveHeight;

                // Güvenli alan içine sıkıştır
                x = Math.Clamp(x, -AssumedRadius, AssumedRadius);
                y = Math.Clamp(y, -AssumedRadius, AssumedRadius);

                return new Vec3(x, y, z);
            }

            // XY temelli yaklaşım (Surface tekne vb. için)
            double xx = 0, yy = 0;
            double scale = AssumedRadius;

            // A) Eğer motor İLERİ/GERİ (+-X) itiyorsa:
            // Tau.Z = x*Fy - y*Fx  => Fy~0 ise => Tau.Z = -y * Fx → y = -Tau.Z / Fx
            if (Math.Abs(F.X) > Math.Abs(F.Y))
            {
                var fx = F.X;
                if (Math.Abs(fx) < eps)
                    fx = Math.Sign(fx == 0 ? 1.0 : fx) * eps;

                yy = -Tau.Z / fx;
                // X konumu için varsayım: ileri iten genelde arkadadır (göreceli)
                xx = (F.X > 0) ? -scale : scale;
            }
            // B) Eğer motor YANA (+-Y) itiyorsa (omni drive):
            // Tau.Z = x*Fy => x = Tau.Z / Fy
            else
            {
                var fy = F.Y;
                if (Math.Abs(fy) < eps)
                    fy = Math.Sign(fy == 0 ? 1.0 : fy) * eps;

                xx = Tau.Z / fy;
                yy = 0; // Merkezde varsay
            }

            return new Vec3(
                Math.Clamp(xx, -scale, scale),
                Math.Clamp(yy, -scale, scale),
                0
            );
        }

        /// <summary>
        /// Fallback: Fiziksel veri yetersizse varsayımsal konum.
        /// 
        /// - Yüzey teknesi (Surface) için XY düzleminde dairesel dizilim.
        /// - Heave yönü baskınsa Z bileşeni olan basit bir konumlandırma.
        /// </summary>
        private Vec3 EstimateFallbackPosition(int channel, Vec3 forceDir)
        {
            // Heave thruster gibi görünüyorsa (Z çok baskın)
            if (Math.Abs(forceDir.Z) >= Math.Abs(forceDir.X) &&
                Math.Abs(forceDir.Z) >= Math.Abs(forceDir.Y) &&
                Math.Abs(forceDir.Z) > 1e-3)
            {
                var count = Math.Max(1, AssumedMotorCount <= 0 ? 4 : AssumedMotorCount);
                var angle = 2.0 * Math.PI * (channel % count) / count;

                var x = 0.5 * AssumedRadius * Math.Cos(angle);
                var y = 0.5 * AssumedRadius * Math.Sin(angle);
                var z = Math.Sign(forceDir.Z == 0 ? 1.0 : forceDir.Z) * AssumedHeaveHeight;

                return new Vec3(x, y, z);
            }

            // Aksi halde klasik 2D dairesel dizilim
            return EstimateCircularPosition(channel);
        }

        /// <summary>
        /// Fallback: Fiziksel veri yetersizse dairesel varsayım (XY düzleminde).
        /// AssumedMotorCount <= 0 ise 4 alınır (sadece bu lokal hesap için).
        /// </summary>
        private Vec3 EstimateCircularPosition(int channel)
        {
            var count = Math.Max(1, AssumedMotorCount <= 0 ? 4 : AssumedMotorCount);
            var angle = 2.0 * Math.PI * (channel % count) / count;

            return new Vec3(
                AssumedRadius * Math.Cos(angle),
                AssumedRadius * Math.Sin(angle),
                0
            );
        }

        private static Vec3 Average(IEnumerable<Vec3> list)
        {
            var arr = list as IList<Vec3> ?? list.ToList();
            if (arr.Count == 0) return Vec3.Zero;

            double sx = 0, sy = 0, sz = 0;
            foreach (var v in arr) { sx += v.X; sy += v.Y; sz += v.Z; }

            return new Vec3(sx / arr.Count, sy / arr.Count, sz / arr.Count);
        }

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";

        /// <summary>
        /// Elde edilen sonuçları ThrusterDesc listesine dönüştürür.
        /// Normalde gerçek donanım keşfi için kullanılır.
        /// </summary>
        public List<ThrusterDesc> GenerateThrusterMap()
        {
            var bestPerChannel = _results
                .GroupBy(r => r.Channel)
                .Select(g => g.OrderByDescending(x => x.Confidence).First())
                .OrderBy(r => r.Channel);

            var thrusters = bestPerChannel
                .Select(r => new ThrusterDesc(
                    Id: $"CH{r.Channel}",
                    Channel: r.Channel,
                    Position: r.Position,      // Fizik veya varsayımla bulunan konum
                    ForceDir: r.ForceDirection // Ölçülen itki yönü
                ))
                .ToList();

            Console.WriteLine($"[DISCOVERY] Generation complete. {thrusters.Count} thrusters identified.");
            return thrusters;
        }

        /// <summary>
        /// Donanım keşfi başarısız olduğunda (veya hiç denenmediğinde),
        /// simülasyon için profil + rastgele tabanlı sahte thruster dizilimi üretir.
        ///
        /// Bu fonksiyon:
        /// - ActuatorManager tarafında "motor bulunamadı, sim'e geçiyoruz"
        ///   durumunda kullanılmak üzere tasarlanmıştır.
        /// - Motor sayısı:
        ///     * AssumedMotorCount > 0 ise doğrudan ondan alınır,
        ///     * Aksi halde VehicleProfile'a göre rastgele bir aralık seçilir.
        /// </summary>
        public List<ThrusterDesc> GenerateFallbackThrusterMapForSimulation()
        {
            var thrusters = new List<ThrusterDesc>();

            // Eğer zaten gerçek keşif sonuçları varsa, onları kullanmak daha mantıklı.
            if (_results.Count > 0)
            {
                Console.WriteLine("[DISCOVERY] Using real discovery results for simulation map.");
                return GenerateThrusterMap();
            }

            // Rastgele üreteceğimiz motor sayısını belirle
            var rng = new Random();

            int count;
            if (AssumedMotorCount > 0)
            {
                count = AssumedMotorCount;
            }
            else
            {
                // Profil bazlı mantıklı aralıklar
                (int min, int max) range = Profile switch
                {
                    VehicleProfile.Submarine => (4, 8),
                    VehicleProfile.Rov       => (4, 8),
                    VehicleProfile.Surface   => (3, 6),
                    _                        => (3, 8)
                };

                count = rng.Next(range.min, range.max + 1);
            }

            Console.WriteLine($"[DISCOVERY] No real motors detected. Generating RANDOM fallback SIM thrusters (Profile={Profile}, count={count}).");

            switch (Profile)
            {
                case VehicleProfile.Submarine:
                case VehicleProfile.Rov:
                {
                    // Basit model:
                    // - Yatay (surge/sway) thruster'lar: XY düzleminde rastgele dairesel
                    // - Dikey (heave) thruster'lar: +/-Z'de, yine daire üzerinde
                    int horizCount = Math.Max(2, count / 2);
                    int heaveCount = count - horizCount;

                    // Yatay thruster'lar
                    for (int i = 0; i < horizCount; i++)
                    {
                        var pos = RandomOnCircle(rng, AssumedRadius);

                        // Yatay itki: çoğunlukla ileri +X, biraz yanal/z jitter ile
                        var dir = new Vec3(
                            1.0,
                            (rng.NextDouble() - 0.5) * 0.3,
                            (rng.NextDouble() - 0.5) * 0.1
                        ).Normalize();

                        thrusters.Add(new ThrusterDesc(
                            Id: $"SIM_H{i}",
                            Channel: i,
                            Position: pos,
                            ForceDir: dir
                        ));
                    }

                    // Heave thruster'lar
                    for (int j = 0; j < heaveCount; j++)
                    {
                        int idx = horizCount + j;
                        var pos2D = RandomOnCircle(rng, AssumedRadius * 0.5);

                        // Z: yarısı yukarı, yarısı aşağı
                        var z = (j % 2 == 0 ? +AssumedHeaveHeight : -AssumedHeaveHeight);
                        var pos = new Vec3(pos2D.X, pos2D.Y, z);

                        var dir = new Vec3(0, 0, z >= 0 ? 1 : -1); // Yukarı/Aşağı itki

                        thrusters.Add(new ThrusterDesc(
                            Id: $"SIM_V{j}",
                            Channel: idx,
                            Position: pos,
                            ForceDir: dir
                        ));
                    }

                    break;
                }

                case VehicleProfile.Surface:
                default:
                {
                    // Surface için: tüm thruster'lar XY düzleminde rastgele dairesel,
                    // itki yönü çoğunlukla ileri (+X) ama biraz jitter ile.
                    for (int i = 0; i < count; i++)
                    {
                        var pos = RandomOnCircle(rng, AssumedRadius);

                        var dir = new Vec3(
                            1.0,
                            (rng.NextDouble() - 0.5) * 0.3,
                            (rng.NextDouble() - 0.5) * 0.1
                        ).Normalize();

                        thrusters.Add(new ThrusterDesc(
                            Id: $"SIM_CH{i}",
                            Channel: i,
                            Position: pos,
                            ForceDir: dir
                        ));
                    }

                    break;
                }
            }

            Console.WriteLine($"[DISCOVERY] RANDOM fallback SIM thrusters generated: {thrusters.Count}.");
            return thrusters;
        }

        /// <summary>
        /// XY düzleminde, rastgele açılı dairesel bir konum üretir.
        /// </summary>
        private static Vec3 RandomOnCircle(Random rng, double radius)
        {
            var angle = rng.NextDouble() * 2.0 * Math.PI;
            return new Vec3(
                radius * Math.Cos(angle),
                radius * Math.Sin(angle),
                0
            );
        }

        public void Reset()
        {
            _results.Clear();
            _imuSamples.Clear();
        }
    }
}
