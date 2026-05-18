namespace Hydronom.Core.Communication.Envelope;

[Flags]
public enum HydronomMessageFlags : ushort
{
    None = 0,

    RequiresAck = 1 << 0,
    IsAck = 1 << 1,
    IsNack = 1 << 2,

    IsCompressed = 1 << 3,
    IsDelta = 1 << 4,
    IsSnapshot = 1 << 5,

    IsEncrypted = 1 << 6,
    IsSigned = 1 << 7,

    IsBroadcast = 1 << 8,
    IsReplayFrame = 1 << 9,

    IsSafetyCritical = 1 << 10,
    IsOperatorCommand = 1 << 11,
    IsAutonomousCommand = 1 << 12
}