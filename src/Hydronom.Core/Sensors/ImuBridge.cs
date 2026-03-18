// File: Hydronom.Core/Sensors/ImuBridge.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Sensors
{
    /// <summary>
    /// IMU Veri Köprüsü.
    /// MİMARİ NOTU: C# tarafı sensörlere doğrudan erişmez.
    /// Bu sınıf, Python katmanından TCP ile gelen IMU verilerinin (Push) toplandığı
    /// ve AutoDiscoveryEngine gibi modüllere dağıtıldığı merkezdir.
    /// Simülasyon modunda ise kendi sanal verisini üretir.
    /// </summary>
    public class ImuBridge : IDisposable
    {
        private readonly bool _simulationMode;
        private readonly Random _rand = new();
        private bool _disposed;

        // Son okunan değerler (Thread-safe)
        private Vec3 _accel = Vec3.Zero;
        private Vec3 _gyro = Vec3.Zero;
        private readonly object _lock = new();

        // Simülasyon gürültü parametreleri
        private double _noiseLevelAccel = 0.015; // m/s²
        private double _noiseLevelGyro = 0.05;   // rad/s

        /// <summary>
        /// Yeni IMU verisi geldiğinde tetiklenir.
        /// AutoDiscoveryEngine ve diğer modüller buraya abone olur.
        /// </summary>
        public event Action<Vec3, Vec3>? OnSample;

        /// <summary>
        /// Simülasyon modu örnekleme hızı.
        /// </summary>
        public double SimSampleRateHz { get; init; } = 100.0;

        public ImuBridge(bool simulationMode = false)
        {
            _simulationMode = simulationMode;
        }

        public async Task StartAsync(CancellationToken token)
        {
            Console.WriteLine($"[IMU] Bridge started (Mode: {(_simulationMode ? "Simulation" : "Passive/External")})");

            if (!_simulationMode)
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (TaskCanceledException) { }
                return;
            }

            // Sim mode veri üretim döngüsü
            var dt = 1.0 / SimSampleRateHz;

            while (!token.IsCancellationRequested && !_disposed)
            {
                var accel = SimulateAccel();
                var gyro  = SimulateGyro();

                UpdateInternal(accel, gyro);

                await Task.Delay(TimeSpan.FromSeconds(dt), token);
            }
        }

        /// <summary>
        /// Python veri sisteminden gelen IMU verisini enjekte eder.
        /// </summary>
        public void PushExternalData(Vec3 accel, Vec3 gyro)
        {
            if (_simulationMode)
                return; // sim modunda dış veri yok sayılır

            UpdateInternal(accel, gyro);
        }

        private void UpdateInternal(Vec3 a, Vec3 g)
        {
            lock (_lock)
            {
                _accel = a;
                _gyro  = g;
            }

            OnSample?.Invoke(a, g);
        }

        public (Vec3 accel, Vec3 gyro) GetLatest()
        {
            lock (_lock)
                return (_accel, _gyro);
        }

        // -------------------------
        //     SİMÜLASYON MODE
        // -------------------------

        private Vec3 SimulateAccel()
        {
            var t = DateTime.UtcNow.Millisecond / 1000.0;

            var baseAcc = new Vec3(
                0.02 * Math.Sin(t * 2.0),
                0.02 * Math.Cos(t * 1.5),
                -9.81
            );

            return baseAcc + NoiseVec(_noiseLevelAccel);
        }

        private Vec3 SimulateGyro()
        {
            var t = DateTime.UtcNow.Millisecond / 1000.0;

            var baseGyro = new Vec3(
                0.1 * Math.Sin(t),
                0.1 * Math.Cos(t),
                0.0
            );

            return baseGyro + NoiseVec(_noiseLevelGyro);
        }

        private Vec3 NoiseVec(double level)
        {
            double n() => (_rand.NextDouble() * 2.0 - 1.0) * level;
            return new Vec3(n(), n(), n());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Console.WriteLine("[IMU] Bridge disposed");
        }
    }
}
