namespace Hydronom.Core.Communication.Commands;

public enum HydronomCommandKind : ushort
{
    Unknown = 0,

    Arm = 1,
    Disarm = 2,
    EmergencyStop = 3,

    ManualControl = 10,
    MissionCommand = 20,
    ScenarioCommand = 21,

    AuthorityClaim = 30,
    AuthorityRelease = 31,

    SetMode = 40,
    SetTarget = 41,
    PauseMission = 42,
    ResumeMission = 43,
    AbortMission = 44,

    RequestStatus = 60,
    RequestSnapshot = 61,

    Custom = 1000
}