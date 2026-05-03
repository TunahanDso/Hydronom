using System;
using Hydronom.Runtime.Tuning;

namespace Hydronom.Runtime.Tuning
{
    /// <summary>
    /// Getter/setter delegeâ€™leriyle ITuningSink uygular; runtime ayarlarÄ±nÄ± uzaktan deÄŸiÅŸtirir.
    /// </summary>
    public sealed class InlineTuner : ITuningSink
    {
        private readonly Func<double> _getThrRate;
        private readonly Action<double>? _setThrRate;

        private readonly Func<double> _getRudRate;
        private readonly Action<double>? _setRudRate;

        private readonly Func<double> _getAhead;
        private readonly Action<double>? _setAhead;

        private readonly Func<double> _getFov;
        private readonly Action<double>? _setFov;

        private readonly Func<int> _getTickMs;
        private readonly Action<int>? _setTickMs;

        private readonly Func<bool> _getTaskActive;

        public InlineTuner(
            Func<double> getThrRate, Action<double>? setThrRate,
            Func<double> getRudRate, Action<double>? setRudRate,
            Func<double> getAhead, Action<double>? setAhead,
            Func<double> getFov, Action<double>? setFov,
            Func<int> getTickMs, Action<int>? setTickMs,
            Func<bool> getTaskActive
        )
        {
            _getThrRate = getThrRate ?? throw new ArgumentNullException(nameof(getThrRate));
            _setThrRate = setThrRate;
            _getRudRate = getRudRate ?? throw new ArgumentNullException(nameof(getRudRate));
            _setRudRate = setRudRate;
            _getAhead = getAhead ?? throw new ArgumentNullException(nameof(getAhead));
            _setAhead = setAhead;
            _getFov = getFov ?? throw new ArgumentNullException(nameof(getFov));
            _setFov = setFov;
            _getTickMs = getTickMs ?? throw new ArgumentNullException(nameof(getTickMs));
            _setTickMs = setTickMs;
            _getTaskActive = getTaskActive ?? throw new ArgumentNullException(nameof(getTaskActive));
        }

        public void SetLimiter(double? throttleRatePerSec, double? rudderRatePerSec)
        {
            if (throttleRatePerSec.HasValue && _setThrRate is not null)
                _setThrRate(throttleRatePerSec.Value);
            if (rudderRatePerSec.HasValue && _setRudRate is not null)
                _setRudRate(rudderRatePerSec.Value);
        }

        public void SetAnalysis(double? aheadDistanceM, double? halfFovDeg)
        {
            if (aheadDistanceM.HasValue && _setAhead is not null)
                _setAhead(aheadDistanceM.Value);
            if (halfFovDeg.HasValue && _setFov is not null)
                _setFov(halfFovDeg.Value);
        }

        public void SetTick(int? tickMs)
        {
            if (tickMs.HasValue && _setTickMs is not null)
                _setTickMs(tickMs.Value);
        }

        public RuntimeStatus GetStatus()
        {
            return new RuntimeStatus(
                LimiterThrottleRatePerSec: _getThrRate(),
                LimiterRudderRatePerSec: _getRudRate(),
                AnalysisAheadDistanceM: _getAhead(),
                AnalysisHalfFovDeg: _getFov(),
                TickMs: _getTickMs(),
                TaskActive: _getTaskActive()
            );
        }
    }
}

