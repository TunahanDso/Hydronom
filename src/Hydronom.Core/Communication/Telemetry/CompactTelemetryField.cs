namespace Hydronom.Core.Communication.Telemetry;

[Flags]
public enum CompactTelemetryField : ulong
{
    None = 0,

    PositionX = 1UL << 0,
    PositionY = 1UL << 1,
    PositionZ = 1UL << 2,

    Roll = 1UL << 3,
    Pitch = 1UL << 4,
    Yaw = 1UL << 5,

    VelocityX = 1UL << 6,
    VelocityY = 1UL << 7,
    VelocityZ = 1UL << 8,

    AngularVelocityX = 1UL << 9,
    AngularVelocityY = 1UL << 10,
    AngularVelocityZ = 1UL << 11,

    Speed = 1UL << 12,
    HeadingError = 1UL << 13,
    DistanceToTarget = 1UL << 14,

    BatteryVoltage = 1UL << 15,
    BatteryPercent = 1UL << 16,

    MissionProgress = 1UL << 17,
    RiskScore = 1UL << 18,

    ForceX = 1UL << 19,
    ForceY = 1UL << 20,
    ForceZ = 1UL << 21,

    TorqueX = 1UL << 22,
    TorqueY = 1UL << 23,
    TorqueZ = 1UL << 24,

    AllPose =
        PositionX |
        PositionY |
        PositionZ |
        Roll |
        Pitch |
        Yaw,

    AllVelocity =
        VelocityX |
        VelocityY |
        VelocityZ |
        AngularVelocityX |
        AngularVelocityY |
        AngularVelocityZ,

    AllControl =
        Speed |
        HeadingError |
        DistanceToTarget,

    AllPower =
        BatteryVoltage |
        BatteryPercent,

    AllMission =
        MissionProgress |
        RiskScore,

    AllWrench =
        ForceX |
        ForceY |
        ForceZ |
        TorqueX |
        TorqueY |
        TorqueZ,

    All =
        AllPose |
        AllVelocity |
        AllControl |
        AllPower |
        AllMission |
        AllWrench
}