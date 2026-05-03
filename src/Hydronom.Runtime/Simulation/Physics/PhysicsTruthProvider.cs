using System;
using System.Threading;
using System.Threading.Tasks;
using Hydronom.Core.Simulation.Truth;

namespace Hydronom.Runtime.Simulation.Physics
{
    /// <summary>
    /// Runtime tarafÄ±nda gÃ¼ncel PhysicsTruthState'i saÄŸlayan provider.
    ///
    /// Sim sensÃ¶r backend'leri bu provider Ã¼zerinden truth state okur.
    /// BÃ¶ylece sim GPS/IMU/LiDAR/kamera kendi kafasÄ±na gÃ¶re sahte hareket Ã¼retmez;
    /// runtime physics state'inden Ã¶lÃ§Ã¼m Ã¼retir.
    /// </summary>
    public sealed class PhysicsTruthProvider : IPhysicsTruthProvider
    {
        private readonly object _lock = new();

        private PhysicsTruthState _latestTruth;
        private bool _hasTruth;

        public PhysicsTruthProvider(string providerName = "PhysicsTruthProvider")
        {
            ProviderName = string.IsNullOrWhiteSpace(providerName)
                ? "PhysicsTruthProvider"
                : providerName.Trim();

            _latestTruth = default;
            _hasTruth = false;
        }

        public string ProviderName { get; }

        public bool IsAvailable
        {
            get
            {
                lock (_lock)
                {
                    return _hasTruth;
                }
            }
        }

        public DateTime LastTruthUtc
        {
            get
            {
                lock (_lock)
                {
                    return _hasTruth ? _latestTruth.TimestampUtc : default;
                }
            }
        }

        public PhysicsTruthState GetLatestTruth()
        {
            lock (_lock)
            {
                return _hasTruth
                    ? _latestTruth.Sanitized()
                    : default;
            }
        }

        public ValueTask<PhysicsTruthState> GetLatestTruthAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GetLatestTruth());
        }

        /// <summary>
        /// Runtime physics loop tarafÄ±ndan yeni truth frame yayÄ±nlamak iÃ§in Ã§aÄŸrÄ±lÄ±r.
        /// </summary>
        public void Publish(PhysicsTruthState truth)
        {
            lock (_lock)
            {
                _latestTruth = truth.Sanitized();
                _hasTruth = true;
            }
        }

        /// <summary>
        /// Provider iÃ§indeki truth bilgisini sÄ±fÄ±rlar.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _latestTruth = default;
                _hasTruth = false;
            }
        }

        public PhysicsTruthSnapshot GetSnapshot(DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;

            lock (_lock)
            {
                if (!_hasTruth)
                {
                    return new PhysicsTruthSnapshot(
                        TimestampUtc: now,
                        ProviderName: ProviderName,
                        IsAvailable: false,
                        LastTruthUtc: default,
                        LastTruthAgeMs: double.PositiveInfinity,
                        LatestTruth: default,
                        Summary: $"{ProviderName} has no published truth frame."
                    );
                }

                var ageMs = Math.Max(0.0, (now - _latestTruth.TimestampUtc).TotalMilliseconds);

                return new PhysicsTruthSnapshot(
                    TimestampUtc: now,
                    ProviderName: ProviderName,
                    IsAvailable: true,
                    LastTruthUtc: _latestTruth.TimestampUtc,
                    LastTruthAgeMs: ageMs,
                    LatestTruth: _latestTruth.Sanitized(),
                    Summary: $"{ProviderName} available. Last truth age={ageMs:F1} ms."
                );
            }
        }
    }
}
