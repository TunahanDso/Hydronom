namespace Hydronom.Core.Communication.Commands;

public enum HydronomCommandAuthority : byte
{
    Unknown = 0,

    Observer = 1,

    Operator = 2,

    Supervisor = 3,

    SafetyOfficer = 4,

    AutonomousRuntime = 5,

    GroundStation = 6,

    EmergencyConsole = 7,

    Developer = 8
}