namespace Hydronom.Core.Communication.RuntimeBridge;

public enum HydronomRuntimeCommandAckStatus : byte
{
    Unknown = 0,

    Received = 1,

    Accepted = 2,

    QueuedForSafetyGate = 3,

    QueuedForExecution = 4,

    Applied = 5,

    Rejected = 10,

    RejectedByDecode = 11,

    RejectedBySecurity = 12,

    RejectedByAuthority = 13,

    RejectedByRuntimeBridge = 14,

    RejectedBySafetyGate = 15,

    Failed = 20,

    Timeout = 21
}