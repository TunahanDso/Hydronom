using System;
using Hydronom.Core.Domain;

partial class Program
{
    /// <summary>
    /// External pose uygulanmadığında runtime iç fizik simülasyonunu yürütür.
    ///
    /// Amaç:
    /// - Sensör/pose gelmediği durumda karar/rota testlerini sürdürebilmek
    /// - ActuatorManager'ın ürettiği body-frame force/torque değerleriyle VehicleState'i ilerletmek
    ///
    /// Not:
    /// - forceBody gövde frame'indedir.
    /// - VehicleState lineer tarafta dünya frame kuvvet beklediği için forceBody dünya frame'e çevrilir.
    /// - torqueBody gövde frame olarak korunur.
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
            Console.WriteLine("[STATE] Synthetic state integration aktif (karar/rota testi için iç fizik yürütülüyor).");
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