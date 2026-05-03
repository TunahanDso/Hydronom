namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// State update adayÄ±nÄ±n kabul/red kararÄ±.
    /// </summary>
    public enum StateUpdateDecision
    {
        Unknown = 0,

        Accepted = 10,

        RejectedInvalidData = 100,
        RejectedSourceNotAuthorized = 101,
        RejectedStaleTimestamp = 102,
        RejectedFrameMismatch = 103,
        RejectedLowConfidence = 104,
        RejectedTeleportDetected = 105,
        RejectedPhysicallyImpossible = 106,
        RejectedWrongMode = 107,
        RejectedVehicleMismatch = 108
    }
}
