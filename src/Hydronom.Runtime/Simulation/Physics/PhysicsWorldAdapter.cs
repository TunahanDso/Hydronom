using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Simulation.Environment;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;
using Hydronom.Runtime.Simulation.World;

namespace Hydronom.Runtime.Simulation.Physics
{
    /// <summary>
    /// Runtime fizik state'i ile simÃ¼lasyon dÃ¼nyasÄ±/environment arasÄ±nda kÃ¶prÃ¼ kurar.
    ///
    /// Bu sÄ±nÄ±fÄ±n amacÄ±:
    /// - RuntimeSimWorld iÃ§indeki environment state'i okumak
    /// - Su/akÄ±ntÄ±/rÃ¼zgar/yerÃ§ekimi gibi ortam bilgilerini physics tarafÄ±na anlaÅŸÄ±lÄ±r hale getirmek
    /// - Domain.VehicleState'ten PhysicsTruthState Ã¼retmek
    /// - Sim sensÃ¶rlerin kullanacaÄŸÄ± truth state'i Ã§evresel baÄŸlamla zenginleÅŸtirmek
    ///
    /// Bu sÄ±nÄ±f physics engine deÄŸildir.
    /// Physics engine'in Ã¼rettiÄŸi state'i dÃ¼nya/environment bilgisiyle paketleyen adaptÃ¶rdÃ¼r.
    /// </summary>
    public sealed class PhysicsWorldAdapter
    {
        private readonly RuntimeSimWorldStore _worldStore;

        public PhysicsWorldAdapter(RuntimeSimWorldStore worldStore)
        {
            _worldStore = worldStore ?? throw new ArgumentNullException(nameof(worldStore));
        }

        /// <summary>
        /// Mevcut runtime dÃ¼nyasÄ±nÄ± okuyarak physics context Ã¼retir.
        /// </summary>
        public PhysicsWorldContext GetContext()
        {
            var runtimeWorld = _worldStore.GetRuntimeWorld().Sanitized();
            var environment = runtimeWorld.Environment.Sanitized();
            var world = runtimeWorld.World.Sanitized();

            return new PhysicsWorldContext(
                TimestampUtc: DateTime.UtcNow,
                WorldId: world.WorldId,
                Medium: environment.Medium,
                GravityWorld: environment.GravityWorld.ToDomainVec3(),
                FluidVelocityWorld: environment.GetFluidVelocityAt(SimVector3.Zero).ToDomainVec3(),
                WaterEnabled: environment.Water.Enabled,
                WaterLevelZ: environment.Water.WaterLevelZ,
                WaterDensityKgM3: environment.Water.DensityKgM3,
                WindVelocityWorld: environment.Wind.VelocityWorld.ToDomainVec3(),
                CurrentVelocityWorld: environment.Current.VelocityWorld.ToDomainVec3(),
                EnvironmentSummary: environment.Summary,
                ObjectCount: world.Objects.Count,
                DetectableObjectCount: world.GetDetectableObjects().Count
            ).Sanitized();
        }

        /// <summary>
        /// Belirli bir araÃ§ konumuna gÃ¶re environment etkilerini hesaplar.
        /// </summary>
        public PhysicsWorldContext GetContextAt(Vec3 positionWorld)
        {
            var runtimeWorld = _worldStore.GetRuntimeWorld().Sanitized();
            var environment = runtimeWorld.Environment.Sanitized();
            var world = runtimeWorld.World.Sanitized();

            var p = SimVector3.FromDomainVec3(positionWorld);
            var fluidVelocity = environment.GetFluidVelocityAt(p);

            return new PhysicsWorldContext(
                TimestampUtc: DateTime.UtcNow,
                WorldId: world.WorldId,
                Medium: environment.Medium,
                GravityWorld: environment.GravityWorld.ToDomainVec3(),
                FluidVelocityWorld: fluidVelocity.ToDomainVec3(),
                WaterEnabled: environment.Water.Enabled,
                WaterLevelZ: environment.Water.WaterLevelZ,
                WaterDensityKgM3: environment.Water.DensityKgM3,
                WindVelocityWorld: environment.Wind.VelocityWorld.ToDomainVec3(),
                CurrentVelocityWorld: environment.Current.GetVelocityAtDepth(p.Z).ToDomainVec3(),
                EnvironmentSummary: environment.Summary,
                ObjectCount: world.Objects.Count,
                DetectableObjectCount: world.GetDetectableObjects().Count
            ).Sanitized();
        }

        /// <summary>
        /// Domain.VehicleState ve son fizik yÃ¼klerinden PhysicsTruthState Ã¼retir.
        /// </summary>
        public PhysicsTruthState BuildTruthState(
            string vehicleId,
            VehicleState state,
            PhysicsLoads lastAppliedLoads,
            Vec3? accelerationWorld = null,
            Vec3? angularAccelerationDegSec = null
        )
        {
            var safeState = state.Sanitized();
            var context = GetContextAt(safeState.Position);

            return new PhysicsTruthState(
                VehicleId: Normalize(vehicleId, "UNKNOWN"),
                TimestampUtc: DateTime.UtcNow,
                Position: safeState.Position,
                Velocity: safeState.LinearVelocity,
                Acceleration: accelerationWorld ?? Vec3.Zero,
                Orientation: safeState.Orientation,
                AngularVelocityDegSec: safeState.AngularVelocity,
                AngularAccelerationDegSec: angularAccelerationDegSec ?? Vec3.Zero,
                LastAppliedLoads: lastAppliedLoads.Sanitized(),
                EnvironmentSummary: context.EnvironmentSummary,
                FrameId: "world",
                TraceId: Guid.NewGuid().ToString("N")
            ).Sanitized();
        }

        /// <summary>
        /// DÃ¼nya nesneleri iÃ§inde algÄ±lanabilir nesne sayÄ±sÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
        /// Sim sensor backend'leri bu bilgiyi diagnostics amacÄ±yla kullanabilir.
        /// </summary>
        public int GetDetectableObjectCount()
        {
            return _worldStore
                .GetRuntimeWorld()
                .World
                .Sanitized()
                .GetDetectableObjects()
                .Count;
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    /// <summary>
    /// PhysicsWorldAdapter tarafÄ±ndan Ã¼retilen environment/physics baÄŸlamÄ±.
    ///
    /// Bu yapÄ± physics engine, sim sensor backend ve diagnostics katmanlarÄ±na
    /// "ÅŸu anda fizik hangi dÃ¼nya koÅŸullarÄ±nda Ã§alÄ±ÅŸÄ±yor?" sorusunun cevabÄ±nÄ± verir.
    /// </summary>
    public readonly record struct PhysicsWorldContext(
        DateTime TimestampUtc,
        string WorldId,
        SimMediumKind Medium,
        Vec3 GravityWorld,
        Vec3 FluidVelocityWorld,
        bool WaterEnabled,
        double WaterLevelZ,
        double WaterDensityKgM3,
        Vec3 WindVelocityWorld,
        Vec3 CurrentVelocityWorld,
        string EnvironmentSummary,
        int ObjectCount,
        int DetectableObjectCount
    )
    {
        public PhysicsWorldContext Sanitized()
        {
            return new PhysicsWorldContext(
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                WorldId: Normalize(WorldId, "world"),
                Medium: Medium,
                GravityWorld: SanitizeVec(GravityWorld, new Vec3(0.0, 0.0, -9.80665)),
                FluidVelocityWorld: SanitizeVec(FluidVelocityWorld, Vec3.Zero),
                WaterEnabled: WaterEnabled,
                WaterLevelZ: Safe(WaterLevelZ),
                WaterDensityKgM3: SafePositive(WaterDensityKgM3, 997.0),
                WindVelocityWorld: SanitizeVec(WindVelocityWorld, Vec3.Zero),
                CurrentVelocityWorld: SanitizeVec(CurrentVelocityWorld, Vec3.Zero),
                EnvironmentSummary: Normalize(EnvironmentSummary, "Environment context."),
                ObjectCount: ObjectCount < 0 ? 0 : ObjectCount,
                DetectableObjectCount: DetectableObjectCount < 0 ? 0 : DetectableObjectCount
            );
        }

        private static Vec3 SanitizeVec(Vec3 v, Vec3 fallback)
        {
            if (!double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z))
                return fallback;

            return v;
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
