using Hydronom.Core.Communication.Compression;
using Hydronom.Core.Communication.Security;

namespace Hydronom.Core.Communication.Profiles;

public sealed record HydronomCommunicationProfile
{
    public string Name { get; init; } = "default";

    public string Mode { get; init; } = "Hybrid";

    public HydronomSecurityProfile Security { get; init; } = HydronomSecurityProfile.Race;

    public HydronomCompressionMode TelemetryCompression { get; init; } =
        HydronomCompressionMode.FieldMaskDeltaQuantized;

    public bool UseBinaryForCriticalMessages { get; init; } = true;

    public bool UseJsonForDiagnostics { get; init; } = true;

    public bool EnableAdaptiveBandwidth { get; init; } = true;

    public int MaxCriticalQueueDepth { get; init; } = 512;

    public int MaxHighQueueDepth { get; init; } = 1024;

    public int MaxNormalQueueDepth { get; init; } = 2048;

    public int MaxLowQueueDepth { get; init; } = 2048;

    public int MaxBulkQueueDepth { get; init; } = 1024;

    public double ExcellentStateHz { get; init; } = 50.0;

    public double GoodStateHz { get; init; } = 20.0;

    public double WeakStateHz { get; init; } = 10.0;

    public double CriticalStateHz { get; init; } = 2.0;

    public static HydronomCommunicationProfile Development { get; } = new()
    {
        Name = "development",
        Mode = "JsonDebug",
        Security = HydronomSecurityProfile.Development,
        TelemetryCompression = HydronomCompressionMode.None,
        UseBinaryForCriticalMessages = false,
        UseJsonForDiagnostics = true,
        EnableAdaptiveBandwidth = false
    };

    public static HydronomCommunicationProfile Race { get; } = new()
    {
        Name = "race",
        Mode = "HybridRace",
        Security = HydronomSecurityProfile.Race,
        TelemetryCompression = HydronomCompressionMode.FieldMaskDeltaQuantized,
        UseBinaryForCriticalMessages = true,
        UseJsonForDiagnostics = true,
        EnableAdaptiveBandwidth = true
    };

    public static HydronomCommunicationProfile Secure { get; } = new()
    {
        Name = "secure",
        Mode = "SecureHybrid",
        Security = HydronomSecurityProfile.Secure,
        TelemetryCompression = HydronomCompressionMode.FieldMaskDeltaQuantized,
        UseBinaryForCriticalMessages = true,
        UseJsonForDiagnostics = true,
        EnableAdaptiveBandwidth = true
    };
}