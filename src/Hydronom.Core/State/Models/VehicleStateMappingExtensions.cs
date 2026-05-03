using System;
using Hydronom.Core.Domain;
using Hydronom.Core.State.Authority;

namespace Hydronom.Core.State.Models
{
    /// <summary>
    /// Eski Domain.VehicleState fizik modeli ile yeni VehicleOperationalState modeli
    /// arasÄ±nda geÃ§iÅŸ saÄŸlayan yardÄ±mcÄ± dÃ¶nÃ¼ÅŸÃ¼mler.
    /// </summary>
    public static class VehicleStateMappingExtensions
    {
        public static VehicleOperationalState ToOperationalState(
            this VehicleState state,
            string vehicleId,
            VehicleStateSourceKind sourceKind,
            StateAuthorityMode authorityMode,
            double confidence,
            string frameId = "map",
            string qualitySummary = "DOMAIN_STATE_MAPPING"
        )
        {
            var safe = state.Sanitized();

            return new VehicleOperationalState(
                VehicleId: string.IsNullOrWhiteSpace(vehicleId) ? "UNKNOWN" : vehicleId.Trim(),
                TimestampUtc: DateTime.UtcNow,
                Pose: new VehiclePose(
                    X: safe.Position.X,
                    Y: safe.Position.Y,
                    Z: safe.Position.Z,
                    YawDeg: safe.Orientation.YawDeg,
                    FrameId: frameId
                ),
                Twist: new VehicleTwist(
                    Vx: safe.LinearVelocity.X,
                    Vy: safe.LinearVelocity.Y,
                    Vz: safe.LinearVelocity.Z,
                    YawRateDegSec: safe.AngularVelocity.Z
                ),
                Attitude: new VehicleAttitude(
                    RollDeg: safe.Orientation.RollDeg,
                    PitchDeg: safe.Orientation.PitchDeg,
                    YawDeg: safe.Orientation.YawDeg,
                    RollRateDegSec: safe.AngularVelocity.X,
                    PitchRateDegSec: safe.AngularVelocity.Y,
                    YawRateDegSec: safe.AngularVelocity.Z
                ),
                SourceKind: sourceKind,
                AuthorityMode: authorityMode,
                Confidence: confidence,
                FrameId: frameId,
                QualitySummary: qualitySummary,
                TraceId: Guid.NewGuid().ToString("N")
            ).Sanitized();
        }

        public static VehicleState ToDomainVehicleState(
            this VehicleOperationalState state,
            Vec3? linearForce = null,
            Vec3? angularTorque = null
        )
        {
            var safe = state.Sanitized();

            return new VehicleState(
                Position: new Vec3(safe.Pose.X, safe.Pose.Y, safe.Pose.Z),
                Orientation: new Orientation(
                    safe.Attitude.RollDeg,
                    safe.Attitude.PitchDeg,
                    safe.Attitude.YawDeg
                ),
                LinearVelocity: new Vec3(safe.Twist.Vx, safe.Twist.Vy, safe.Twist.Vz),
                AngularVelocity: new Vec3(
                    safe.Attitude.RollRateDegSec,
                    safe.Attitude.PitchRateDegSec,
                    safe.Attitude.YawRateDegSec
                ),
                LinearForce: linearForce ?? Vec3.Zero,
                AngularTorque: angularTorque ?? Vec3.Zero
            ).Sanitized();
        }
    }
}
