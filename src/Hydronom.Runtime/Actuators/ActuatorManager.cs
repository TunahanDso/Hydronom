// Hydronom.Runtime\Actuators\ActuatorManager.cs
using System;
using System.Buffers.Binary;
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
    /// Özellikler:
    /// - Thruster geometrisinden 6xM B matrisi üretir.
    /// - Ridge LS ile wrench -> thruster çözümü yapar.
    /// - COBS framed binary serial protokolü kullanır.
    /// - TX/RX worker thread’leri ile non-blocking çalışır.
    /// - 50 ms Apply-watchdog ile failsafe nötrleme yapar.
    /// - Thruster health / telemetry desteği vardır.
    /// - Sağlıksız thruster’ları authority ve çözücü dışına alır.
    /// - Çıkış rampası (slew-rate) destekler.
    ///
    /// Not:
    /// - Burada hesaplanan toplam kuvvet ve moment body-frame’dedir.
    /// - Dünya frame dönüşümü üst katmanda yapılmalıdır.
    /// </summary>
    public sealed class ActuatorManager : IActuator, IDisposable
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

        private readonly Thread _logThread;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly AutoResetEvent _logSignal = new(false);

        private readonly CancellationTokenSource _cts = new();

        private readonly Timer _watchdogTimer;
        private long _lastApplyTicks;
        private int _watchdogNeutralized;

        private readonly Thread? _rxThread;
        private ushort _commandSequence;
        private ushort _telemetrySequence;

        private volatile bool _serialFaultLatched;

        // Seri TX debug log spam'ini azaltmak için zaman damgası
        private long _lastCommandLogTicks;

        private const double SolverLambda = 0.02;
        private const int WatchdogTimeoutMs = 250;
        private const int WatchdogTickMs = 10;
        private const int MaxProtocolThrusters = 16;
        private const byte ProtocolVersion = 1;
        private const byte MsgTypeCommand = 0x10;
        private const byte MsgTypeTelemetry = 0x20;

        /// <summary>
        /// Her motor için maksimum itki (N).
        /// </summary>
        public double MaxThrustN { get; set; } = 22.0;

        /// <summary>
        /// Hedef wrench tarafında moment eksenlerini ölçeklemek için.
        /// </summary>
        public double TorqueWeight { get; set; } = 1.0;

        /// <summary>
        /// Thruster health değerlendirmesinde anlamlı komut eşiği.
        /// </summary>
        public double CommandActivityThreshold { get; set; } = 0.15;

        /// <summary>
        /// Jam şüphesi için akım eşiği.
        /// </summary>
        public int JamCurrentThresholdMilliAmp { get; set; } = 1200;

        /// <summary>
        /// Jam şüphesi için düşük RPM eşiği.
        /// </summary>
        public int JamRpmThreshold { get; set; } = 80;

        /// <summary>
        /// Telemetry stale timeout.
        /// </summary>
        public int FeedbackStaleTimeoutMs { get; set; } = 250;

        /// <summary>
        /// Thruster çıkış rampası: normalize çıkış için birim/s.
        /// 0 veya negatif verilirse kapalı kabul edilir.
        /// </summary>
        public double OutputSlewRatePerSec { get; set; } = 25.0;

        /// <summary>
        /// Son hesaplanan body-frame toplam wrench.
        /// </summary>
        public VehicleState VehicleState { get; private set; } = VehicleState.Zero;

        public Vec3 LastForceBody => VehicleState.LinearForce;
        public Vec3 LastTorqueBody => VehicleState.AngularTorque;

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
        /// Wrench komutunu thruster komutlarına çevirir, uygular ve body wrench üretir.
        /// </summary>
        public void Apply(DecisionCommand cmd)
        {
            ThrowIfDisposed();

            if (_thrusters.Count == 0)
            {
                EnqueueLog("[ActuatorManager] No thrusters configured");
                if (_motorController != null)
                    _ = _motorController.ApplyAsync(cmd, CancellationToken.None);
                return;
            }

            Interlocked.Exchange(ref _lastApplyTicks, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _watchdogNeutralized, 0);

            double[] td = new double[6]
            {
                cmd.Fx,
                cmd.Fy,
                cmd.Fz,
                cmd.Tx * TorqueWeight,
                cmd.Ty * TorqueWeight,
                cmd.Tz * TorqueWeight
            };

            SolverCache solver;
            lock (_stateLock)
                solver = _solverCache;

            double[] u = SolveWithCache(solver, td);

            bool healthChanged;
            Vec3 totalFBody;
            Vec3 totalTBody;

            lock (_stateLock)
            {
                DateTime now = DateTime.UtcNow;
                double dt = ComputeThrusterSlewDt(now);

                for (int j = 0; j < _thrusters.Count; j++)
                {
                    double desired = Math.Clamp(u[j], -1.0, 1.0);

                    if (!_thrusters[j].IsHealthy)
                        desired = 0.0;

                    double applied = ApplySlewLimit(_thrusters[j].Current, desired, dt);
                    _thrusters[j].Current = applied;
                    _thrusters[j].LastCommandUtc = now;
                }

                healthChanged = EvaluateFeedbackHealth_NoLock(now);

                totalFBody = Vec3.Zero;
                totalTBody = Vec3.Zero;

                foreach (var t in _thrusters)
                {
                    if (!t.IsHealthy)
                        continue;

                    Vec3 force = t.ForceDir * (t.Current * MaxThrustN);
                    Vec3 torque = Vec3.Cross(t.Position, force);

                    totalFBody += force;
                    totalTBody += torque;
                }

                VehicleState = VehicleState with
                {
                    LinearForce = totalFBody,
                    AngularTorque = totalTBody
                };
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

            if (_motorController != null)
                _ = _motorController.ApplyAsync(cmd, CancellationToken.None);
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
            if (dt <= 1e-4) return 1e-4;
            if (dt > 0.25) return 0.25;
            return dt;
        }

        private double ApplySlewLimit(double current, double desired, double dt)
        {
            if (OutputSlewRatePerSec <= 0)
                return desired;

            double deltaMax = OutputSlewRatePerSec * dt;
            double delta = desired - current;

            if (Math.Abs(delta) <= deltaMax)
                return desired;

            return current + Math.Sign(delta) * deltaMax;
        }

        /// <summary>
        /// Geometriye bağlı sabit B matrisi.
        /// </summary>
        private double[,] BuildThrusterMatrixFromGeometry()
        {
            int m = _thrusters.Count;
            double[,] b = new double[6, m];

            for (int j = 0; j < m; j++)
            {
                var t = _thrusters[j];
                Vec3 dir = t.ForceDir;
                Vec3 r = t.Position;
                Vec3 torquePerUnit = Vec3.Cross(r, dir);

                b[0, j] = dir.X * MaxThrustN;
                b[1, j] = dir.Y * MaxThrustN;
                b[2, j] = dir.Z * MaxThrustN;

                b[3, j] = torquePerUnit.X * MaxThrustN;
                b[4, j] = torquePerUnit.Y * MaxThrustN;
                b[5, j] = torquePerUnit.Z * MaxThrustN;
            }

            return b;
        }

        private void RebuildSolverCache_NoLockRequired()
        {
            lock (_stateLock)
            {
                int rows = _baseB.GetLength(0);
                int cols = _baseB.GetLength(1);

                if (rows == 0 || cols == 0)
                {
                    _solverCache = SolverCache.Empty;
                    return;
                }

                double[,] bEff = new double[rows, cols];

                for (int j = 0; j < cols; j++)
                {
                    double gain = _thrusters[j].IsHealthy ? 1.0 : 0.0;
                    for (int i = 0; i < rows; i++)
                        bEff[i, j] = _baseB[i, j] * gain;
                }

                double[] colScale = new double[cols];
                const double minNorm = 1e-6;

                for (int j = 0; j < cols; j++)
                {
                    double norm = 0.0;
                    for (int i = 0; i < rows; i++)
                    {
                        double v = bEff[i, j];
                        norm += v * v;
                    }

                    norm = Math.Sqrt(norm);
                    if (norm < minNorm)
                        norm = 1.0;

                    colScale[j] = norm;
                }

                double[,] bs = new double[rows, cols];
                for (int j = 0; j < cols; j++)
                {
                    double s = colScale[j];
                    for (int i = 0; i < rows; i++)
                        bs[i, j] = bEff[i, j] / s;
                }

                double[,] a = new double[cols, cols];
                for (int i = 0; i < cols; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        double sum = 0.0;
                        for (int k = 0; k < rows; k++)
                            sum += bs[k, i] * bs[k, j];

                        if (i == j)
                            sum += SolverLambda;

                        a[i, j] = sum;
                    }
                }

                double[,] aInv = InvertMatrix(a);

                _solverCache = new SolverCache(
                    bEff,
                    bs,
                    colScale,
                    aInv,
                    _thrusters.Select(t => t.IsHealthy).ToArray()
                );
            }
        }

        private void RecomputeAuthorityProfile_NoLockRequired()
        {
            lock (_stateLock)
            {
                if (_thrusters.Count == 0)
                {
                    _authorityProfile = ControlAuthorityProfile.Empty;
                    return;
                }

                int rows = _baseB.GetLength(0);
                int cols = _baseB.GetLength(1);

                AxisAuthority[] axes = new AxisAuthority[rows];

                for (int i = 0; i < rows; i++)
                {
                    double pos = 0.0;
                    double neg = 0.0;

                    for (int j = 0; j < cols; j++)
                    {
                        if (!_thrusters[j].IsHealthy)
                            continue;

                        double v = _baseB[i, j];
                        if (v > 0) pos += v;
                        else if (v < 0) neg += -v;
                    }

                    axes[i] = new AxisAuthority(pos, neg);
                }

                _authorityProfile = new ControlAuthorityProfile(
                    Fx: axes[0],
                    Fy: axes[1],
                    Fz: axes[2],
                    Tx: axes[3],
                    Ty: axes[4],
                    Tz: axes[5]
                );
            }
        }

        /// <summary>
        /// Thruster health güncellemesi. true dönerse solver/authority yenilenmeli.
        /// </summary>
        private bool EvaluateFeedbackHealth_NoLock(DateTime now)
        {
            bool changed = false;

            foreach (var t in _thrusters)
            {
                bool previousHealth = t.IsHealthy;

                if (t.LastFeedbackUtc != default &&
                    (now - t.LastFeedbackUtc).TotalMilliseconds > FeedbackStaleTimeoutMs)
                {
                    t.HealthFlags |= ThrusterHealthFlags.TelemetryStale;
                    t.IsHealthy = false;
                }
                else if (t.LastFeedbackUtc != default &&
                         (now - t.LastFeedbackUtc).TotalMilliseconds <= FeedbackStaleTimeoutMs)
                {
                    t.HealthFlags &= ~ThrusterHealthFlags.TelemetryStale;
                    t.IsHealthy = t.HealthFlags == ThrusterHealthFlags.None;
                }

                if (previousHealth != t.IsHealthy)
                    changed = true;
            }

            return changed;
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
                new ThrusterDesc("SIM_CH0", 0, new Vec3(-halfL, +halfW, zMain), new Vec3(+1, 0, 0), false),
                new ThrusterDesc("SIM_CH1", 1, new Vec3(-halfL, -halfW, zMain), new Vec3(+1, 0, 0), false),
                new ThrusterDesc("SIM_CH2", 2, new Vec3(+halfL, +halfW, zUpper), new Vec3(0, 0, +1), false),
                new ThrusterDesc("SIM_CH3", 3, new Vec3(+halfL, -halfW, zUpper), new Vec3(0, 0, +1), false),
                new ThrusterDesc("SIM_CH4", 4, new Vec3(0.0, +halfW, zMain), new Vec3(+1, 0, 0), false),
                new ThrusterDesc("SIM_CH5", 5, new Vec3(0.0, -halfW, zMain), new Vec3(+1, 0, 0), false),
            };

            foreach (var t in defaults)
                _thrusters.Add(new Thruster(t));

            EnqueueLog("[ActuatorManager] SIM thruster layout:");
            foreach (var t in _thrusters.OrderBy(t => t.Channel))
            {
                EnqueueLog($"  {t.Id}@ch{t.Channel}: Pos={Fmt(t.Position)} Dir={Fmt(t.ForceDir)}");
            }

            EnqueueLog($"[ActuatorManager] SIM modda {_thrusters.Count} thruster oluşturuldu.");
        }

        private void TryOpenSerial()
        {
            if (_serialPortName is null)
                return;

            lock (_serialLock)
            {
                try
                {
                    _serial = new SerialPort(_serialPortName, _serialBaud)
                    {
                        ReadTimeout = 100,
                        WriteTimeout = 100
                    };

                    _serial.Open();
                    LastSerialError = null;
                    _serialFaultLatched = false;
                    EnqueueLog($"[SERIAL] Opened {_serialPortName} @ {_serialBaud}");
                }
                catch (Exception ex)
                {
                    LastSerialError = ex.Message;
                    _serialFaultLatched = true;
                    EnqueueLog($"[SERIAL] Open failed: {ex.Message}");

                    try { _serial?.Dispose(); } catch { }
                    _serial = null;
                }
            }
        }

        private void EnqueueActuatorCommandFrame()
        {
            if (_serialPortName is null)
                return;

            byte[] payload = BuildCommandPayload();

            DebugLogThrusterStateBeforeSerialize();
            DebugLogOutgoingCommandPayload(payload);

            byte[] encoded = CobsEncode(payload);

            var framed = new byte[encoded.Length + 1];
            Buffer.BlockCopy(encoded, 0, framed, 0, encoded.Length);
            framed[^1] = 0x00;

            _txQueue.Enqueue(framed);
            _txSignal.Set();
        }

        private void DebugLogThrusterStateBeforeSerialize()
        {
            try
            {
                long nowTicks = Stopwatch.GetTimestamp();
                double elapsedMs = (nowTicks - Interlocked.Read(ref _lastCommandLogTicks)) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < 200.0)
                    return;

                lock (_stateLock)
                {
                    string text = string.Join(" ",
                        _thrusters
                            .OrderBy(t => t.Channel)
                            .Select(t => $"{t.Id}@ch{t.Channel} cur={t.Current:F3} healthy={t.IsHealthy}"));

                    EnqueueLog("[THRUSTER-STATE] " + text);
                }
            }
            catch (Exception ex)
            {
                EnqueueLog("[THRUSTER-STATE] log failed: " + ex.Message);
            }
        }

        private void DebugLogOutgoingCommandPayload(byte[] payload)
        {
            try
            {
                long nowTicks = Stopwatch.GetTimestamp();
                double elapsedMs = (nowTicks - Interlocked.Read(ref _lastCommandLogTicks)) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < 200.0)
                    return;

                Interlocked.Exchange(ref _lastCommandLogTicks, nowTicks);

                ushort seq = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2, 2));
                uint uptimeMs = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, 4));
                byte thrusterCount = payload[8];
                byte flags = payload[9];

                var q = new short[MaxProtocolThrusters];
                for (int i = 0; i < MaxProtocolThrusters; i++)
                    q[i] = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10 + i * 2, 2));

                string qText = string.Join(", ", q.Select((v, i) => $"ch{i}={v}"));
                string hex = BitConverter.ToString(payload);

                EnqueueLog($"[SERIAL-TX] seq={seq} uptime={uptimeMs}ms thrusters={thrusterCount} flags=0x{flags:X2} | {qText}");
                EnqueueLog($"[SERIAL-TX-HEX] {hex}");
            }
            catch (Exception ex)
            {
                EnqueueLog("[SERIAL-TX-DEBUG] log failed: " + ex.Message);
            }
        }

        /// <summary>
        /// [0] MsgType
        /// [1] Version
        /// [2..3] Sequence
        /// [4..7] UptimeMs
        /// [8] ThrusterCount
        /// [9] Flags
        /// [10..41] 16 kanal int16
        /// [42..43] CRC16
        /// </summary>
        private byte[] BuildCommandPayload()
        {
            const int payloadLength = 44;
            byte[] payload = new byte[payloadLength];

            payload[0] = MsgTypeCommand;
            payload[1] = ProtocolVersion;

            ushort seq = unchecked(++_commandSequence);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), seq);

            uint uptimeMs = unchecked((uint)Environment.TickCount64);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), uptimeMs);

            payload[8] = (byte)_thrusters.Count;
            payload[9] = _serialFaultLatched ? (byte)0x01 : (byte)0x00;

            lock (_stateLock)
            {
                for (int i = 0; i < MaxProtocolThrusters; i++)
                {
                    short q = 0;

                    if (i < _thrusters.Count)
                    {
                        double val = _thrusters[i].IsHealthy ? _thrusters[i].Current : 0.0;
                        q = QuantizeNormalizedSigned(val);
                    }

                    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10 + i * 2, 2), q);
                }
            }

            ushort crc = Crc16Ccitt(payload.AsSpan(0, payloadLength - 2));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(payloadLength - 2, 2), crc);

            return payload;
        }

        private void RxWorkerLoop()
        {
            List<byte> frame = new(128);

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    SerialPort? port;
                    lock (_serialLock)
                        port = _serial;

                    if (port == null || !port.IsOpen)
                    {
                        TryReopenSerialIfNeeded();
                        Thread.Sleep(50);
                        continue;
                    }

                    int b = port.ReadByte();
                    if (b < 0)
                        continue;

                    if (b == 0x00)
                    {
                        if (frame.Count > 0)
                        {
                            byte[] encoded = frame.ToArray();
                            frame.Clear();

                            if (TryCobsDecode(encoded, out var payload))
                                ProcessInboundPayload(payload);
                            else
                                EnqueueLog("[SERIAL] RX decode failed");
                        }

                        continue;
                    }

                    frame.Add((byte)b);

                    if (frame.Count > 512)
                    {
                        frame.Clear();
                        EnqueueLog("[SERIAL] RX frame overflow, drop");
                    }
                }
                catch (TimeoutException)
                {
                }
                catch (Exception ex)
                {
                    LastSerialError = ex.Message;
                    _serialFaultLatched = true;
                    EnqueueLog("[SERIAL] RX failed: " + ex.Message);
                    Thread.Sleep(50);
                }
            }
        }

        private void TryReopenSerialIfNeeded()
        {
            if (_serialPortName is null)
                return;

            lock (_serialLock)
            {
                if (_serial?.IsOpen == true)
                    return;
            }

            TryOpenSerial();
        }

        private void ProcessInboundPayload(byte[] payload)
        {
            if (payload.Length < 4)
                return;

            byte msgType = payload[0];
            byte version = payload[1];

            if (version != ProtocolVersion)
            {
                EnqueueLog($"[SERIAL] RX protocol mismatch: {version}");
                return;
            }

            switch (msgType)
            {
                case MsgTypeTelemetry:
                    if (TryParseTelemetryPayload(payload))
                        _serialFaultLatched = false;
                    break;

                default:
                    EnqueueLog($"[SERIAL] RX unknown msg type: 0x{msgType:X2}");
                    break;
            }
        }

        /// <summary>
        /// [0] MsgType
        /// [1] Version
        /// [2..3] Sequence
        /// [4] ThrusterCount
        /// [5] Flags
        /// [6..69] 16 kanal için current_mA + rpm
        /// [70..71] CRC16
        /// </summary>
        private bool TryParseTelemetryPayload(byte[] payload)
        {
            const int payloadLength = 72;
            if (payload.Length != payloadLength)
            {
                EnqueueLog($"[SERIAL] Telemetry size mismatch: {payload.Length}");
                return false;
            }

            ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(payloadLength - 2, 2));
            ushort actual = Crc16Ccitt(payload.AsSpan(0, payloadLength - 2));

            if (expected != actual)
            {
                EnqueueLog("[SERIAL] Telemetry CRC mismatch");
                return false;
            }

            _telemetrySequence = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2, 2));
            int count = payload[4];
            byte flags = payload[5];

            bool changed = false;

            lock (_stateLock)
            {
                int usable = Math.Min(Math.Min(count, _thrusters.Count), MaxProtocolThrusters);

                for (int i = 0; i < usable; i++)
                {
                    int offset = 6 + i * 4;
                    short currentMa = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, 2));
                    short rpm = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset + 2, 2));

                    var t = _thrusters[i];
                    bool previousHealth = t.IsHealthy;

                    t.CurrentSenseMilliAmp = currentMa;
                    t.RpmFeedback = rpm;
                    t.HealthFlags = ThrusterHealthFlags.None;
                    t.LastFeedbackUtc = DateTime.UtcNow;

                    if ((flags & 0x01) != 0)
                        t.HealthFlags |= ThrusterHealthFlags.ControllerWarning;

                    if (Math.Abs(t.Current) >= CommandActivityThreshold &&
                        Math.Abs(currentMa) >= JamCurrentThresholdMilliAmp &&
                        Math.Abs(rpm) <= JamRpmThreshold)
                    {
                        t.HealthFlags |= ThrusterHealthFlags.JamSuspected;
                    }

                    t.IsHealthy = t.HealthFlags == ThrusterHealthFlags.None;

                    if (previousHealth != t.IsHealthy)
                        changed = true;
                }

                for (int i = usable; i < _thrusters.Count; i++)
                {
                    var t = _thrusters[i];
                    bool previousHealth = t.IsHealthy;

                    if (t.LastFeedbackUtc != default &&
                        (DateTime.UtcNow - t.LastFeedbackUtc).TotalMilliseconds > FeedbackStaleTimeoutMs)
                    {
                        t.HealthFlags |= ThrusterHealthFlags.TelemetryStale;
                        t.IsHealthy = false;
                    }

                    if (previousHealth != t.IsHealthy)
                        changed = true;
                }

            }

            if (changed)
            {
                RebuildSolverCache_NoLockRequired();
                RecomputeAuthorityProfile_NoLockRequired();
                EnqueueLog($"[ActuatorManager] Health update → authority profile: {AuthorityProfile}");
            }

            return true;
        }

        private void PublishActuatorSummary(Vec3 totalFBody, Vec3 totalTBody)
        {
            string summary;
            lock (_stateLock)
            {
                var ordered = _thrusters
                    .OrderBy(mtr => mtr.Channel)
                    .Select(mtr =>
                    {
                        string health = mtr.IsHealthy ? "OK" : $"FAULT:{mtr.HealthFlags}";
                        return $"{mtr.Id}={mtr.Current:F2}[{health}|I={mtr.CurrentSenseMilliAmp}mA|RPM={mtr.RpmFeedback}]";
                    });

                summary = $"[Actuator] {string.Join(" ", ordered)} | F_body={Fmt(totalFBody)} T_body={Fmt(totalTBody)}";
            }

            EnqueueLog(summary);
        }

        private void TxWorkerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _txSignal.WaitOne(50);

                    while (_txQueue.TryDequeue(out var frame))
                    {
                        SerialPort? port;
                        lock (_serialLock)
                            port = _serial;

                        if (port == null || !port.IsOpen)
                        {
                            TryReopenSerialIfNeeded();

                            lock (_serialLock)
                                port = _serial;

                            if (port == null || !port.IsOpen)
                            {
                                _serialFaultLatched = true;
                                LastSerialError = "Serial not available";
                                continue;
                            }
                        }

                        port.Write(frame, 0, frame.Length);
                        LastSerialError = null;
                    }
                }
                catch (Exception ex)
                {
                    LastSerialError = ex.Message;
                    _serialFaultLatched = true;
                    EnqueueLog("[SERIAL] TX failed: " + ex.Message);
                    Thread.Sleep(20);
                }
            }
        }

        private void WatchdogTick(object? _)
        {
            if (_disposed)
                return;

            long last = Interlocked.Read(ref _lastApplyTicks);
            double elapsedMs = StopwatchTicksToMs(Stopwatch.GetTimestamp() - last);

            if (elapsedMs <= WatchdogTimeoutMs)
                return;

            if (Interlocked.CompareExchange(ref _watchdogNeutralized, 1, 0) != 0)
                return;

            NeutralizeOutputs("WATCHDOG");
        }

        private void NeutralizeOutputs(string reason)
        {
            try
            {
                lock (_stateLock)
                {
                    foreach (var t in _thrusters)
                        t.Current = 0.0;

                    VehicleState = VehicleState with
                    {
                        LinearForce = Vec3.Zero,
                        AngularTorque = Vec3.Zero
                    };
                }

                WrenchComputed?.Invoke(Vec3.Zero, Vec3.Zero);
                EnqueueActuatorCommandFrame();
                EnqueueLog($"[FAILSAFE] {reason} -> all thrusters neutralized");

                if (_motorController != null)
                {
                    var neutral = DecisionCommand.Zero;
                    _ = _motorController.ApplyAsync(neutral, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                EnqueueLog("[FAILSAFE] Neutralize failed: " + ex.Message);
            }
        }

        private void LogWorkerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _logSignal.WaitOne(100);

                    while (_logQueue.TryDequeue(out var msg))
                    {
                        try
                        {
                            Console.WriteLine(msg);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void EnqueueLog(string message)
        {
            if (_disposed)
                return;

            _logQueue.Enqueue(message);
            _logSignal.Set();
        }

        private static short QuantizeNormalizedSigned(double value)
        {
            double clamped = Math.Clamp(value, -1.0, 1.0);
            return (short)Math.Round(clamped * 1000.0);
        }

        private static double[] SolveWithCache(SolverCache cache, double[] td)
        {
            if (cache.IsEmpty)
                return Array.Empty<double>();

            int rows = cache.Bs.GetLength(0);
            int cols = cache.Bs.GetLength(1);

            double[] b = new double[cols];

            for (int i = 0; i < cols; i++)
            {
                double sum = 0.0;
                for (int k = 0; k < rows; k++)
                    sum += cache.Bs[k, i] * td[k];
                b[i] = sum;
            }

            double[] w = Multiply(cache.AInv, b);
            double[] u = new double[cols];

            for (int j = 0; j < cols; j++)
            {
                u[j] = w[j] / cache.ColScale[j];
                if (!cache.ActiveMask[j])
                    u[j] = 0.0;
            }

            return u;
        }

        private static double[] Multiply(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            if (cols != vector.Length)
                throw new InvalidOperationException("Matrix/vector boyutu uyumsuz.");

            double[] result = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double sum = 0.0;
                for (int j = 0; j < cols; j++)
                    sum += matrix[i, j] * vector[j];
                result[i] = sum;
            }

            return result;
        }

        private static double[,] InvertMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            if (n != matrix.GetLength(1))
                throw new InvalidOperationException("Sadece kare matris terslenebilir.");

            double[,] a = new double[n, n * 2];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    a[i, j] = matrix[i, j];

                a[i, n + i] = 1.0;
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double max = Math.Abs(a[col, col]);

                for (int row = col + 1; row < n; row++)
                {
                    double v = Math.Abs(a[row, col]);
                    if (v > max)
                    {
                        max = v;
                        pivot = row;
                    }
                }

                if (max < 1e-12)
                    throw new InvalidOperationException("Matris terslenemiyor; geometri veya lambda kontrol edilmeli.");

                if (pivot != col)
                {
                    for (int j = 0; j < n * 2; j++)
                    {
                        double tmp = a[col, j];
                        a[col, j] = a[pivot, j];
                        a[pivot, j] = tmp;
                    }
                }

                double diag = a[col, col];
                for (int j = 0; j < n * 2; j++)
                    a[col, j] /= diag;

                for (int row = 0; row < n; row++)
                {
                    if (row == col)
                        continue;

                    double factor = a[row, col];
                    if (Math.Abs(factor) < 1e-15)
                        continue;

                    for (int j = 0; j < n * 2; j++)
                        a[row, j] -= factor * a[col, j];
                }
            }

            double[,] inv = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    inv[i, j] = a[i, n + j];
            }

            return inv;
        }

        private static ushort Crc16Ccitt(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;

            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }

            return crc;
        }

        private static byte[] CobsEncode(ReadOnlySpan<byte> input)
        {
            byte[] output = new byte[input.Length + input.Length / 254 + 2];

            int readIndex = 0;
            int writeIndex = 1;
            int codeIndex = 0;
            byte code = 1;

            while (readIndex < input.Length)
            {
                if (input[readIndex] == 0)
                {
                    output[codeIndex] = code;
                    code = 1;
                    codeIndex = writeIndex++;
                    readIndex++;
                }
                else
                {
                    output[writeIndex++] = input[readIndex++];
                    code++;

                    if (code == 0xFF)
                    {
                        output[codeIndex] = code;
                        code = 1;
                        codeIndex = writeIndex++;
                    }
                }
            }

            output[codeIndex] = code;

            byte[] result = new byte[writeIndex];
            Buffer.BlockCopy(output, 0, result, 0, writeIndex);
            return result;
        }

        private static bool TryCobsDecode(ReadOnlySpan<byte> input, out byte[] output)
        {
            output = Array.Empty<byte>();

            try
            {
                byte[] buffer = new byte[input.Length];
                int readIndex = 0;
                int writeIndex = 0;

                while (readIndex < input.Length)
                {
                    byte code = input[readIndex];
                    if (code == 0)
                        return false;

                    readIndex++;

                    for (int i = 1; i < code; i++)
                    {
                        if (readIndex >= input.Length)
                            return false;

                        buffer[writeIndex++] = input[readIndex++];
                    }

                    if (code < 0xFF && readIndex < input.Length)
                        buffer[writeIndex++] = 0;
                }

                output = new byte[writeIndex];
                Buffer.BlockCopy(buffer, 0, output, 0, writeIndex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double StopwatchTicksToMs(long ticks)
            => ticks * 1000.0 / Stopwatch.Frequency;

        private static string Fmt(Vec3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActuatorManager));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _watchdogTimer.Dispose(); } catch { }
            try { _cts.Cancel(); } catch { }

            _txSignal.Set();
            _logSignal.Set();

            try { _txThread.Join(200); } catch { }
            try { _logThread.Join(200); } catch { }
            try { _rxThread?.Join(200); } catch { }

            lock (_serialLock)
            {
                try { _serial?.Close(); } catch { }
                try { _serial?.Dispose(); } catch { }
                _serial = null;
            }

            _txSignal.Dispose();
            _logSignal.Dispose();
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }

        private readonly record struct SolverCache(
            double[,] B,
            double[,] Bs,
            double[] ColScale,
            double[,] AInv,
            bool[] ActiveMask)
        {
            public static SolverCache Empty { get; } =
                new SolverCache(
                    new double[0, 0],
                    new double[0, 0],
                    Array.Empty<double>(),
                    new double[0, 0],
                    Array.Empty<bool>());

            public bool IsEmpty => ColScale.Length == 0;
        }
    }

    /// <summary>
    /// Thruster geometri tanımı.
    /// </summary>
    public readonly record struct ThrusterDesc(
        string Id,
        int Channel,
        Vec3 Position,
        Vec3 ForceDir,
        bool Reversed = false
    );

    /// <summary>
    /// Gerçek zamanlı thruster nesnesi.
    /// </summary>
    public sealed class Thruster
    {
        public string Id { get; }
        public int Channel { get; }
        public Vec3 Position { get; }
        public Vec3 ForceDir { get; }
        public bool Reversed { get; }

        public double Current { get; set; }
        public int CurrentSenseMilliAmp { get; set; }
        public int RpmFeedback { get; set; }
        public ThrusterHealthFlags HealthFlags { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastCommandUtc { get; set; }
        public DateTime LastFeedbackUtc { get; set; }

        public Thruster(ThrusterDesc d)
        {
            Id = d.Id;
            Channel = d.Channel;
            Position = d.Position;

            var dir = d.ForceDir;
            if (d.Reversed)
                dir *= -1.0;

            ForceDir = dir.Normalize();
            Reversed = d.Reversed;
        }
    }

    [Flags]
    public enum ThrusterHealthFlags
    {
        None = 0,
        TelemetryStale = 1 << 0,
        JamSuspected = 1 << 1,
        ControllerWarning = 1 << 2
    }

    public readonly record struct AxisAuthority(double Positive, double Negative)
    {
        public bool HasPositive => Positive > 1e-6;
        public bool HasNegative => Negative > 1e-6;
        public double Span => Positive + Negative;

        public override string ToString() => $"(+{Positive:F2}/-{Negative:F2})";
    }

    public readonly record struct ControlAuthorityProfile(
        AxisAuthority Fx,
        AxisAuthority Fy,
        AxisAuthority Fz,
        AxisAuthority Tx,
        AxisAuthority Ty,
        AxisAuthority Tz)
    {
        public bool CanSurge => Fx.Span > 1e-6;
        public bool CanSway => Fy.Span > 1e-6;
        public bool CanHeave => Fz.Span > 1e-6;
        public bool CanRoll => Tx.Span > 1e-6;
        public bool CanPitch => Ty.Span > 1e-6;
        public bool CanYaw => Tz.Span > 1e-6;

        public static ControlAuthorityProfile Empty { get; } =
            new ControlAuthorityProfile(
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0),
                new AxisAuthority(0, 0)
            );

        public override string ToString()
            => $"Fx{Fx} Fy{Fy} Fz{Fz} Tx{Tx} Ty{Ty} Tz{Tz}";
    }
}