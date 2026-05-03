// File: Hydronom.Core/Modules/AutoDiscoveryEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;
using ThrusterDesc = Hydronom.Core.Domain.ThrusterDesc;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Python sensÃ¶r pipeline'Ä±ndan gelen IMU Ã¶rnekleriyle actuator/thruster geometri keÅŸfi yapar.
    ///
    /// Ã–nemli mimari not:
    /// - Bu sÄ±nÄ±f sensÃ¶rÃ¼ doÄŸrudan okumaz.
    /// - IMU, GPS, LiDAR vb. veriler Python tarafÄ±nda toplanÄ±r.
    /// - Runtime/Core tarafÄ± bu sÄ±nÄ±fa Python'dan gelen before/after IMU pencerelerini verir.
    ///
    /// KullanÄ±m modeli:
    /// 1. Python tarafÄ± motor testi Ã¶ncesi kÄ±sa bir IMU penceresi Ã¼retir.
    /// 2. Belirli PWM/kanal kÄ±sa sÃ¼re Ã§alÄ±ÅŸtÄ±rÄ±lÄ±r.
    /// 3. Python tarafÄ± motor testi sÄ±rasÄ±ndaki IMU penceresini Ã¼retir.
    /// 4. RegisterChannelEffect(channel, before, after) Ã§aÄŸrÄ±lÄ±r.
    /// 5. GenerateThrusterMap() ile ThrusterDesc listesi alÄ±nÄ±r.
    ///
    /// Ã‡Ä±karÄ±mlar:
    /// - Accel delta      -> ForceDir tahmini
    /// - Gyro delta       -> torque proxy / moment etkisi
    /// - r x F iliÅŸkisi   -> yaklaÅŸÄ±k Position tahmini
    ///
    /// SÄ±nÄ±rlamalar:
    /// - Bu bir kesin kalibrasyon sistemi deÄŸil, otomatik ilk tahmin motorudur.
    /// - GerÃ§ek yÃ¶n/reverse ayarÄ± yine dÃ¼ÅŸÃ¼k gÃ¼Ã§ fiziksel testle doÄŸrulanmalÄ±dÄ±r.
    /// </summary>
    public class AutoDiscoveryEngine
    {
        public enum VehicleProfile
        {
            Unknown = 0,
            Surface = 1,
            Submarine = 2,
            Rov = 3,
            Ground = 4,
            Aerial = 5
        }

        /// <summary>
        /// Python/runtime tarafÄ±ndan verilebilen ham IMU Ã¶rneÄŸi.
        /// Accel: m/s^2 veya normalize edilmiÅŸ ivme olabilir.
        /// Gyro : rad/s Ã¶nerilir, fakat sadece delta/proxy olarak kullanÄ±lÄ±r.
        /// TimestampUtc: opsiyonel zaman damgasÄ±.
        /// </summary>
        public readonly record struct ImuFrame(
            Vec3 Accel,
            Vec3 Gyro,
            DateTime TimestampUtc
        );

        /// <summary>
        /// Kanal keÅŸif sonucu.
        /// </summary>
        public readonly record struct ChannelSample(
            int Channel,
            Vec3 Position,
            Vec3 ForceDirection,
            double Confidence,
            bool Reversed,
            bool CanReverse,
            string Source,
            double ForceMagnitude,
            double TorqueMagnitude
        );

        private readonly List<ChannelSample> _results = new();
        private readonly List<ImuFrame> _imuSamples = new();

        private readonly int _maxSamples;

        /// <summary>
        /// AssumedMotorCount <= 0 ise fallback profil bazlÄ± karar verir.
        /// </summary>
        public int AssumedMotorCount { get; set; } = 0;

        public double AssumedRadius { get; set; } = 0.5;

        /// <summary>
        /// Heave thruster'larÄ±n merkeze gÃ¶re yaklaÅŸÄ±k yÃ¼ksekliÄŸi.
        /// </summary>
        public double AssumedHeaveHeight { get; set; } = 0.3;

        public VehicleProfile Profile { get; set; } = VehicleProfile.Surface;

        /// <summary>
        /// GerÃ§ek keÅŸiften gelen motorlar iÃ§in varsayÄ±lan CanReverse.
        /// GÃ¼venlik iÃ§in false.
        /// Ã‡ift yÃ¶nlÃ¼ ESC kullanan sistemlerde dÄ±ÅŸarÄ±dan true yapÄ±labilir.
        /// </summary>
        public bool DefaultCanReverse { get; set; } = false;

        /// <summary>
        /// SimÃ¼lasyon fallback thruster'larÄ± iÃ§in CanReverse.
        /// Simde negatif/pozitif Ã§Ã¶zÃ¼m test edilebilsin diye varsayÄ±lan true.
        /// </summary>
        public bool SimulationCanReverse { get; set; } = true;

        /// <summary>
        /// KeÅŸif sÄ±rasÄ±nda Reversed otomatik kilitlenmez.
        /// Ã‡Ã¼nkÃ¼ Reversed motor yÃ¶n kalibrasyonudur ve fiziksel doÄŸrulama ister.
        /// </summary>
        public bool DefaultReversed { get; set; } = false;

        /// <summary>
        /// Minimum anlamlÄ± ivme delta eÅŸiÄŸi.
        /// Python tarafÄ±nda filtrelenmiÅŸ veri geliyorsa bu deÄŸer dÃ¼ÅŸÃ¼k kalabilir.
        /// </summary>
        public double MinForceDelta { get; set; } = 1e-3;

        /// <summary>
        /// Minimum anlamlÄ± gyro/tork proxy delta eÅŸiÄŸi.
        /// </summary>
        public double MinTorqueDelta { get; set; } = 0.05;

        /// <summary>
        /// Confidence hesabÄ±nda kullanÄ±lacak normalize Ã¶lÃ§ek.
        /// </summary>
        public double ConfidenceScale { get; set; } = 1.0;

        public IReadOnlyList<ChannelSample> Results => _results;

        public IReadOnlyList<ImuFrame> ImuSamples => _imuSamples;

        public AutoDiscoveryEngine(int maxSamples = 200)
        {
            _maxSamples = Math.Max(10, maxSamples);
        }

        /// <summary>
        /// Eski API uyumu: accel + gyro Ã¶rneÄŸi ekler.
        /// </summary>
        public void AddImuSample(Vec3 accel, Vec3 gyro)
        {
            AddImuSample(new ImuFrame(accel, gyro, DateTime.UtcNow));
        }

        /// <summary>
        /// Python sensÃ¶r pipeline'Ä±ndan gelen tek IMU frame'i ekler.
        /// </summary>
        public void AddImuSample(ImuFrame frame)
        {
            if (!IsFinite(frame.Accel) || !IsFinite(frame.Gyro))
                return;

            if (_imuSamples.Count >= _maxSamples)
                _imuSamples.RemoveAt(0);

            var timestamp = frame.TimestampUtc == default
                ? DateTime.UtcNow
                : frame.TimestampUtc;

            _imuSamples.Add(frame with { TimestampUtc = timestamp });
        }

        /// <summary>
        /// Python'dan gelen son N Ã¶rneÄŸi before/after olarak bÃ¶lerek kanal etkisi kaydeder.
        /// Bu yÃ¶ntem, runtime tarafÄ± ayrÄ± pencereleri Ã¼retmiyorsa pratik fallback olarak kullanÄ±labilir.
        /// </summary>
        public void RegisterChannelEffectFromBufferedSamples(
            int channel,
            int beforeCount,
            int afterCount)
        {
            if (beforeCount <= 0 || afterCount <= 0)
                return;

            int required = beforeCount + afterCount;
            if (_imuSamples.Count < required)
            {
                Console.WriteLine($"[DISCOVERY] Channel {channel}: not enough buffered Python IMU samples ({_imuSamples.Count}/{required}).");
                return;
            }

            var window = _imuSamples
                .Skip(_imuSamples.Count - required)
                .Take(required)
                .ToArray();

            var before = window
                .Take(beforeCount)
                .Select(x => (x.Accel, x.Gyro));

            var after = window
                .Skip(beforeCount)
                .Take(afterCount)
                .Select(x => (x.Accel, x.Gyro));

            RegisterChannelEffect(channel, before, after);
        }

        /// <summary>
        /// PWM taramasÄ± sÄ±rasÄ±nda belirli bir kanalÄ±n fiziksel etkisini analiz eder.
        /// before ve after pencereleri normalde Python sensÃ¶r katmanÄ±ndan gelir.
        /// </summary>
        public void RegisterChannelEffect(
            int channel,
            IEnumerable<(Vec3 Accel, Vec3 Gyro)> before,
            IEnumerable<(Vec3 Accel, Vec3 Gyro)> after)
        {
            if (channel < 0)
            {
                Console.WriteLine($"[DISCOVERY] Invalid channel {channel}, skipping.");
                return;
            }

            if (before is null || after is null)
                return;

            var beforeArr = before
                .Where(x => IsFinite(x.Accel) && IsFinite(x.Gyro))
                .ToArray();

            var afterArr = after
                .Where(x => IsFinite(x.Accel) && IsFinite(x.Gyro))
                .ToArray();

            if (beforeArr.Length == 0 || afterArr.Length == 0)
            {
                Console.WriteLine($"[DISCOVERY] Channel {channel}: empty/invalid before-after samples, skipping.");
                return;
            }

            var avgAccelBefore = Average(beforeArr.Select(x => x.Accel));
            var avgAccelAfter = Average(afterArr.Select(x => x.Accel));

            var avgGyroBefore = Average(beforeArr.Select(x => x.Gyro));
            var avgGyroAfter = Average(afterArr.Select(x => x.Gyro));

            var deltaForce = avgAccelAfter - avgAccelBefore;
            var deltaTorque = avgGyroAfter - avgGyroBefore;

            var magForce = deltaForce.Length;
            var magTorque = deltaTorque.Length;

            if (!double.IsFinite(magForce) || magForce < MinForceDelta)
            {
                Console.WriteLine($"[DISCOVERY] Channel {channel} -> No significant thrust detected (|Î”a|={magForce:F4}).");
                return;
            }

            var forceDir = SafeNormalize(deltaForce, new Vec3(1, 0, 0));

            Vec3 estimatedPos;
            string source;

            if (double.IsFinite(magTorque) && magTorque >= MinTorqueDelta)
            {
                estimatedPos = CalculatePositionFromPhysics(forceDir, deltaTorque);
                source = "python_imu_physics";
            }
            else
            {
                estimatedPos = EstimateFallbackPosition(channel, forceDir);
                source = "python_imu_force_fallback";
            }

            var confidence = ComputeConfidence(magForce, magTorque, beforeArr.Length, afterArr.Length);

            var sample = new ChannelSample(
                Channel: channel,
                Position: estimatedPos,
                ForceDirection: forceDir,
                Confidence: confidence,
                Reversed: DefaultReversed,
                CanReverse: DefaultCanReverse,
                Source: source,
                ForceMagnitude: magForce,
                TorqueMagnitude: magTorque
            );

            ReplaceOrAddBestSample(sample);

            Console.WriteLine(
                $"[DISCOVERY] CH{channel}: " +
                $"Dir={Fmt(forceDir)} Pos={Fmt(estimatedPos)} " +
                $"Conf={confidence:F2} |Î”a|={magForce:F3} |Î”gyro|={magTorque:F3} " +
                $"CanReverse={sample.CanReverse} Source={source}");
        }

        private void ReplaceOrAddBestSample(ChannelSample sample)
        {
            int idx = _results.FindIndex(x => x.Channel == sample.Channel);

            if (idx < 0)
            {
                _results.Add(sample);
                return;
            }

            if (sample.Confidence >= _results[idx].Confidence)
                _results[idx] = sample;
        }

        private double ComputeConfidence(double magForce, double magTorque, int beforeCount, int afterCount)
        {
            double scale = Math.Max(1e-6, ConfidenceScale);

            double forceScore = Math.Clamp(magForce / scale, 0.0, 1.0);
            double torqueScore = Math.Clamp(magTorque / Math.Max(MinTorqueDelta, 1e-6), 0.0, 1.0);
            double sampleScore = Math.Clamp(Math.Min(beforeCount, afterCount) / 20.0, 0.0, 1.0);

            double confidence = 0.65 * forceScore + 0.20 * torqueScore + 0.15 * sampleScore;

            return Math.Clamp(confidence, 0.0, 1.0);
        }

        /// <summary>
        /// YaklaÅŸÄ±k ters r x F Ã§Ã¶zÃ¼mÃ¼.
        /// Burada Tau gerÃ§ek tork deÄŸil, gyro delta proxy olarak kullanÄ±lÄ±r.
        /// </summary>
        private Vec3 CalculatePositionFromPhysics(Vec3 F, Vec3 Tau)
        {
            const double eps = 1e-6;

            if (!IsFinite(F) || F.Length < eps)
                return EstimateCircularPosition(0);

            if (!IsFinite(Tau))
                Tau = Vec3.Zero;

            if (Math.Abs(F.Z) >= Math.Abs(F.X) &&
                Math.Abs(F.Z) >= Math.Abs(F.Y) &&
                Math.Abs(F.Z) > 1e-3)
            {
                var fz = F.Z;
                if (Math.Abs(fz) < eps)
                    fz = Math.Sign(fz == 0 ? 1.0 : fz) * eps;

                double x = -Tau.Y / fz;
                double y = Tau.X / fz;
                double z = Math.Sign(F.Z == 0 ? 1.0 : F.Z) * AssumedHeaveHeight;

                x = Math.Clamp(x, -AssumedRadius, AssumedRadius);
                y = Math.Clamp(y, -AssumedRadius, AssumedRadius);

                return new Vec3(x, y, z);
            }

            double xx;
            double yy;
            double scale = Math.Max(0.05, AssumedRadius);

            if (Math.Abs(F.X) > Math.Abs(F.Y))
            {
                var fx = F.X;
                if (Math.Abs(fx) < eps)
                    fx = Math.Sign(fx == 0 ? 1.0 : fx) * eps;

                yy = -Tau.Z / fx;
                xx = F.X > 0 ? -scale : scale;
            }
            else
            {
                var fy = F.Y;
                if (Math.Abs(fy) < eps)
                    fy = Math.Sign(fy == 0 ? 1.0 : fy) * eps;

                xx = Tau.Z / fy;
                yy = 0.0;
            }

            return new Vec3(
                Math.Clamp(xx, -scale, scale),
                Math.Clamp(yy, -scale, scale),
                0.0
            );
        }

        private Vec3 EstimateFallbackPosition(int channel, Vec3 forceDir)
        {
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

            return EstimateCircularPosition(channel);
        }

        private Vec3 EstimateCircularPosition(int channel)
        {
            var count = Math.Max(1, AssumedMotorCount <= 0 ? 4 : AssumedMotorCount);
            var angle = 2.0 * Math.PI * (channel % count) / count;

            return new Vec3(
                AssumedRadius * Math.Cos(angle),
                AssumedRadius * Math.Sin(angle),
                0.0
            );
        }

        private static Vec3 Average(IEnumerable<Vec3> list)
        {
            var arr = list as IList<Vec3> ?? list.ToList();

            if (arr.Count == 0)
                return Vec3.Zero;

            double sx = 0;
            double sy = 0;
            double sz = 0;

            foreach (var v in arr)
            {
                sx += v.X;
                sy += v.Y;
                sz += v.Z;
            }

            return new Vec3(sx / arr.Count, sy / arr.Count, sz / arr.Count);
        }

        private static Vec3 SafeNormalize(Vec3 value, Vec3 fallback)
        {
            if (!IsFinite(value) || value.Length < 1e-9)
                return fallback.Normalize();

            return value.Normalize();
        }

        private static bool IsFinite(Vec3 v)
        {
            return
                double.IsFinite(v.X) &&
                double.IsFinite(v.Y) &&
                double.IsFinite(v.Z);
        }

        private static string Fmt(Vec3 v)
            => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";

        /// <summary>
        /// Elde edilen gerÃ§ek/Python tabanlÄ± sonuÃ§larÄ± ThrusterDesc listesine dÃ¶nÃ¼ÅŸtÃ¼rÃ¼r.
        /// </summary>
        public List<ThrusterDesc> GenerateThrusterMap()
        {
            var bestPerChannel = _results
                .GroupBy(r => r.Channel)
                .Select(g => g.OrderByDescending(x => x.Confidence).First())
                .OrderBy(r => r.Channel)
                .ToList();

            var thrusters = bestPerChannel
                .Select(r => new ThrusterDesc(
                    Id: $"CH{r.Channel}",
                    Channel: r.Channel,
                    Position: r.Position,
                    ForceDir: r.ForceDirection,
                    Reversed: r.Reversed,
                    CanReverse: r.CanReverse
                ).Normalize())
                .ToList();

            Console.WriteLine($"[DISCOVERY] Generation complete. {thrusters.Count} thrusters identified from Python-fed IMU windows.");
            return thrusters;
        }

        /// <summary>
        /// DonanÄ±m keÅŸfi baÅŸarÄ±sÄ±z olduÄŸunda veya hiÃ§ denenmediÄŸinde deterministic fallback Ã¼retir.
        ///
        /// Eski sÃ¼rÃ¼m rastgele fallback Ã¼retiyordu. Bu, test tekrar edilebilirliÄŸini bozuyordu.
        /// Bu sÃ¼rÃ¼m profil bazlÄ± deterministik layout Ã¼retir.
        /// </summary>
        public List<ThrusterDesc> GenerateFallbackThrusterMapForSimulation()
        {
            if (_results.Count > 0)
            {
                Console.WriteLine("[DISCOVERY] Using real Python-fed discovery results for simulation map.");
                return GenerateThrusterMap();
            }

            int count = ResolveFallbackMotorCount();

            Console.WriteLine($"[DISCOVERY] No real motors detected. Generating deterministic fallback SIM thrusters (Profile={Profile}, count={count}).");

            var thrusters = Profile switch
            {
                VehicleProfile.Submarine => GenerateSubmarineOrRovFallback(count),
                VehicleProfile.Rov => GenerateSubmarineOrRovFallback(count),
                VehicleProfile.Ground => GenerateGroundFallback(count),
                VehicleProfile.Aerial => GenerateAerialFallback(count),
                VehicleProfile.Surface => GenerateSurfaceFallback(count),
                _ => GenerateSurfaceFallback(count)
            };

            Console.WriteLine($"[DISCOVERY] Deterministic fallback SIM thrusters generated: {thrusters.Count}.");
            return thrusters;
        }

        private int ResolveFallbackMotorCount()
        {
            if (AssumedMotorCount > 0)
                return AssumedMotorCount;

            return Profile switch
            {
                VehicleProfile.Submarine => 6,
                VehicleProfile.Rov => 6,
                VehicleProfile.Ground => 2,
                VehicleProfile.Aerial => 4,
                VehicleProfile.Surface => 4,
                _ => 4
            };
        }

        private List<ThrusterDesc> GenerateSurfaceFallback(int count)
        {
            var thrusters = new List<ThrusterDesc>();

            if (count <= 2)
            {
                thrusters.Add(MakeThruster("SIM_L", 0, new Vec3(-AssumedRadius, +AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
                thrusters.Add(MakeThruster("SIM_R", 1, new Vec3(-AssumedRadius, -AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
                return thrusters;
            }

            if (count == 3)
            {
                thrusters.Add(MakeThruster("SIM_RL", 0, new Vec3(-AssumedRadius, +AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
                thrusters.Add(MakeThruster("SIM_RR", 1, new Vec3(-AssumedRadius, -AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
                thrusters.Add(MakeThruster("SIM_BOW", 2, new Vec3(+AssumedRadius, 0, 0), new Vec3(0, +1, 0)));
                return thrusters;
            }

            thrusters.Add(MakeThruster("SIM_FL", 0, new Vec3(+AssumedRadius, +AssumedRadius * 0.5, 0), new Vec3(0, +1, 0)));
            thrusters.Add(MakeThruster("SIM_FR", 1, new Vec3(+AssumedRadius, -AssumedRadius * 0.5, 0), new Vec3(0, -1, 0)));
            thrusters.Add(MakeThruster("SIM_RL", 2, new Vec3(-AssumedRadius, +AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
            thrusters.Add(MakeThruster("SIM_RR", 3, new Vec3(-AssumedRadius, -AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));

            for (int i = 4; i < count; i++)
            {
                double side = i % 2 == 0 ? +1.0 : -1.0;
                thrusters.Add(MakeThruster(
                    $"SIM_AUX{i}",
                    i,
                    new Vec3(0, side * AssumedRadius * 0.5, 0),
                    new Vec3(+1, 0, 0)));
            }

            return thrusters;
        }

        private List<ThrusterDesc> GenerateSubmarineOrRovFallback(int count)
        {
            var thrusters = new List<ThrusterDesc>();

            int horizontal = Math.Max(2, Math.Min(4, count / 2 + count % 2));
            int vertical = Math.Max(0, count - horizontal);

            for (int i = 0; i < horizontal; i++)
            {
                double angle = 2.0 * Math.PI * i / horizontal;
                var pos = new Vec3(
                    AssumedRadius * Math.Cos(angle),
                    AssumedRadius * Math.Sin(angle),
                    0.0);

                Vec3 dir = i % 2 == 0
                    ? new Vec3(+1, 0, 0)
                    : new Vec3(0, +1, 0);

                thrusters.Add(MakeThruster($"SIM_H{i}", i, pos, dir));
            }

            for (int j = 0; j < vertical; j++)
            {
                int channel = horizontal + j;
                double side = j % 2 == 0 ? +1.0 : -1.0;
                double x = j < 2 ? +AssumedRadius * 0.5 : -AssumedRadius * 0.5;

                var pos = new Vec3(x, side * AssumedRadius * 0.5, AssumedHeaveHeight);
                var dir = new Vec3(0, 0, +1);

                thrusters.Add(MakeThruster($"SIM_V{j}", channel, pos, dir));
            }

            return thrusters;
        }

        private List<ThrusterDesc> GenerateGroundFallback(int count)
        {
            var thrusters = new List<ThrusterDesc>();

            int actual = Math.Max(2, count);

            thrusters.Add(MakeThruster("SIM_LEFT", 0, new Vec3(0, +AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));
            thrusters.Add(MakeThruster("SIM_RIGHT", 1, new Vec3(0, -AssumedRadius * 0.5, 0), new Vec3(+1, 0, 0)));

            for (int i = 2; i < actual; i++)
            {
                double side = i % 2 == 0 ? +1.0 : -1.0;
                thrusters.Add(MakeThruster(
                    $"SIM_G{i}",
                    i,
                    new Vec3(-AssumedRadius * 0.5, side * AssumedRadius * 0.5, 0),
                    new Vec3(+1, 0, 0)));
            }

            return thrusters;
        }

        private List<ThrusterDesc> GenerateAerialFallback(int count)
        {
            var thrusters = new List<ThrusterDesc>();
            int actual = Math.Max(4, count);

            for (int i = 0; i < actual; i++)
            {
                double angle = 2.0 * Math.PI * i / actual;
                var pos = new Vec3(
                    AssumedRadius * Math.Cos(angle),
                    AssumedRadius * Math.Sin(angle),
                    0.0);

                thrusters.Add(MakeThruster($"SIM_PROP{i}", i, pos, new Vec3(0, 0, +1)));
            }

            return thrusters;
        }

        private ThrusterDesc MakeThruster(string id, int channel, Vec3 position, Vec3 forceDir)
        {
            return new ThrusterDesc(
                Id: id,
                Channel: channel,
                Position: position,
                ForceDir: SafeNormalize(forceDir, new Vec3(1, 0, 0)),
                Reversed: false,
                CanReverse: SimulationCanReverse
            ).Normalize();
        }

        public void Reset()
        {
            _results.Clear();
            _imuSamples.Clear();
        }
    }
}
