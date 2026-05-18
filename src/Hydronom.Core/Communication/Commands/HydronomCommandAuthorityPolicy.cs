namespace Hydronom.Core.Communication.Commands;

public sealed record HydronomCommandAuthorityPolicy
{
    public bool DeveloperMode { get; init; }

    public bool AllowDeveloperOverride { get; init; }

    public bool AllowUnknownAuthority { get; init; }

    public bool RequireOperatorIdForOperatorCommands { get; init; } = true;

    public bool RequireReasonForSafetyCriticalCommands { get; init; } = true;

    public bool AllowAutonomousRuntimeEmergencyStop { get; init; } = false;

    public bool AllowAutonomousRuntimeArmDisarm { get; init; } = false;

    public bool AllowObserverStatusOnly { get; init; } = true;

    public IReadOnlyDictionary<HydronomCommandAuthority, IReadOnlySet<HydronomCommandKind>> AllowedCommandsByAuthority { get; init; } =
        CreateDefaultAllowedCommands();

    public IReadOnlyDictionary<string, HydronomCommandAuthority> KnownSourceAuthorities { get; init; } =
        new Dictionary<string, HydronomCommandAuthority>(StringComparer.Ordinal);

    public IReadOnlySet<string> TrustedSourceIds { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    public static HydronomCommandAuthorityPolicy Default { get; } = new();

    public static HydronomCommandAuthorityPolicy Development { get; } = new()
    {
        DeveloperMode = true,
        AllowDeveloperOverride = true,
        AllowUnknownAuthority = false,
        RequireOperatorIdForOperatorCommands = false,
        RequireReasonForSafetyCriticalCommands = false,
        AllowAutonomousRuntimeEmergencyStop = true,
        AllowAutonomousRuntimeArmDisarm = true
    };

    public static HydronomCommandAuthorityPolicy Race { get; } = new()
    {
        DeveloperMode = false,
        AllowDeveloperOverride = false,
        AllowUnknownAuthority = false,
        RequireOperatorIdForOperatorCommands = true,
        RequireReasonForSafetyCriticalCommands = true,
        AllowAutonomousRuntimeEmergencyStop = false,
        AllowAutonomousRuntimeArmDisarm = false
    };

    public HydronomCommandAuthorityPolicy WithKnownSource(
        string sourceId,
        HydronomCommandAuthority authority,
        bool trusted = true)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return this;
        }

        var known = new Dictionary<string, HydronomCommandAuthority>(
            KnownSourceAuthorities,
            StringComparer.Ordinal)
        {
            [sourceId] = authority
        };

        var trustedSources = new HashSet<string>(
            TrustedSourceIds,
            StringComparer.Ordinal);

        if (trusted)
        {
            trustedSources.Add(sourceId);
        }

        return this with
        {
            KnownSourceAuthorities = known,
            TrustedSourceIds = trustedSources
        };
    }

    private static IReadOnlyDictionary<HydronomCommandAuthority, IReadOnlySet<HydronomCommandKind>> CreateDefaultAllowedCommands()
    {
        return new Dictionary<HydronomCommandAuthority, IReadOnlySet<HydronomCommandKind>>
        {
            [HydronomCommandAuthority.Observer] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.Operator] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.Arm,
                HydronomCommandKind.Disarm,
                HydronomCommandKind.ManualControl,
                HydronomCommandKind.MissionCommand,
                HydronomCommandKind.ScenarioCommand,
                HydronomCommandKind.SetMode,
                HydronomCommandKind.SetTarget,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.ResumeMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.Supervisor] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.Arm,
                HydronomCommandKind.Disarm,
                HydronomCommandKind.ManualControl,
                HydronomCommandKind.MissionCommand,
                HydronomCommandKind.ScenarioCommand,
                HydronomCommandKind.AuthorityClaim,
                HydronomCommandKind.AuthorityRelease,
                HydronomCommandKind.SetMode,
                HydronomCommandKind.SetTarget,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.ResumeMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.SafetyOfficer] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.Disarm,
                HydronomCommandKind.EmergencyStop,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.AutonomousRuntime] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.MissionCommand,
                HydronomCommandKind.ScenarioCommand,
                HydronomCommandKind.SetTarget,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.ResumeMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.GroundStation] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.Arm,
                HydronomCommandKind.Disarm,
                HydronomCommandKind.ManualControl,
                HydronomCommandKind.MissionCommand,
                HydronomCommandKind.ScenarioCommand,
                HydronomCommandKind.AuthorityClaim,
                HydronomCommandKind.AuthorityRelease,
                HydronomCommandKind.SetMode,
                HydronomCommandKind.SetTarget,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.ResumeMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot
            },

            [HydronomCommandAuthority.EmergencyConsole] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.EmergencyStop,
                HydronomCommandKind.RequestStatus
            },

            [HydronomCommandAuthority.Developer] = new HashSet<HydronomCommandKind>
            {
                HydronomCommandKind.Arm,
                HydronomCommandKind.Disarm,
                HydronomCommandKind.EmergencyStop,
                HydronomCommandKind.ManualControl,
                HydronomCommandKind.MissionCommand,
                HydronomCommandKind.ScenarioCommand,
                HydronomCommandKind.AuthorityClaim,
                HydronomCommandKind.AuthorityRelease,
                HydronomCommandKind.SetMode,
                HydronomCommandKind.SetTarget,
                HydronomCommandKind.PauseMission,
                HydronomCommandKind.ResumeMission,
                HydronomCommandKind.AbortMission,
                HydronomCommandKind.RequestStatus,
                HydronomCommandKind.RequestSnapshot,
                HydronomCommandKind.Custom
            }
        };
    }
}