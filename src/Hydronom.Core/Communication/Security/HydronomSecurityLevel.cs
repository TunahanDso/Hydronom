namespace Hydronom.Core.Communication.Security;

public enum HydronomSecurityLevel : byte
{
    None = 0,

    CrcOnly = 1,

    Authenticated = 2,

    Encrypted = 3,

    AuthenticatedEncrypted = 4
}