using Hydronom.Core.Communication.Commands;

namespace Hydronom.Core.Communication.RuntimeBridge;

public sealed record HydronomRuntimeCommandIntent
{
    public string IntentId { get; init; } = Guid.NewGuid().ToString("N");

    public HydronomRuntimeCommandIntentKind Kind { get; init; } =
        HydronomRuntimeCommandIntentKind.Unknown;

    public HydronomCommandKind SourceCommandKind { get; init; } =
        HydronomCommandKind.Unknown;

    public HydronomCommandAuthority Authority { get; init; } =
        HydronomCommandAuthority.Unknown;

    public string CommandId { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string VehicleId { get; init; } = "";

    public string OperatorId { get; init; } = "";

    public ulong Sequence { get; init; }

    public long TimestampUnixMs { get; init; }

    public bool RequiresAck { get; init; }

    public bool SafetyCritical { get; init; }

    public string Reason { get; init; } = "";

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();

    public bool IsEmergency => Kind == HydronomRuntimeCommandIntentKind.EmergencyStop;

    public bool IsMissionRelated =>
        Kind is HydronomRuntimeCommandIntentKind.StartMission
            or HydronomRuntimeCommandIntentKind.StopMission
            or HydronomRuntimeCommandIntentKind.PauseMission
            or HydronomRuntimeCommandIntentKind.ResumeMission
            or HydronomRuntimeCommandIntentKind.AbortMission
            or HydronomRuntimeCommandIntentKind.StartScenario
            or HydronomRuntimeCommandIntentKind.StopScenario
            or HydronomRuntimeCommandIntentKind.PauseScenario
            or HydronomRuntimeCommandIntentKind.ResumeScenario
            or HydronomRuntimeCommandIntentKind.SetTarget;

    public bool IsManualControl => Kind == HydronomRuntimeCommandIntentKind.ManualControl;

    public string GetParameterOrDefault(string key, string defaultValue = "")
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return defaultValue;
        }

        return Parameters.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }

    public bool TryGetParameter(string key, out string value)
    {
        value = "";

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!Parameters.TryGetValue(key, out var found))
        {
            return false;
        }

        value = found;
        return true;
    }
}