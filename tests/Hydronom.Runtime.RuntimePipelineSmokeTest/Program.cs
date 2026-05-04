using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.State.Authority;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.StateRuntime;

Console.WriteLine("=== Hydronom Runtime Pipeline Integration Smoke Test ===");
Console.WriteLine();

var vehicleId = "RUNTIME-PIPELINE-SMOKE-001";

var truthProvider = new PhysicsTruthProvider("RuntimePipelineTruthProvider");

var truth = new PhysicsTruthState(
    VehicleId: vehicleId,
    TimestampUtc: DateTime.UtcNow,
    Position: new Vec3(4.0, 1.5, 0.2),
    Velocity: new Vec3(1.5, 0.5, 0.0),
    Acceleration: new Vec3(0.1, 0.0, 0.0),
    Orientation: new Orientation(2.0, -1.0, 35.0),
    AngularVelocityDegSec: new Vec3(0.5, -0.2, 8.0),
    AngularAccelerationDegSec: Vec3.Zero,
    LastAppliedLoads: PhysicsLoads.Zero,
    EnvironmentSummary: "RUNTIME_PIPELINE_SMOKE",
    FrameId: "map",
    TraceId: "runtime-pipeline-truth"
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

var sensorBuilder = new SensorRuntimeBuilder(registry);
var sensorRuntime = sensorBuilder.Build(sensorOptions);

Console.WriteLine("[1] Sensor runtime created");
Console.WriteLine($"Runtime type : {sensorRuntime.GetType().Name}");
Console.WriteLine($"Runtime mode : {sensorRuntime.Mode}");

if (sensorRuntime is not CSharpSensorRuntime csharpSensorRuntime)
{
    return Fail("Sensor runtime CSharpSensorRuntime değil.");
}

Console.WriteLine($"Backend count : {csharpSensorRuntime.BackendCount}");
Console.WriteLine();

Require(csharpSensorRuntime.BackendCount == 2, "Runtime içinde IMU + GPS olmak üzere 2 backend olmalı.");

await sensorRuntime.StartAsync();

var samples = await sensorRuntime.ReadBatchAsync();

await sensorRuntime.StopAsync();

Console.WriteLine("[2] Sensor batch read");
Console.WriteLine($"Sample count : {samples.Count}");

foreach (var sample in samples)
{
    Console.WriteLine(
        $"- {sample.Sensor.SensorId} | source={sample.Sensor.SourceId} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}"
    );
}

Console.WriteLine();

Require(samples.Count == 2, "Sensor batch içinde 2 sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Imu), "Sensor batch içinde IMU sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Gps), "Sensor batch içinde GPS sample olmalı.");

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
var store = new VehicleStateStore(vehicleId, StateAuthorityMode.CSharpPrimary);
var pipeline = new StateUpdatePipeline(authority, store);
var telemetryBridge = new StateTelemetryBridge();

var estimator = new GpsImuStateEstimator();
var runner = new StateEstimatorRunner(estimator);

var fusionHost = new FusionRuntimeHost(
    estimatorRunner: runner,
    statePipeline: pipeline,
    stateStore: store,
    stateTelemetryBridge: telemetryBridge
);

var context = FusionContext.Create(
    vehicleId: vehicleId,
    frameId: "map",
    maxSampleAgeMs: 2_000.0,
    traceId: "runtime-pipeline-smoke"
);

var tickResult = fusionHost.Tick(
    samples,
    context,
    utcNow: DateTime.UtcNow
);

var safeTick = tickResult.Sanitized();

Console.WriteLine("[3] FusionRuntimeHost tick result");
Console.WriteLine($"Input samples        : {safeTick.InputSampleCount}");
Console.WriteLine($"Candidate produced   : {safeTick.CandidateProduced}");
Console.WriteLine($"State submitted      : {safeTick.StateUpdateSubmitted}");
Console.WriteLine($"State accepted       : {safeTick.StateUpdateAccepted}");
Console.WriteLine($"Decision             : {safeTick.Decision}");
Console.WriteLine($"Reason               : {safeTick.Reason}");
Console.WriteLine();

Require(safeTick.InputSampleCount == 2, "Fusion host 2 input sample görmeli.");
Require(safeTick.CandidateProduced, "Fusion host candidate üretmeli.");
Require(safeTick.StateUpdateSubmitted, "Fusion host state pipeline'a update göndermeli.");
Require(safeTick.StateUpdateAccepted, "Fusion host valid initial acquisition candidate'ı kabul ettirmeli.");
Require(safeTick.Decision == StateUpdateDecision.Accepted, "State update decision Accepted olmalı.");
Require(
    safeTick.Reason.Contains("İlk güvenilir", StringComparison.OrdinalIgnoreCase) ||
    safeTick.Reason.Contains("initial", StringComparison.OrdinalIgnoreCase),
    "Accepted reason initial acquisition olduğunu belirtmeli."
);

var current = store.Current;

Console.WriteLine("[4] VehicleStateStore current state");
Console.WriteLine($"VehicleId  : {current.VehicleId}");
Console.WriteLine($"Pose       : X={current.Pose.X:F3}, Y={current.Pose.Y:F3}, Z={current.Pose.Z:F3}, Yaw={current.Pose.YawDeg:F3}");
Console.WriteLine($"Source     : {current.SourceKind}");
Console.WriteLine($"Confidence : {current.Confidence:F3}");
Console.WriteLine($"Frame      : {current.FrameId}");
Console.WriteLine($"Quality    : {current.QualitySummary}");
Console.WriteLine();

Require(store.AcceptedUpdateCount == 1, "Store accepted count 1 olmalı.");
Require(store.RejectedUpdateCount == 0, "Store rejected count 0 olmalı.");
Require(current.SourceKind == VehicleStateSourceKind.CSharpFusion, "Store current source CSharpFusion olmalı.");
Require(current.Confidence >= 0.50, "Store current confidence yeterli olmalı.");
Require(current.QualitySummary.Contains("INITIAL_ACQUISITION", StringComparison.OrdinalIgnoreCase), "Store quality initial acquisition işaretini taşımalı.");
Require(Math.Abs(current.Pose.X - truth.Position.X) < 2.0, "Store Pose X truth pozisyonuna yakın olmalı.");
Require(Math.Abs(current.Pose.Y - truth.Position.Y) < 2.0, "Store Pose Y truth pozisyonuna yakın olmalı.");
Require(Math.Abs(current.Pose.YawDeg - truth.Orientation.YawDeg) < 0.001, "Store yaw IMU/truth yaw değerinden gelmeli.");

var telemetry = fusionHost.LastTelemetry.Sanitized();

Console.WriteLine("[5] State telemetry");
Console.WriteLine($"VehicleId      : {telemetry.VehicleId}");
Console.WriteLine($"Has state      : {telemetry.HasState}");
Console.WriteLine($"Last accepted  : {telemetry.LastUpdateAccepted}");
Console.WriteLine($"Last decision  : {telemetry.LastDecision}");
Console.WriteLine($"Confidence     : {telemetry.Confidence:F3}");
Console.WriteLine($"Accepted count : {telemetry.AcceptedUpdateCount}");
Console.WriteLine($"Rejected count : {telemetry.RejectedUpdateCount}");
Console.WriteLine($"Summary        : {telemetry.Summary}");
Console.WriteLine();

Require(telemetry.HasState, "Telemetry has state true olmalı.");
Require(telemetry.LastUpdateAccepted, "Telemetry son update accepted göstermeli.");
Require(telemetry.LastDecision == StateUpdateDecision.Accepted, "Telemetry last decision Accepted olmalı.");
Require(telemetry.AcceptedUpdateCount == 1, "Telemetry accepted count 1 olmalı.");
Require(telemetry.RejectedUpdateCount == 0, "Telemetry rejected count 0 olmalı.");

var fusionDiagnostics = fusionHost.LastFusionDiagnostics.Sanitized();

Console.WriteLine("[6] Fusion diagnostics");
Console.WriteLine($"Engine             : {fusionDiagnostics.FusionEngineName}");
Console.WriteLine($"Input sample count : {fusionDiagnostics.InputSampleCount}");
Console.WriteLine($"Used sample count  : {fusionDiagnostics.UsedSampleCount}");
Console.WriteLine($"Rejected count     : {fusionDiagnostics.RejectedSampleCount}");
Console.WriteLine($"Produced candidate : {fusionDiagnostics.ProducedCandidate}");
Console.WriteLine($"Confidence         : {fusionDiagnostics.Confidence:F3}");
Console.WriteLine($"Summary            : {fusionDiagnostics.Summary}");
Console.WriteLine();

Require(fusionDiagnostics.InputSampleCount == 2, "Fusion diagnostics input count 2 olmalı.");
Require(fusionDiagnostics.UsedSampleCount == 2, "Fusion diagnostics used count 2 olmalı.");
Require(fusionDiagnostics.ProducedCandidate, "Fusion diagnostics produced candidate true olmalı.");

var debugBridge = new FusionTelemetryBridge();
var debugSummary = debugBridge.ProjectDebugSummary(safeTick);

Console.WriteLine("[7] Fusion telemetry debug summary");
Console.WriteLine(debugSummary);
Console.WriteLine();

Require(!string.IsNullOrWhiteSpace(debugSummary), "Fusion telemetry debug summary boş olmamalı.");
Require(debugSummary.Contains("accepted=True", StringComparison.OrdinalIgnoreCase), "Debug summary accepted=True içermeli.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: Runtime pipeline sensor batch'i initial acquisition ile authoritative state'e dönüştürdü.");
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

static int Fail(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();

    return 1;
}