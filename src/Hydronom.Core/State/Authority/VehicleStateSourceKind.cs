namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// VehicleState Ã¼retmeye Ã§alÄ±ÅŸan kaynaÄŸÄ±n tÃ¼rÃ¼.
    ///
    /// Bu deÄŸer StateAuthorityManager tarafÄ±ndan state kabul/red kararÄ±nda kullanÄ±lÄ±r.
    /// </summary>
    public enum VehicleStateSourceKind
    {
        Unknown = 0,

        CSharpFusion = 10,
        CSharpEstimator = 11,

        PhysicsTruth = 20,
        ReplayEstimate = 30,

        ExternalPose = 40,
        ManualOverride = 50,

        PythonBackup = 60,
        PythonCompareOnly = 61
    }
}
