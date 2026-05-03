using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

namespace Hydronom.Runtime.Actuators
{
    /// <summary>
    /// ActuatorManager diagnostics / health / watchdog / logging bÃ¶lÃ¼mÃ¼.
    ///
    /// Bu partial dosya ÅŸunlardan sorumludur:
    /// - Thruster feedback health deÄŸerlendirmesi
    /// - Watchdog failsafe
    /// - TÃ¼m thruster Ã§Ä±kÄ±ÅŸlarÄ±nÄ± nÃ¶trleme
    /// - Actuator ve allocation summary loglarÄ±
    /// - Log worker dÃ¶ngÃ¼sÃ¼
    /// - Dispose sÄ±rasÄ±nda gÃ¼venli kapanÄ±ÅŸ yardÄ±mcÄ±larÄ±
    /// </summary>
    public sealed partial class ActuatorManager
    {
        /// <summary>
        /// Thruster telemetry health gÃ¼ncellemesi.
        /// true dÃ¶nerse solver/authority yeniden hesaplanmalÄ±dÄ±r.
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

                    LastAllocationReport = new ActuatorAllocationReport(
                        Success: true,
                        Reason: reason,
                        RequestedForceBody: Vec3.Zero,
                        RequestedTorqueBody: Vec3.Zero,
                        AchievedForceBody: Vec3.Zero,
                        AchievedTorqueBody: Vec3.Zero,
                        ForceErrorBody: Vec3.Zero,
                        TorqueErrorBody: Vec3.Zero,
                        NormalizedError: 0.0,
                        SaturationRatio: 0.0,
                        ActiveThrusterCount: 0,
                        HealthyThrusterCount: _thrusters.Count(t => t.IsHealthy),
                        HadSaturation: false,
                        HadUnhealthyThruster: _thrusters.Any(t => !t.IsHealthy),
                        AuthorityLimited: false,
                        ReverseClampCount: 0
                    );
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

        private void PublishActuatorSummary(Vec3 totalFBody, Vec3 totalTBody)
        {
            string summary;

            lock (_stateLock)
            {
                var ordered = _thrusters
                    .OrderBy(mtr => mtr.Channel)
                    .Select(mtr =>
                    {
                        string health = mtr.IsHealthy
                            ? "OK"
                            : $"FAULT:{mtr.HealthFlags}";

                        string reverseMode = mtr.CanReverse
                            ? "BiDir"
                            : "OneWay";

                        return
                            $"{mtr.Id}={mtr.Current:F2}" +
                            $"[{health}|{reverseMode}|I={mtr.CurrentSenseMilliAmp}mA|RPM={mtr.RpmFeedback}]";
                    });

                summary = $"[Actuator] {string.Join(" ", ordered)} | F_body={Fmt(totalFBody)} T_body={Fmt(totalTBody)}";
            }

            EnqueueLog(summary);
        }

        private void PublishAllocationSummary(ActuatorAllocationReport report)
        {
            string summary =
                $"[Allocation] {report.Reason} " +
                $"reqF={Fmt(report.RequestedForceBody)} reqT={Fmt(report.RequestedTorqueBody)} " +
                $"gotF={Fmt(report.AchievedForceBody)} gotT={Fmt(report.AchievedTorqueBody)} " +
                $"err={report.NormalizedError:F3} " +
                $"sat={report.SaturationRatio:F2} " +
                $"active={report.ActiveThrusterCount} " +
                $"healthy={report.HealthyThrusterCount} " +
                $"limited={report.AuthorityLimited} " +
                $"revClamp={report.ReverseClampCount}";

            EnqueueLog(summary);
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

        private static double StopwatchTicksToMs(long ticks)
            => ticks * 1000.0 / Stopwatch.Frequency;

        private static string Fmt(Vec3 v)
            => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActuatorManager));
        }

        /// <summary>
        /// Worker thread ve seri port kaynaklarÄ±nÄ± gÃ¼venli ÅŸekilde kapatÄ±r.
        /// </summary>
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
    }
}
