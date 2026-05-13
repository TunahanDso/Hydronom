namespace Hydronom.Runtime.Scheduling;

/// <summary>
/// Runtime içinde farklı frekanslarda çalışacak ana modül aileleri.
/// Bu enum geçici değildir; telemetry, diagnostics, blackbox ve gelecekteki
/// multi-thread scheduler/host mimarisi için ortak modül kimliğidir.
/// </summary>
public enum RuntimeModuleKind
{
    Unknown = 0,

    SensorRuntime,
    FusionEstimator,
    StateAuthority,

    WorldModel,
    GlobalPlanner,
    LocalPlanner,
    TrajectoryGenerator,

    Analysis,
    Decision,
    Control,
    ActuatorCommand,

    Scenario,
    Telemetry,
    Heartbeat,
    Blackbox,

    NativeBridge,
    TwinPublisher
}