using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// ActuatorManager serial protocol bÃ¶lÃ¼mÃ¼.
    ///
    /// Bu partial dosya ÅŸunlardan sorumludur:
    /// - COBS framed binary serial protokolÃ¼
    /// - Komut payload Ã¼retimi
    /// - Telemetry payload parse
    /// - TX/RX worker dÃ¶ngÃ¼leri
    /// - CRC16 doÄŸrulama
    ///
    /// Protokol:
    /// - Frame delimiter: 0x00
    /// - Encoding: COBS
    /// - Command message: 0x10
    /// - Telemetry message: 0x20
    /// </summary>
    public sealed partial class ActuatorManager
    {
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

        /// <summary>
        /// Command payload format:
        ///
        /// [0]      MsgType
        /// [1]      Version
        /// [2..3]   Sequence
        /// [4..7]   UptimeMs
        /// [8]      ThrusterCount
        /// [9]      Flags
        /// [10..41] 16 kanal int16 normalized command
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

            payload[8] = (byte)Math.Min(_thrusters.Count, MaxProtocolThrusters);
            payload[9] = _serialFaultLatched ? (byte)0x01 : (byte)0x00;

            lock (_stateLock)
            {
                for (int i = 0; i < MaxProtocolThrusters; i++)
                {
                    short q = 0;

                    if (i < _thrusters.Count)
                    {
                        var thruster = _thrusters[i];

                        double val = thruster.IsHealthy
                            ? thruster.Current
                            : 0.0;

                        val = SanitizeProtocolCommand(thruster, val);

                        q = QuantizeNormalizedSigned(val);
                    }

                    BinaryPrimitives.WriteInt16LittleEndian(
                        payload.AsSpan(10 + i * 2, 2),
                        q
                    );
                }
            }

            ushort crc = Crc16Ccitt(payload.AsSpan(0, payloadLength - 2));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(payloadLength - 2, 2), crc);

            return payload;
        }

        /// <summary>
        /// Serial protokole yazmadan Ã¶nce son gÃ¼venlik filtresi.
        ///
        /// Apply aÅŸamasÄ±nda zaten CanReverse uygulanÄ±r; bu metot ise
        /// serial payload iÃ§in ikinci savunma hattÄ±dÄ±r.
        /// </summary>
        private static double SanitizeProtocolCommand(Thruster thruster, double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (!thruster.CanReverse && value < 0.0)
                value = 0.0;

            return Math.Clamp(value, thruster.CanReverse ? -1.0 : 0.0, 1.0);
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
        /// Telemetry payload format:
        ///
        /// [0]      MsgType
        /// [1]      Version
        /// [2..3]   Sequence
        /// [4]      ThrusterCount
        /// [5]      Flags
        /// [6..69]  16 kanal iÃ§in current_mA + rpm
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
                int usable = Math.Min(
                    Math.Min(count, _thrusters.Count),
                    MaxProtocolThrusters
                );

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

                EnqueueLog($"[ActuatorManager] Health update â†’ authority profile: {AuthorityProfile}");
            }

            return true;
        }

        private void DebugLogThrusterStateBeforeSerialize()
        {
            try
            {
                long nowTicks = Stopwatch.GetTimestamp();

                double elapsedMs =
                    (nowTicks - Interlocked.Read(ref _lastCommandLogTicks)) *
                    1000.0 /
                    Stopwatch.Frequency;

                if (elapsedMs < 200.0)
                    return;

                lock (_stateLock)
                {
                    string text = string.Join(
                        " ",
                        _thrusters
                            .OrderBy(t => t.Channel)
                            .Select(t =>
                                $"{t.Id}@ch{t.Channel} " +
                                $"cur={t.Current:F3} " +
                                $"canRev={t.CanReverse} " +
                                $"healthy={t.IsHealthy}")
                    );

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

                double elapsedMs =
                    (nowTicks - Interlocked.Read(ref _lastCommandLogTicks)) *
                    1000.0 /
                    Stopwatch.Frequency;

                if (elapsedMs < 200.0)
                    return;

                Interlocked.Exchange(ref _lastCommandLogTicks, nowTicks);

                ushort seq = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2, 2));
                uint uptimeMs = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, 4));
                byte thrusterCount = payload[8];
                byte flags = payload[9];

                var q = new short[MaxProtocolThrusters];

                for (int i = 0; i < MaxProtocolThrusters; i++)
                {
                    q[i] = BinaryPrimitives.ReadInt16LittleEndian(
                        payload.AsSpan(10 + i * 2, 2)
                    );
                }

                string qText = string.Join(
                    ", ",
                    q.Select((v, i) => $"ch{i}={v}")
                );

                string hex = BitConverter.ToString(payload);

                EnqueueLog($"[SERIAL-TX] seq={seq} uptime={uptimeMs}ms thrusters={thrusterCount} flags=0x{flags:X2} | {qText}");
                EnqueueLog($"[SERIAL-TX-HEX] {hex}");
            }
            catch (Exception ex)
            {
                EnqueueLog("[SERIAL-TX-DEBUG] log failed: " + ex.Message);
            }
        }

        private static short QuantizeNormalizedSigned(double value)
        {
            double clamped = Math.Clamp(value, -1.0, 1.0);
            return (short)Math.Round(clamped * 1000.0);
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
    }
}
