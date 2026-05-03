using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Simulation.Truth;

namespace Hydronom.Runtime.Simulation.Physics
{
    /// <summary>
    /// Runtime physics state'ini PhysicsTruthProvider'a yayÄ±nlayan servis.
    ///
    /// Bu sÄ±nÄ±f runtime loop iÃ§inde ÅŸu amaÃ§la kullanÄ±lacaktÄ±r:
    ///
    /// Domain.VehicleState
    ///     â†“
    /// PhysicsWorldAdapter
    ///     â†“
    /// PhysicsTruthState
    ///     â†“
    /// PhysicsTruthProvider
    ///     â†“
    /// Sim Sensor Backends
    ///
    /// BÃ¶ylece C# simÃ¼lasyonunda sensÃ¶rler gerÃ§ek physics truth'tan beslenir.
    /// </summary>
    public sealed class PhysicsTruthPublisher
    {
        private readonly PhysicsWorldAdapter _adapter;
        private readonly PhysicsTruthProvider _provider;

        private PhysicsTruthState _lastPublished;
        private bool _hasPublished;

        public PhysicsTruthPublisher(
            PhysicsWorldAdapter adapter,
            PhysicsTruthProvider provider
        )
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool HasPublished => _hasPublished;

        public PhysicsTruthState LastPublished => _hasPublished
            ? _lastPublished.Sanitized()
            : default;

        /// <summary>
        /// Mevcut Domain.VehicleState'i truth state'e Ã§evirip provider'a yayÄ±nlar.
        /// </summary>
        public PhysicsTruthState Publish(
            string vehicleId,
            VehicleState state,
            PhysicsLoads lastAppliedLoads,
            Vec3? accelerationWorld = null,
            Vec3? angularAccelerationDegSec = null
        )
        {
            var truth = _adapter.BuildTruthState(
                vehicleId: vehicleId,
                state: state,
                lastAppliedLoads: lastAppliedLoads,
                accelerationWorld: accelerationWorld,
                angularAccelerationDegSec: angularAccelerationDegSec
            );

            _provider.Publish(truth);

            _lastPublished = truth.Sanitized();
            _hasPublished = true;

            return _lastPublished;
        }

        /// <summary>
        /// Son PhysicsStepReport bilgisinden truth frame Ã¼retir.
        /// </summary>
        public PhysicsTruthState PublishFromReport(
            string vehicleId,
            PhysicsStepReport report
        )
        {
            var safeReport = report;

            Vec3 accelerationWorld = Vec3.Zero;
            Vec3 angularAccelerationDegSec = Vec3.Zero;

            if (safeReport.WasIntegrated && safeReport.DtUsed > 1e-9)
            {
                accelerationWorld = safeReport.LinearAccelerationWorld;

                const double RadToDeg = 180.0 / Math.PI;
                angularAccelerationDegSec = safeReport.AngularAccelerationBodyRad * RadToDeg;
            }

            return Publish(
                vehicleId: vehicleId,
                state: safeReport.StateAfter,
                lastAppliedLoads: new PhysicsLoads(
                    ForceWorld: safeReport.ForceWorld,
                    TorqueBody: safeReport.TorqueBody
                ),
                accelerationWorld: accelerationWorld,
                angularAccelerationDegSec: angularAccelerationDegSec
            );
        }

        public PhysicsTruthSnapshot GetSnapshot(DateTime? utcNow = null)
        {
            return _provider.GetSnapshot(utcNow);
        }
    }
}
