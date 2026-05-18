using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;

namespace Hydronom.Core.Communication.Pipeline;

public sealed record HydronomCommunicationPipelineOptions
{
    public string SourceId { get; init; } = "hydronom-runtime";

    public string TargetId { get; init; } = "hydronom-ground";

    public string SessionId { get; init; } = "";

    public string HmacSecretKey { get; init; } = "hydronom-development-secret-key";

    public HydronomSecurityProfile SecurityProfile { get; init; } = HydronomSecurityProfile.Race;

    public CompactTelemetryDeltaOptions DeltaOptions { get; init; } =
        CompactTelemetryDeltaOptions.Default;

    public bool EnableDeltaTelemetry { get; init; } = true;

    public bool EnableSecurity { get; init; } = true;

    public bool RequireTelemetryChange { get; init; } = false;

    public static HydronomCommunicationPipelineOptions Development { get; } = new()
    {
        SourceId = "hydronom-runtime-dev",
        TargetId = "hydronom-ground-dev",
        SessionId = "dev-session",
        HmacSecretKey = "hydronom-development-secret-key",
        SecurityProfile = HydronomSecurityProfile.Development,
        DeltaOptions = CompactTelemetryDeltaOptions.Default,
        EnableDeltaTelemetry = true,
        EnableSecurity = false,
        RequireTelemetryChange = false
    };

    public static HydronomCommunicationPipelineOptions Race { get; } = new()
    {
        SourceId = "hydronom-runtime",
        TargetId = "hydronom-ground",
        SessionId = "race-session",
        HmacSecretKey = "hydronom-race-secret-key-please-change",
        SecurityProfile = HydronomSecurityProfile.Race,
        DeltaOptions = CompactTelemetryDeltaOptions.Default,
        EnableDeltaTelemetry = true,
        EnableSecurity = true,
        RequireTelemetryChange = false
    };

    public static HydronomCommunicationPipelineOptions LowBandwidth { get; } = new()
    {
        SourceId = "hydronom-runtime",
        TargetId = "hydronom-ground",
        SessionId = "low-bandwidth-session",
        HmacSecretKey = "hydronom-low-bandwidth-secret-key",
        SecurityProfile = HydronomSecurityProfile.Race,
        DeltaOptions = CompactTelemetryDeltaOptions.LowBandwidth,
        EnableDeltaTelemetry = true,
        EnableSecurity = true,
        RequireTelemetryChange = true
    };
}