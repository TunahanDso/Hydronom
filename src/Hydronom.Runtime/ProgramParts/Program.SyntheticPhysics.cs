using System;
using Hydronom.Core.Domain;

partial class Program
{
    /// <summary>
    /// External pose uygulanmadÃƒâ€Ã‚Â±Ãƒâ€Ã…Â¸Ãƒâ€Ã‚Â±nda runtime iÃƒÆ’Ã‚Â§ fizik simÃƒÆ’Ã‚Â¼lasyonunu yÃƒÆ’Ã‚Â¼rÃƒÆ’Ã‚Â¼tÃƒÆ’Ã‚Â¼r.
    ///
    /// AmaÃƒÆ’Ã‚Â§:
    /// - SensÃƒÆ’Ã‚Â¶r/pose gelmediÃƒâ€Ã…Â¸i durumda karar/rota testlerini sÃƒÆ’Ã‚Â¼rdÃƒÆ’Ã‚Â¼rebilmek.
    /// - ActuatorManager'Ãƒâ€Ã‚Â±n ÃƒÆ’Ã‚Â¼rettiÃƒâ€Ã…Â¸i body-frame force/torque deÃƒâ€Ã…Â¸erleriyle VehicleState'i ilerletmek.
    /// - V1 environment-aware post process ile su yÃƒÆ’Ã‚Â¼zeyi/taban sÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±rlarÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â± korumak.
    ///
    /// Not:
    /// - forceBody gÃƒÆ’Ã‚Â¶vde frame'indedir.
    /// - VehicleState lineer tarafta dÃƒÆ’Ã‚Â¼nya frame kuvvet beklediÃƒâ€Ã…Â¸i iÃƒÆ’Ã‚Â§in forceBody dÃƒÆ’Ã‚Â¼nya frame'e ÃƒÆ’Ã‚Â§evrilir.
    /// - torqueBody gÃƒÆ’Ã‚Â¶vde frame olarak korunur.
    /// </summary>
    private static VehicleState IntegrateSyntheticStateIfNeeded(
        VehicleState state,
        Vec3 forceBody,
        Vec3 torqueBody,
        double dtMeasured,
        PhysicsOptions physics,
        WorldOptions world,
        RuntimeOptions runtime,
        bool externalApplied,
        ref LoopRuntimeState loopState)
    {
        bool shouldIntegrateSyntheticState =
            runtime.UseSyntheticStateWhenNoExternal &&
            !externalApplied;

        if (!shouldIntegrateSyntheticState)
            return state;

        if (!loopState.LoggedSyntheticStateNotice)
        {
            Console.WriteLine("[STATE] Synthetic state integration aktif (karar/rota testi iÃƒÆ’Ã‚Â§in iÃƒÆ’Ã‚Â§ fizik yÃƒÆ’Ã‚Â¼rÃƒÆ’Ã‚Â¼tÃƒÆ’Ã‚Â¼lÃƒÆ’Ã‚Â¼yor).");
            Console.WriteLine("[ENV-PHYS] VP9A WorldModel sampling aktif (environment resolver + config-backed surface/floor clamp).");
            loopState.LoggedSyntheticStateNotice = true;
        }

        var withForces = state.ClearForces();
        withForces = withForces with
        {
            LinearForce = state.Orientation.BodyToWorld(forceBody),
            AngularTorque = torqueBody
        };

        var integrated = withForces.IntegrateMarine(
            dt: dtMeasured,
            mass: physics.MassKg,
            inertia: physics.Inertia,
            linearDragBody: physics.LinearDragBody,
            quadraticDragBody: physics.QuadraticDragBody,
            angularLinearDragBody: physics.AngularLinearDragBody,
            angularQuadraticDragBody: physics.AngularQuadraticDragBody,
            maxLinearSpeed: physics.MaxSyntheticLinearSpeed,
            maxAngularSpeedDeg: physics.MaxSyntheticAngularSpeedDeg
        );

        return ApplyEnvironmentAwareSyntheticPhysicsPostStep(
            integrated,
            physics,
            world,
            runtime.LogVerbose,
            loopState.TickIndex);
    }
}