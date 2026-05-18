namespace Hydronom.Core.Communication.RuntimeBridge;

public enum HydronomRuntimeCommandIntentKind : ushort
{
    Unknown = 0,

    Arm = 1,
    Disarm = 2,
    EmergencyStop = 3,

    ManualControl = 10,

    StartMission = 20,
    StopMission = 21,
    PauseMission = 22,
    ResumeMission = 23,
    AbortMission = 24,

    StartScenario = 30,
    StopScenario = 31,
    PauseScenario = 32,
    ResumeScenario = 33,

    SetMode = 40,
    SetTarget = 41,

    RequestStatus = 60,
    RequestSnapshot = 61,

    AuthorityClaim = 80,
    AuthorityRelease = 81,

    Custom = 1000
}