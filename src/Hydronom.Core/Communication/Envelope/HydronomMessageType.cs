namespace Hydronom.Core.Communication.Envelope;

public enum HydronomMessageType : ushort
{
    Unknown = 0,

    Heartbeat = 1,
    Hello = 2,
    Goodbye = 3,

    VehicleState = 10,
    FusedState = 11,
    SensorSample = 12,
    ExternalPose = 13,

    MissionStatus = 30,
    MissionCommand = 31,
    ScenarioStatus = 32,
    WorldSnapshot = 33,
    WorldDelta = 34,

    ManualControl = 50,
    ActuatorCommand = 51,
    ActuatorStatus = 52,

    Arm = 70,
    Disarm = 71,
    EmergencyStop = 72,
    AuthorityClaim = 73,

    DiagnosticSummary = 100,
    DiagnosticDetail = 101,
    Health = 102,

    AiAdvice = 130,
    AiStatus = 131,
    AiToolCall = 132,
    AiToolResult = 133,

    LogBatch = 150,
    ReplayFrame = 151,

    Ack = 200,
    Nack = 201,
    TimeSyncPing = 210,
    TimeSyncPong = 211,

    PicoNodeHello = 300,
    PicoSensorFrame = 301,
    PicoNodeHealth = 302,
    PicoNodeCommand = 303
}