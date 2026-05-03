// Hydronom.Runtime\Actuators\ActuatorManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// 6-DoF geometry tabanlı actuator yöneticisi.
    ///
    /// Bu ana partial dosya yalnızca ana orkestrasyon akışını taşır.
    ///
    /// Parçalanmış sorumluluklar:
    /// - ActuatorManager.Allocation.cs  : B matrisi, ridge LS solver, authority, allocation raporu
    /// - ActuatorManager.Protocol.cs    : Serial, COBS, CRC, TX/RX, telemetry parse
    /// - ActuatorManager.Diagnostics.cs : Watchdog, failsafe, health, logging, summary, dispose
    /// - ActuatorModels.cs              : Thruster, authority, health, allocation report modelleri
    ///
    /// Not:
    /// - Burada hesaplanan toplam kuvvet ve moment body-frame’dedir.
    /// - Dünya frame dönüşümü fizik entegratörü / üst katman tarafından yapılmalıdır.
    /// </summary>
    public sealed partial class ActuatorManager : IActuator, IDisposable
    {
        private readonly List<Thruster> _thrusters = new();
        private readonly IMotorController? _motorController;

        private readonly object _stateLock = new();
        private readonly object _serialLock = new();

        private string? _serialPortName;
        private int _serialBaud;
        private SerialPort? _serial;
        private bool _disposed;

        private ControlAuthorityProfile _authorityProfile = ControlAuthorityProfile.Empty;

        private double[,] _baseB = new double[0, 0];
        private SolverCache _solverCache = SolverCache.Empty;

        private readonly ConcurrentQueue<byte[]> _txQueue = new();
        private readonly AutoResetEvent _txSignal = new(false);
        private readonly Thread _txThread;

        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly AutoResetEvent _logSignal = new(false);
        private readonly Thread _logThread;

        private readonly CancellationTokenSource _cts = new();

        private readonly Timer _watchdogTimer;
        private long _lastApplyTicks;
        private int _watchdogNeutralized;

        private readonly Thread? _rxThread;
        private ushort _commandSequence;
        private ushort _telemetrySequence;

        private volatile bool _serialFaultLatched;

        // Seri TX debug log spam'ini azaltmak için zaman damgası.
        private long _lastCommandLogTicks;

        private const double SolverLambda = 0.02;
        private const int WatchdogTimeoutMs = 250;
        private const int WatchdogTickMs = 10;
        private const int MaxProtocolThrusters = 16;

        private const byte ProtocolVersion = 1;
        private const byte MsgTypeCommand = 0x10;
        private const byte MsgTypeTelemetry = 0x20;

        /// <summary>
        /// Her actuator/thruster için maksimum itki.
        /// Birim: N
        /// </summary>
        public double MaxThrustN { get; set; } = 22.0;

        /// <summary>
        /// Solver hedefinde moment eksenlerini ölçeklemek için kullanılır.
        /// Rapor tarafında gerçek istenen tork korunur.
        /// </summary>
        public double TorqueWeight { get; set; } = 1.0;

        /// <summary>
        /// Thruster health değerlendirmesinde anlamlı komut eşiği.
        /// </summary>
        public double CommandActivityThreshold { get; set; } = 0.15;

        /// <summary>
        /// Jam şüphesi için akım eşiği.
        /// Birim: mA
        /// </summary>
        public int JamCurrentThresholdMilliAmp { get; set; } = 1200;

        /// <summary>
        /// Jam şüphesi için düşük RPM eşiği.
        /// </summary>
        public int JamRpmThreshold { get; set; } = 80;

        /// <summary>
        /// Telemetry stale timeout.
        /// Birim: ms
        /// </summary>
        public int FeedbackStaleTimeoutMs { get; set; } = 250;

        /// <summary>
        /// Thruster çıkış rampası.
        /// Normalize çıkış için birim/s.
        /// 0 veya negatif verilirse kapalı kabul edilir.
        /// </summary>
        public double OutputSlewRatePerSec { get; set; } = 25.0;

        /// <summary>
        /// Son hesaplanan body-frame toplam wrench.
        ///
        /// Not:
        /// VehicleState burada fiziksel state değil, yalnızca son actuator wrench taşıyıcısı olarak kullanılır.
        /// </summary>
        public VehicleState VehicleState { get; private set; } = VehicleState.Zero;

        public Vec3 LastForceBody => VehicleState.LinearForce;
        public Vec3 LastTorqueBody => VehicleState.AngularTorque;

        /// <summary>
        /// Son allocation işleminin açıklanabilir raporu.
        /// Analysis, Safety, Diagnostics ve Ops katmanları bunu okuyabilir.
        /// </summary>
        public ActuatorAllocationReport LastAllocationReport { get; private set; } =
            ActuatorAllocationReport.Empty;

        public event Action<Vec3, Vec3>? WrenchComputed;

        public IReadOnlyList<Thruster> Thrusters => _thrusters;
        public ControlAuthorityProfile AuthorityProfile => _authorityProfile;

        public string? LastSerialError { get; private set; }

        public string? SerialPortName => _serialPortName;
        public int SerialBaud => _serialBaud;

        public bool IsSerialOpen
        {
            get
            {
                lock (_serialLock)
                    return _serial?.IsOpen == true;
            }
        }

        public ActuatorManager(
            IEnumerable<ThrusterDesc>? thrusterDescs = null,
            IMotorController? motorController = null,
            string? serialPortName = null,
            int serialBaud = 115200)
        {
            _motorController = motorController;

            if (thrusterDescs != null)
            {
                foreach (var t in thrusterDescs)
                    _thrusters.Add(new Thruster(t));
            }

            if (_thrusters.Count == 0)
            {
                EnqueueLog("[ActuatorManager] Thruster config/discovery bulunamadı. Deterministik SIM thruster seti üretiliyor...");
                GenerateDeterministicSimThrusters();
            }

            if (_thrusters.Count > MaxProtocolThrusters)
                throw new InvalidOperationException($"Protocol en fazla {MaxProtocolThrusters} thruster destekliyor. Mevcut: {_thrusters.Count}");

            _baseB = BuildThrusterMatrixFromGeometry();
            RebuildSolverCache_NoLockRequired();
            RecomputeAuthorityProfile_NoLockRequired();

            _serialPortName = string.IsNullOrWhiteSpace(serialPortName) ? null : serialPortName;
            _serialBaud = serialBaud;

            _txThread = new Thread(TxWorkerLoop)
            {
                IsBackground = true,
                Name = "ActuatorManager-TX"
            };
            _txThread.Start();

            _logThread = new Thread(LogWorkerLoop)
            {
                IsBackground = true,
                Name = "ActuatorManager-LOG"
            };
            _logThread.Start();

            _lastApplyTicks = Stopwatch.GetTimestamp();
            _watchdogTimer = new Timer(WatchdogTick, null, WatchdogTickMs, WatchdogTickMs);

            if (_serialPortName is not null)
            {
                TryOpenSerial();

                _rxThread = new Thread(RxWorkerLoop)
                {
                    IsBackground = true,
                    Name = "ActuatorManager-RX"
                };
                _rxThread.Start();
            }
            else
            {
                EnqueueLog("[SERIAL] Disabled");
            }

            EnqueueLog($"[ActuatorManager] Control authority profile: {AuthorityProfile}");
            EnqueueLog($"[SERIAL] SetSerialPort -> {(_serialPortName ?? "<disabled>")} @ {_serialBaud}");
        }

        /// <summary>
        /// Çalışma sırasında seri portu değiştirmek için kullanılır.
        /// </summary>
        public void SetSerialPort(string? port, int baud = 115200)
        {
            ThrowIfDisposed();

            lock (_serialLock)
            {
                try { _serial?.Close(); } catch { }
                try { _serial?.Dispose(); } catch { }

                _serial = null;
                _serialPortName = string.IsNullOrWhiteSpace(port) ? null : port;
                _serialBaud = baud;
                LastSerialError = null;
                _serialFaultLatched = false;

                if (_serialPortName is not null)
                    TryOpenSerial();
                else
                    EnqueueLog("[SERIAL] Disabled");
            }

            EnqueueLog($"[SERIAL] SetSerialPort -> {(_serialPortName ?? "<disabled>")} @ {_serialBaud}");
        }

        /// <summary>
        /// Wrench komutunu thruster komutlarına çevirir, uygular ve body-frame toplam wrench üretir.
        ///
        /// Bu akış artık yalnızca komut dağıtımı değildir:
        /// - hedef wrench alınır,
        /// - solver ile thruster çözümü üretilir,
        /// - health ve saturation dikkate alınır,
        /// - motor/ESC ters çalışma kabiliyeti dikkate alınır,
        /// - slew-rate uygulanır,
        /// - gerçek üretilebilen wrench hesaplanır,
        /// - allocation kalite raporu oluşturulur.
        /// </summary>
        public void Apply(DecisionCommand cmd)
        {
            ThrowIfDisposed();

            if (_thrusters.Count == 0)
            {
                EnqueueLog("[ActuatorManager] No thrusters configured");

                LastAllocationReport = new ActuatorAllocationReport(
                    Success: false,
                    Reason: "NO_THRUSTERS",
                    RequestedForceBody: new Vec3(cmd.Fx, cmd.Fy, cmd.Fz),
                    RequestedTorqueBody: new Vec3(cmd.Tx, cmd.Ty, cmd.Tz),
                    AchievedForceBody: Vec3.Zero,
                    AchievedTorqueBody: Vec3.Zero,
                    ForceErrorBody: new Vec3(cmd.Fx, cmd.Fy, cmd.Fz),
                    TorqueErrorBody: new Vec3(cmd.Tx, cmd.Ty, cmd.Tz),
                    NormalizedError: 1.0,
                    SaturationRatio: 0.0,
                    ActiveThrusterCount: 0,
                    HealthyThrusterCount: 0,
                    HadSaturation: false,
                    HadUnhealthyThruster: false,
                    AuthorityLimited: true,
                    ReverseClampCount: 0
                );

                if (_motorController != null)
                    _ = _motorController.ApplyAsync(cmd, CancellationToken.None);

                return;
            }

            Interlocked.Exchange(ref _lastApplyTicks, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _watchdogNeutralized, 0);

            var solve = SolveAllocation(cmd);

            bool healthChanged;
            Vec3 totalFBody;
            Vec3 totalTBody;
            ActuatorAllocationReport allocationReport;

            lock (_stateLock)
            {
                DateTime now = DateTime.UtcNow;
                double dt = ComputeThrusterSlewDt(now);

                bool hadSaturation = false;
                double saturationSum = 0.0;
                int activeThrusterCount = 0;
                int reverseClampCount = 0;

                for (int j = 0; j < _thrusters.Count; j++)
                {
                    var thruster = _thrusters[j];

                    double desired = j < solve.RawSolution.Length
                        ? solve.RawSolution[j]
                        : 0.0;

                    if (!double.IsFinite(desired))
                        desired = 0.0;

                    double beforeClamp = desired;
                    desired = Math.Clamp(desired, -1.0, 1.0);

                    if (Math.Abs(beforeClamp - desired) > 1e-9)
                        hadSaturation = true;

                    if (!thruster.IsHealthy)
                        desired = 0.0;

                    desired = ApplyThrusterCapability(thruster, desired, ref reverseClampCount);

                    double applied = ApplySlewLimit(thruster.Current, desired, dt);
                    applied = Math.Clamp(applied, thruster.CanReverse ? -1.0 : 0.0, 1.0);

                    thruster.Current = applied;
                    thruster.LastCommandUtc = now;

                    double abs = Math.Abs(applied);
                    saturationSum += abs;

                    if (abs > 1e-6)
                        activeThrusterCount++;
                }

                healthChanged = EvaluateFeedbackHealth_NoLock(now);

                (totalFBody, totalTBody) = ComputeAchievedWrench_NoLock();

                VehicleState = VehicleState with
                {
                    LinearForce = totalFBody,
                    AngularTorque = totalTBody
                };

                double saturationRatio = _thrusters.Count == 0
                    ? 0.0
                    : saturationSum / _thrusters.Count;

                allocationReport = BuildAllocationReport_NoLock(
                    solve,
                    totalFBody,
                    totalTBody,
                    hadSaturation,
                    activeThrusterCount,
                    saturationRatio,
                    reverseClampCount
                );

                LastAllocationReport = allocationReport;
            }

            if (healthChanged)
            {
                RebuildSolverCache_NoLockRequired();
                RecomputeAuthorityProfile_NoLockRequired();
                EnqueueLog($"[ActuatorManager] Health update → authority profile: {AuthorityProfile}");
            }

            WrenchComputed?.Invoke(totalFBody, totalTBody);

            EnqueueActuatorCommandFrame();
            PublishActuatorSummary(totalFBody, totalTBody);
            PublishAllocationSummary(allocationReport);

            if (_motorController != null)
                _ = _motorController.ApplyAsync(cmd, CancellationToken.None);
        }

        /// <summary>
        /// Motor/ESC fiziksel kabiliyetini uygular.
        ///
        /// CanReverse=true:
        ///   - Negatif ve pozitif normalize komutlar geçerlidir.
        ///
        /// CanReverse=false:
        ///   - Negatif komut fiziksel çıkışa gönderilemez.
        ///   - Negatif komut güvenli şekilde 0.0'a kırpılır.
        ///
        /// Not:
        /// Reversed etkisi Thruster constructor'ında ForceDir üzerine uygulanır.
        /// Bu metot yalnızca fiziksel motor çıkış kabiliyetini uygular.
        /// </summary>
        private static double ApplyThrusterCapability(
            Thruster thruster,
            double desired,
            ref int reverseClampCount)
        {
            if (!thruster.CanReverse && desired < 0.0)
            {
                reverseClampCount++;
                return 0.0;
            }

            return desired;
        }

        private double ComputeThrusterSlewDt(DateTime now)
        {
            DateTime? oldest = null;

            foreach (var t in _thrusters)
            {
                if (t.LastCommandUtc == default)
                    continue;

                if (oldest is null || t.LastCommandUtc < oldest.Value)
                    oldest = t.LastCommandUtc;
            }

            if (oldest is null)
                return 0.1;

            double dt = (now - oldest.Value).TotalSeconds;

            if (dt <= 1e-4)
                return 1e-4;

            if (dt > 0.25)
                return 0.25;

            return dt;
        }

        private double ApplySlewLimit(double current, double desired, double dt)
        {
            if (OutputSlewRatePerSec <= 0.0)
                return desired;

            double deltaMax = OutputSlewRatePerSec * dt;
            double delta = desired - current;

            if (Math.Abs(delta) <= deltaMax)
                return desired;

            return current + Math.Sign(delta) * deltaMax;
        }

        private void GenerateDeterministicSimThrusters()
        {
            _thrusters.Clear();

            double halfL = 0.60;
            double halfW = 0.25;
            double zMain = -0.20;
            double zUpper = 0.20;

            var defaults = new[]
            {
                new ThrusterDesc("SIM_CH0", 0, new Vec3(-halfL, +halfW, zMain), new Vec3(+1, 0, 0), false, true),
                new ThrusterDesc("SIM_CH1", 1, new Vec3(-halfL, -halfW, zMain), new Vec3(+1, 0, 0), false, true),
                new ThrusterDesc("SIM_CH2", 2, new Vec3(+halfL, +halfW, zUpper), new Vec3(0, 0, +1), false, true),
                new ThrusterDesc("SIM_CH3", 3, new Vec3(+halfL, -halfW, zUpper), new Vec3(0, 0, +1), false, true),
                new ThrusterDesc("SIM_CH4", 4, new Vec3(0.0, +halfW, zMain), new Vec3(+1, 0, 0), false, true),
                new ThrusterDesc("SIM_CH5", 5, new Vec3(0.0, -halfW, zMain), new Vec3(+1, 0, 0), false, true),
            };

            foreach (var t in defaults)
                _thrusters.Add(new Thruster(t));

            EnqueueLog("[ActuatorManager] SIM thruster layout:");

            foreach (var t in _thrusters.OrderBy(t => t.Channel))
                EnqueueLog($"  {t.Id}@ch{t.Channel}: Pos={Fmt(t.Position)} Dir={Fmt(t.ForceDir)} CanReverse={t.CanReverse}");

            EnqueueLog($"[ActuatorManager] SIM modda {_thrusters.Count} thruster oluşturuldu.");
        }
    }
}
