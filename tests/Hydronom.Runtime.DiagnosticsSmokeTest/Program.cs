using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.State.Authority;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Operations.Snapshots;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.StateRuntime;
using Hydronom.Runtime.Telemetry;

Console.WriteLine("=== Hydronom Runtime Diagnostics Smoke Test ===");
Console.WriteLine();

var vehicleId = "DIAGNOSTICS-SMOKE-VEHICLE-001";

var truthProvider = new PhysicsTruthProvider("DiagnosticsSmokeTruthProvider");

var truth = new PhysicsTruthState(
    VehicleId: vehicleId,
    TimestampUtc: DateTime.UtcNow,
    Position: new Vec3(6.0, -2.0, 0.4),
    Velocity: new Vec3(1.2, 0.4, 0.0),
    Acceleration: new Vec3(0.0, 0.0, 0.0),
    Orientation: new Orientation(1.0, -0.5, 22.0),
    AngularVelocityDegSec: new Vec3(0.0, 0.0, 5.0),
    AngularAccelerationDegSec: Vec3.Zero,
    LastAppliedLoads: PhysicsLoads.Zero,
    EnvironmentSummary: "DIAGNOSTICS_SMOKE",
    FrameId: "map",
    TraceId: "diagnostics-smoke-truth"
);

truthProvider.Publish(truth);

var sensorOptions = SensorRuntimeOptions.Default();
sensorOptions.Mode = SensorRuntimeMode.CSharpPrimary;
sensorOptions.EnableDefaultSimSensors = true;
sensorOptions.EnableImu = true;
sensorOptions.EnableGps = true;
sensorOptions.EnableLidar = false;
sensorOptions.EnableCamera = false;

var registry = new SensorBackendRegistry()
    .Register(
        key: "sim_imu",
        factory: _ => new Hydronom.Runtime.Sensors.Imu.SimImuSensor(truthProvider: truthProvider)
    )
    .Register(
        key: "sim_gps",
        factory: _ => new Hydronom.Runtime.Sensors.Gps.SimGpsSensor(truthProvider: truthProvider)
    );

var sensorRuntime = new SensorRuntimeBuilder(registry).Build(sensorOptions);

await sensorRuntime.StartAsync();

var samples = await sensorRuntime.ReadBatchAsync();
var sensorHealth = sensorRuntime.GetHealth();

await sensorRuntime.StopAsync();

Console.WriteLine("[1] Sensor runtime");
Console.WriteLine($"Runtime type       : {sensorRuntime.GetType().Name}");
Console.WriteLine($"Sample count       : {samples.Count}");
Console.WriteLine($"Sensor count       : {sensorHealth.SensorCount}");
Console.WriteLine($"Healthy count      : {sensorHealth.HealthyCount}");
Console.WriteLine($"Has critical issue : {sensorHealth.HasCriticalIssue}");
Console.WriteLine();

Require(samples.Count == 2, "Sensor runtime 2 sample üretmeli.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Imu), "IMU sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Gps), "GPS sample olmalı.");
Require(sensorHealth.SensorCount == 2, "Sensor health sensor count 2 olmalı.");
Require(sensorHealth.HealthyCount == 2, "Sensor health healthy count 2 olmalı.");
Require(!sensorHealth.HasCriticalIssue, "Sensor health critical issue olmamalı.");

var policy = StateAuthorityPolicy.CSharpPrimary with
{
    MaxStateAgeMs = 2_000.0,
    MinConfidence = 0.50,
    MaxTeleportDistanceMeters = 25.0,
    MaxPlausibleSpeedMps = 25.0,
    MaxPlausibleYawRateDegSec = 180.0,
    RequireFrameMatch = true
};

var authority = new StateAuthorityManager(policy);
var stateStore = new VehicleStateStore(vehicleId, StateAuthorityMode.CSharpPrimary);
var statePipeline = new StateUpdatePipeline(authority, stateStore);
var stateTelemetryBridge = new StateTelemetryBridge();

var estimator = new GpsImuStateEstimator();
var runner = new StateEstimatorRunner(estimator);

var fusionHost = new FusionRuntimeHost(
    estimatorRunner: runner,
    statePipeline: statePipeline,
    stateStore: stateStore,
    stateTelemetryBridge: stateTelemetryBridge
);

var context = FusionContext.Create(
    vehicleId: vehicleId,
    frameId: "map",
    maxSampleAgeMs: 2_000.0,
    traceId: "diagnostics-smoke-context"
);

var tick = fusionHost.Tick(samples, context, utcNow: DateTime.UtcNow).Sanitized();

Console.WriteLine("[2] Fusion host");
Console.WriteLine($"Candidate produced : {tick.CandidateProduced}");
Console.WriteLine($"State submitted    : {tick.StateUpdateSubmitted}");
Console.WriteLine($"State accepted     : {tick.StateUpdateAccepted}");
Console.WriteLine($"Decision           : {tick.Decision}");
Console.WriteLine($"Reason             : {tick.Reason}");
Console.WriteLine();

Require(tick.CandidateProduced, "Fusion host candidate üretmeli.");
Require(tick.StateUpdateSubmitted, "Fusion host state update submit etmeli.");
Require(tick.StateUpdateAccepted, "Fusion host initial acquisition kabul etmeli.");
Require(tick.Decision == StateUpdateDecision.Accepted, "Fusion host decision Accepted olmalı.");

var snapshotBuilder = new RuntimeOperationSnapshotBuilder("diagnostics_smoke_runtime");
var diagnosticsSnapshot = snapshotBuilder.Build(
    sensorHealth,
    fusionHost,
    stateStore
).Sanitized();

Console.WriteLine("[3] Runtime diagnostics snapshot");
Console.WriteLine($"Runtime id          : {diagnosticsSnapshot.RuntimeId}");
Console.WriteLine($"Overall health      : {diagnosticsSnapshot.OverallHealth}");
Console.WriteLine($"Has critical issue  : {diagnosticsSnapshot.HasCriticalIssue}");
Console.WriteLine($"Has warnings        : {diagnosticsSnapshot.HasWarnings}");
Console.WriteLine($"Sensor count        : {diagnosticsSnapshot.SensorHealth.SensorCount}");
Console.WriteLine($"Fusion engine       : {diagnosticsSnapshot.FusionDiagnostics.FusionEngineName}");
Console.WriteLine($"Fusion candidate    : {diagnosticsSnapshot.FusionDiagnostics.ProducedCandidate}");
Console.WriteLine($"State accepted      : {diagnosticsSnapshot.StateStoreSnapshot.AcceptedUpdateCount}");
Console.WriteLine($"State rejected      : {diagnosticsSnapshot.StateStoreSnapshot.RejectedUpdateCount}");
Console.WriteLine($"Last decision       : {diagnosticsSnapshot.StateTelemetry.LastDecision}");
Console.WriteLine($"Summary             : {diagnosticsSnapshot.Summary}");
Console.WriteLine();

Require(diagnosticsSnapshot.RuntimeId == "diagnostics_smoke_runtime", "Snapshot runtime id doğru olmalı.");
Require(diagnosticsSnapshot.OverallHealth == "Healthy", "Snapshot overall health Healthy olmalı.");
Require(!diagnosticsSnapshot.HasCriticalIssue, "Snapshot critical issue olmamalı.");
Require(!diagnosticsSnapshot.HasWarnings, "Snapshot warning olmamalı.");
Require(diagnosticsSnapshot.SensorHealth.SensorCount == 2, "Snapshot sensor count 2 olmalı.");
Require(diagnosticsSnapshot.FusionDiagnostics.ProducedCandidate, "Snapshot fusion produced candidate true olmalı.");
Require(diagnosticsSnapshot.StateStoreSnapshot.AcceptedUpdateCount == 1, "Snapshot accepted count 1 olmalı.");
Require(diagnosticsSnapshot.StateStoreSnapshot.RejectedUpdateCount == 0, "Snapshot rejected count 0 olmalı.");
Require(diagnosticsSnapshot.StateTelemetry.LastDecision == StateUpdateDecision.Accepted, "Snapshot last decision Accepted olmalı.");

var telemetryBridge = new TelemetryBridge();
var telemetry = telemetryBridge.Project(diagnosticsSnapshot);

Console.WriteLine("[4] Runtime telemetry summary");
Console.WriteLine($"Runtime id          : {telemetry.RuntimeId}");
Console.WriteLine($"Overall health      : {telemetry.OverallHealth}");
Console.WriteLine($"Sensor count        : {telemetry.SensorCount}");
Console.WriteLine($"Fusion engine       : {telemetry.FusionEngineName}");
Console.WriteLine($"Fusion candidate    : {telemetry.FusionProducedCandidate}");
Console.WriteLine($"Vehicle id          : {telemetry.VehicleId}");
Console.WriteLine($"Has state           : {telemetry.HasState}");
Console.WriteLine($"State pose          : X={telemetry.StateX:F3}, Y={telemetry.StateY:F3}, Z={telemetry.StateZ:F3}, Yaw={telemetry.StateYawDeg:F3}");
Console.WriteLine($"Last decision       : {telemetry.LastStateDecision}");
Console.WriteLine($"Last accepted       : {telemetry.LastStateAccepted}");
Console.WriteLine($"Accepted count      : {telemetry.AcceptedStateUpdateCount}");
Console.WriteLine($"Rejected count      : {telemetry.RejectedStateUpdateCount}");
Console.WriteLine($"Summary             : {telemetry.Summary}");
Console.WriteLine();

Require(telemetry.RuntimeId == "diagnostics_smoke_runtime", "Telemetry runtime id doğru olmalı.");
Require(telemetry.OverallHealth == "Healthy", "Telemetry health Healthy olmalı.");
Require(telemetry.SensorCount == 2, "Telemetry sensor count 2 olmalı.");
Require(telemetry.FusionProducedCandidate, "Telemetry fusion candidate true olmalı.");
Require(telemetry.VehicleId == vehicleId, "Telemetry vehicle id doğru olmalı.");
Require(telemetry.HasState, "Telemetry has state true olmalı.");
Require(telemetry.LastStateDecision == StateUpdateDecision.Accepted.ToString(), "Telemetry last decision Accepted olmalı.");
Require(telemetry.LastStateAccepted, "Telemetry last accepted true olmalı.");
Require(telemetry.AcceptedStateUpdateCount == 1, "Telemetry accepted count 1 olmalı.");
Require(telemetry.RejectedStateUpdateCount == 0, "Telemetry rejected count 0 olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: Runtime diagnostics snapshot ve telemetry summary sensor/fusion/state zincirini doğru taşıdı.");
Console.ResetColor();

return 0;

static void Require(bool condition, string message)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {message}");
        return;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();

    throw new InvalidOperationException(message);
}