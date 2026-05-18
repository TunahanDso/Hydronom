namespace Hydronom.Core.Communication.RuntimeBridge;

public enum HydronomSecureRuntimeCommandStatus : byte
{
    Unknown = 0,

    Accepted = 1,

    DecodeRejected = 2,

    SecurityRejected = 3,

    CommandInvalid = 4,

    AuthorityRejected = 5,

    RuntimeBridgeRejected = 6
}