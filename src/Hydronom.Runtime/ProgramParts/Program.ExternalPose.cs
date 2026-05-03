using System;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Runtime.Buses;
partial class Program
{
    /// <summary>
    /// External pose provider'dan gelen pozisyon/yaw bilgisini runtime state'e uygular.
    ///
    /// Not:
    /// - Sim/Hybrid modda Simulation:AllowExternalPoseOverride=false ise external pose uygulanmaz.
    /// - External pose taze deÄŸilse uygulanmaz.
    /// - Ã–nceki external pose varsa velocity ve yaw-rate tahmini yapÄ±lÄ±r.
    /// - Teleport tespit edilirse opsiyonel olarak velocity resetlenir.
    /// </summary>
    private static bool TryApplyExternalPose(
        IFrameSource frameSource,
        RuntimeOptions runtime,
        ExternalPoseOptions options,
        ref ExternalPoseState externalState,
        ref VehicleState state,
        long tickIndex)
    {
        if (runtime.SimMode && !runtime.AllowExternalPoseOverrideInSim)
        {
            if (options.PreferExternalConfig && tickIndex - externalState.LastBlockedLogTick >= 50)
            {
                Console.WriteLine("[SRC] ExternalPose override blocked (Sim/Hybrid). Set Simulation:AllowExternalPoseOverride=true to enable.");
                externalState.LastBlockedLogTick = tickIndex;
            }

            return false;
        }

        if (!options.PreferExternalEffective)
            return false;

        if (frameSource is not IExternalPoseProvider extProv)
            return false;

        if (!extProv.TryGetLatestExternal(out var extPose))
            return false;

        if (extPose.AgeMs > extProv.FreshMs)
            return false;

        double newX = extPose.X;
        double newY = extPose.Y;
        double newYawDeg = extPose.HeadingDeg;

        Vec3 linearVelocityToApply = state.LinearVelocity;
        Vec3 angularVelocityToApply = state.AngularVelocity;

        if (externalState.HasPrevious)
        {
            double extDt = (DateTime.UtcNow - externalState.PreviousUtc).TotalSeconds;

            if (extDt > 1e-4 && extDt < 1.0)
            {
                double dxExt = newX - externalState.PreviousX;
                double dyExt = newY - externalState.PreviousY;
                double distExt = Math.Sqrt(dxExt * dxExt + dyExt * dyExt);

                double dyawExt = NormalizeAngleDeg(newYawDeg - externalState.PreviousYawDeg);

                bool teleported =
                    distExt > options.TeleportDistanceM ||
                    Math.Abs(dyawExt) > options.TeleportYawDeg;

                if (teleported && options.ResetVelocityOnTeleport)
                {
                    linearVelocityToApply = new Vec3(
                        0.0,
                        0.0,
                        state.LinearVelocity.Z
                    );

                    angularVelocityToApply = new Vec3(
                        state.AngularVelocity.X,
                        state.AngularVelocity.Y,
                        0.0
                    );
                }
                else
                {
                    double vxEst = dxExt / extDt;
                    double vyEst = dyExt / extDt;
                    double yawRateEstDeg = dyawExt / extDt;

                    linearVelocityToApply = new Vec3(
                        Lerp(state.LinearVelocity.X, vxEst, options.VelocityBlend),
                        Lerp(state.LinearVelocity.Y, vyEst, options.VelocityBlend),
                        state.LinearVelocity.Z
                    );

                    angularVelocityToApply = new Vec3(
                        state.AngularVelocity.X,
                        state.AngularVelocity.Y,
                        Lerp(state.AngularVelocity.Z, yawRateEstDeg, options.YawRateBlend)
                    );
                }
            }
        }

        state = state.WithExternalPose(
            x: newX,
            y: newY,
            z: state.Position.Z,
            yawDeg: newYawDeg,
            rollDeg: state.Orientation.RollDeg,
            pitchDeg: state.Orientation.PitchDeg,
            linearVelocity: linearVelocityToApply,
            angularVelocity: angularVelocityToApply
        );

        externalState.PreviousX = newX;
        externalState.PreviousY = newY;
        externalState.PreviousYawDeg = newYawDeg;
        externalState.PreviousUtc = DateTime.UtcNow;
        externalState.HasPrevious = true;

        return true;
    }
}
