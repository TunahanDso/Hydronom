using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Simulation.Truth
{
    /// <summary>
    /// SimÃ¼lasyon modunda fizik motorunun Ã¼rettiÄŸi gerÃ§ek iÃ§ durum.
    ///
    /// Bu state Ops ana marker'Ä± deÄŸildir.
    /// Sim sensÃ¶rlerin Ã¶lÃ§Ã¼m Ã¼retmesi iÃ§in kullanÄ±lan truth kaynaÄŸÄ±dÄ±r.
    /// </summary>
    public readonly record struct PhysicsTruthState(
        string VehicleId,
        DateTime TimestampUtc,
        Vec3 Position,
        Vec3 Velocity,
        Vec3 Acceleration,
        Orientation Orientation,
        Vec3 AngularVelocityDegSec,
        Vec3 AngularAccelerationDegSec,
        PhysicsLoads LastAppliedLoads,
        string EnvironmentSummary,
        string FrameId,
        string TraceId
    )
    {
        public static PhysicsTruthState FromVehicleState(
            string vehicleId,
            VehicleState state,
            PhysicsLoads loads,
            string environmentSummary = "SIM",
            string frameId = "map"
        )
        {
            var safe = state.Sanitized();

            return new PhysicsTruthState(
                VehicleId: string.IsNullOrWhiteSpace(vehicleId) ? "UNKNOWN" : vehicleId.Trim(),
                TimestampUtc: DateTime.UtcNow,
                Position: safe.Position,
                Velocity: safe.LinearVelocity,
                Acceleration: Vec3.Zero,
                Orientation: safe.Orientation,
                AngularVelocityDegSec: safe.AngularVelocity,
                AngularAccelerationDegSec: Vec3.Zero,
                LastAppliedLoads: loads.Sanitized(),
                EnvironmentSummary: string.IsNullOrWhiteSpace(environmentSummary) ? "SIM" : environmentSummary.Trim(),
                FrameId: string.IsNullOrWhiteSpace(frameId) ? "map" : frameId.Trim(),
                TraceId: Guid.NewGuid().ToString("N")
            );
        }

        public bool IsFinite =>
            IsFiniteVec(Position) &&
            IsFiniteVec(Velocity) &&
            IsFiniteVec(Acceleration) &&
            Orientation.IsFinite &&
            IsFiniteVec(AngularVelocityDegSec) &&
            IsFiniteVec(AngularAccelerationDegSec);

        public PhysicsTruthState Sanitized()
        {
            return new PhysicsTruthState(
                VehicleId: string.IsNullOrWhiteSpace(VehicleId) ? "UNKNOWN" : VehicleId.Trim(),
                TimestampUtc: TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
                Position: SanitizeVec(Position),
                Velocity: SanitizeVec(Velocity),
                Acceleration: SanitizeVec(Acceleration),
                Orientation: Orientation.Sanitized(),
                AngularVelocityDegSec: SanitizeVec(AngularVelocityDegSec),
                AngularAccelerationDegSec: SanitizeVec(AngularAccelerationDegSec),
                LastAppliedLoads: LastAppliedLoads.Sanitized(),
                EnvironmentSummary: string.IsNullOrWhiteSpace(EnvironmentSummary) ? "SIM" : EnvironmentSummary.Trim(),
                FrameId: string.IsNullOrWhiteSpace(FrameId) ? "map" : FrameId.Trim(),
                TraceId: string.IsNullOrWhiteSpace(TraceId) ? Guid.NewGuid().ToString("N") : TraceId.Trim()
            );
        }

        private static bool IsFiniteVec(Vec3 v) =>
            double.IsFinite(v.X) &&
            double.IsFinite(v.Y) &&
            double.IsFinite(v.Z);

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                double.IsFinite(v.X) ? v.X : 0.0,
                double.IsFinite(v.Y) ? v.Y : 0.0,
                double.IsFinite(v.Z) ? v.Z : 0.0
            );
        }
    }
}
