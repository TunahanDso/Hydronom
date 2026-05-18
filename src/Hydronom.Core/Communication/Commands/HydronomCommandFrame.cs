using System.Text.Json;

namespace Hydronom.Core.Communication.Commands;

public sealed record HydronomCommandFrame
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    public HydronomCommandKind Kind { get; init; } = HydronomCommandKind.Unknown;

    public HydronomCommandAuthority Authority { get; init; } = HydronomCommandAuthority.Unknown;

    public string SourceId { get; init; } = "";

    public string TargetId { get; init; } = "";

    public string VehicleId { get; init; } = "";

    public string OperatorId { get; init; } = "";

    public ulong Sequence { get; init; }

    public long TimestampUnixMs { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public bool RequiresAck { get; init; } = true;

    public bool SafetyCritical { get; init; }

    public string Reason { get; init; } = "";

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();

    public JsonElement? RawPayload { get; init; }

    public bool IsEmergency => Kind == HydronomCommandKind.EmergencyStop;

    public bool IsAuthorityCommand =>
        Kind is HydronomCommandKind.AuthorityClaim or HydronomCommandKind.AuthorityRelease;

    public bool IsMissionCommand =>
        Kind is HydronomCommandKind.MissionCommand
            or HydronomCommandKind.ScenarioCommand
            or HydronomCommandKind.SetTarget
            or HydronomCommandKind.PauseMission
            or HydronomCommandKind.ResumeMission
            or HydronomCommandKind.AbortMission;

    public bool IsManualCommand => Kind == HydronomCommandKind.ManualControl;

    public static HydronomCommandFrame Create(
        HydronomCommandKind kind,
        HydronomCommandAuthority authority,
        string sourceId,
        string targetId,
        string vehicleId,
        ulong sequence,
        string operatorId = "",
        string reason = "",
        IReadOnlyDictionary<string, string>? parameters = null,
        bool? requiresAck = null,
        bool? safetyCritical = null)
    {
        return new HydronomCommandFrame
        {
            Kind = kind,
            Authority = authority,
            SourceId = sourceId,
            TargetId = targetId,
            VehicleId = vehicleId,
            Sequence = sequence,
            OperatorId = operatorId,
            Reason = reason,
            Parameters = parameters ?? new Dictionary<string, string>(),
            RequiresAck = requiresAck ?? ShouldRequireAck(kind),
            SafetyCritical = safetyCritical ?? IsSafetyCriticalKind(kind),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static bool ShouldRequireAck(HydronomCommandKind kind)
    {
        return kind is HydronomCommandKind.Arm
            or HydronomCommandKind.Disarm
            or HydronomCommandKind.EmergencyStop
            or HydronomCommandKind.ManualControl
            or HydronomCommandKind.MissionCommand
            or HydronomCommandKind.ScenarioCommand
            or HydronomCommandKind.AuthorityClaim
            or HydronomCommandKind.AuthorityRelease
            or HydronomCommandKind.SetMode
            or HydronomCommandKind.SetTarget
            or HydronomCommandKind.PauseMission
            or HydronomCommandKind.ResumeMission
            or HydronomCommandKind.AbortMission;
    }

    private static bool IsSafetyCriticalKind(HydronomCommandKind kind)
    {
        return kind is HydronomCommandKind.Arm
            or HydronomCommandKind.Disarm
            or HydronomCommandKind.EmergencyStop
            or HydronomCommandKind.ManualControl
            or HydronomCommandKind.MissionCommand
            or HydronomCommandKind.ScenarioCommand
            or HydronomCommandKind.SetMode
            or HydronomCommandKind.SetTarget
            or HydronomCommandKind.PauseMission
            or HydronomCommandKind.ResumeMission
            or HydronomCommandKind.AbortMission;
    }
}