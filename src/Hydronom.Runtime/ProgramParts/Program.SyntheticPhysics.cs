using System;
using Hydronom.Core.Domain;

partial class Program
{
    /// <summary>
    /// External pose uygulanmadÄ±ÄŸÄ±nda runtime iÃ§ fizik simÃ¼lasyonunu yÃ¼rÃ¼tÃ¼r.
    ///
    /// AmaÃ§:
    /// - SensÃ¶r/pose gelmediÄŸi durumda karar/rota testlerini sÃ¼rdÃ¼rebilmek
    /// - ActuatorManager'Ä±n Ã¼rettiÄŸi body-frame force/torque deÄŸerleriyle VehicleState'i ilerletmek
    ///
    /// Not:
    /// - forceBody gÃ¶vde frame'indedir.
    /// - VehicleState lineer tarafta dÃ¼nya frame kuvvet beklediÄŸi iÃ§in forceBody dÃ¼nya frame'e Ã§evrilir.
    /// - torqueBody gÃ¶vde frame olarak korunur.
    /// </summary>
    private static VehicleState IntegrateSyntheticStateIfNeeded(
        VehicleState state,
        Vec3 forceBody,
        Vec3 torqueBody,
        double dtMeasured,
        PhysicsOptions physics,
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
            Console.WriteLine("[STATE] Synthetic state integration aktif (karar/rota testi iÃ§in iÃ§ fizik yÃ¼rÃ¼tÃ¼lÃ¼yor).");
            loopState.LoggedSyntheticStateNotice = true;
        }

        var withForces = state.ClearForces();
        withForces = withForces with
        {
            LinearForce = state.Orientation.BodyToWorld(forceBody),
            AngularTorque = torqueBody
        };

        return withForces.IntegrateMarine(
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
    }
}
