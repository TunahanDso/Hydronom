namespace Hydronom.Core.Communication.Security;

public sealed record HydronomSecurityProfile
{
    public string ProfileName { get; init; } = "default";

    public HydronomSecurityLevel Level { get; init; } = HydronomSecurityLevel.Authenticated;

    public bool RequireMonotonicSequence { get; init; } = true;

    public bool RequireFreshTimestamp { get; init; } = true;

    public TimeSpan MaxClockSkew { get; init; } = TimeSpan.FromSeconds(10);

    public bool RequireKnownSource { get; init; } = true;

    public bool AllowBroadcastCommands { get; init; } = false;

    public bool AllowUnsignedTelemetry { get; init; } = false;

    public bool AllowUnsignedEmergencyStop { get; init; } = false;

    public static HydronomSecurityProfile Development { get; } = new()
    {
        ProfileName = "development",
        Level = HydronomSecurityLevel.CrcOnly,
        RequireKnownSource = false,
        AllowUnsignedTelemetry = true,
        AllowUnsignedEmergencyStop = false
    };

    public static HydronomSecurityProfile Race { get; } = new()
    {
        ProfileName = "race",
        Level = HydronomSecurityLevel.Authenticated,
        RequireMonotonicSequence = true,
        RequireFreshTimestamp = true,
        RequireKnownSource = true,
        AllowBroadcastCommands = false,
        AllowUnsignedTelemetry = false,
        AllowUnsignedEmergencyStop = false
    };

    public static HydronomSecurityProfile Secure { get; } = new()
    {
        ProfileName = "secure",
        Level = HydronomSecurityLevel.AuthenticatedEncrypted,
        RequireMonotonicSequence = true,
        RequireFreshTimestamp = true,
        RequireKnownSource = true,
        AllowBroadcastCommands = false,
        AllowUnsignedTelemetry = false,
        AllowUnsignedEmergencyStop = false
    };
}