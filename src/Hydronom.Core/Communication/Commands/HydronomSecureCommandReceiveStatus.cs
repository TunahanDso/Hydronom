namespace Hydronom.Core.Communication.Commands;

public enum HydronomSecureCommandReceiveStatus : byte
{
    Unknown = 0,

    Accepted = 1,

    DecodeRejected = 2,

    SecurityRejected = 3,

    CommandInvalid = 4,

    AuthorityRejected = 5
}