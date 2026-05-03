namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Bir fizik entegrasyon adÄ±mÄ±nÄ±n aÃ§Ä±klanabilir raporu.
    ///
    /// Bu yapÄ± Analysis, Safety, Replay, Simulation ve Diagnostics katmanlarÄ±nÄ±n
    /// "araÃ§ neden bÃ¶yle hareket etti?" sorusunu cevaplamasÄ±nÄ± saÄŸlar.
    /// </summary>
    public readonly record struct PhysicsStepReport(
        bool WasIntegrated,
        string Reason,
        double DtRequested,
        double DtUsed,
        VehicleState StateBefore,
        VehicleState StateAfter,
        Vec3 ForceWorld,
        Vec3 TorqueBody,
        Vec3 EffectiveTorqueBody,
        Vec3 LinearAccelerationWorld,
        Vec3 AngularAccelerationBodyRad,
        double LinearSpeed,
        double AngularSpeedDeg,
        bool LinearSpeedLimited,
        bool AngularSpeedLimited,
        bool UsedGyroscopicTerm,
        PhysicsIntegrationMode IntegrationMode
    )
    {
        public static PhysicsStepReport NoStep(VehicleState state, double dtRequested, string reason)
        {
            return new PhysicsStepReport(
                WasIntegrated: false,
                Reason: reason,
                DtRequested: dtRequested,
                DtUsed: 0.0,
                StateBefore: state,
                StateAfter: state,
                ForceWorld: Vec3.Zero,
                TorqueBody: Vec3.Zero,
                EffectiveTorqueBody: Vec3.Zero,
                LinearAccelerationWorld: Vec3.Zero,
                AngularAccelerationBodyRad: Vec3.Zero,
                LinearSpeed: 0.0,
                AngularSpeedDeg: 0.0,
                LinearSpeedLimited: false,
                AngularSpeedLimited: false,
                UsedGyroscopicTerm: false,
                IntegrationMode: PhysicsIntegrationMode.SemiImplicitEuler
            );
        }
    }
}
