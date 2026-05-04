using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Abstractions;
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

Console.WriteLine("=== Hydronom Runtime Telemetry Pipeline Smoke Test ===");
Console.WriteLine();

var vehicleId = "TELEMETRY-PIPELINE-SMOKE-001";

var truthProvider = new PhysicsTruthProvider("TelemetryPipelineSmokeTruthProvider");

var truth = new PhysicsTruthState(
    VehicleId: vehicleId,
    TimestampUtc: DateTime.UtcNow,
    Position: new Vec3(21.40, -8.20, 0.90),
    Velocity: new Vec3(1.10, 0.35, 0.0),
    Acceleration: Vec3.Zero,
    Orientation: new Orientation(4.0, -2.0, 72.0),
    AngularVelocityDegSec: new Vec3(0.0, 0.0, 6.0),
    AngularAccelerationDegSec: Vec3.Zero,
    LastAppliedLoads: PhysicsLoads.Zero,
    EnvironmentSummary: "TELEMETRY_PIPELINE_SMOKE",
    FrameId: "map",
    TraceId: "telemetry-pipeline-smoke-truth"
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

var policy = StateAuthorityPolicy.CSharpPrimary with
{
    MaxStateAgeMs = 2_000.0,
    MinConfidence = 0.50,
    MaxTeleportDistanceMeters = 50.0,
    MaxPlausibleSpeedMps = 50.0,
    MaxPlausibleYawRateDegSec = 360.0,
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

var memoryPublisher = new MemoryRuntimeTelemetryPublisher();

var telemetryHost = new RuntimeTelemetryHost(
    snapshotBuilder: new RuntimeOperationSnapshotBuilder("telemetry_pipeline_smoke_runtime"),
    telemetryBridge: new TelemetryBridge(),
    publisher: memoryPublisher
);

await using var pipeline = new RuntimeTelemetryPipeline(
    sensorRuntime: sensorRuntime,
    fusionHost: fusionHost,
    stateStore: stateStore,
    telemetryHost: telemetryHost,
    vehicleId: vehicleId,
    frameId: "map",
    maxSampleAgeMs: 2_000.0
);

Console.WriteLine("[1] Pipeline created");
Console.WriteLine($"Started      : {pipeline.IsStarted}");
Console.WriteLine($"TickIndex    : {pipeline.TickIndex}");
Console.WriteLine($"VehicleId    : {vehicleId}");
Console.WriteLine($"Truth ready  : {truthProvider.IsAvailable}");
Console.WriteLine();

Require(!pipeline.IsStarted, "Pipeline başlangıçta started false olmalı.");
Require(pipeline.TickIndex == 0, "Pipeline başlangıç tick index 0 olmalı.");
Require(truthProvider.IsAvailable, "Truth provider available olmalı.");

await pipeline.StartAsync();

Console.WriteLine("[2] Pipeline started");
Console.WriteLine($"Started      : {pipeline.IsStarted}");
Console.WriteLine();

Require(pipeline.IsStarted, "Pipeline StartAsync sonrası started true olmalı.");

var tick1 = await pipeline.TickAsync();

Console.WriteLine("[3] Pipeline tick #1");
Console.WriteLine($"Executed             : {tick1.Executed}");
Console.WriteLine($"TickIndex            : {tick1.TickIndex}");
Console.WriteLine($"SampleCount          : {tick1.SampleCount}");
Console.WriteLine($"SensorCount          : {tick1.SensorCount}");
Console.WriteLine($"HealthySensorCount   : {tick1.HealthySensorCount}");
Console.WriteLine($"CandidateProduced    : {tick1.CandidateProduced}");
Console.WriteLine($"StateSubmitted       : {tick1.StateSubmitted}");
Console.WriteLine($"StateAccepted        : {tick1.StateAccepted}");
Console.WriteLine($"Published            : {tick1.Published}");
Console.WriteLine($"RuntimeId            : {tick1.RuntimeId}");
Console.WriteLine($"VehicleId            : {tick1.VehicleId}");
Console.WriteLine($"OverallHealth        : {tick1.OverallHealth}");
Console.WriteLine($"Reason               : {tick1.Reason}");
Console.WriteLine();

Require(tick1.Executed, "Tick #1 executed true olmalı.");
Require(tick1.TickIndex == 0, "Tick #1 index 0 olmalı.");
Require(tick1.SampleCount == 2, "Tick #1 sample count 2 olmalı.");
Require(tick1.SensorCount == 2, "Tick #1 sensor count 2 olmalı.");
Require(tick1.HealthySensorCount == 2, "Tick #1 healthy sensor count 2 olmalı.");
Require(tick1.CandidateProduced, "Tick #1 candidate produced true olmalı.");
Require(tick1.StateSubmitted, "Tick #1 state submitted true olmalı.");
Require(tick1.StateAccepted, "Tick #1 state accepted true olmalı.");
Require(tick1.Published, "Tick #1 published true olmalı.");
Require(tick1.RuntimeId == "telemetry_pipeline_smoke_runtime", "Tick #1 runtime id doğru olmalı.");
Require(tick1.VehicleId == vehicleId, "Tick #1 vehicle id doğru olmalı.");
Require(tick1.OverallHealth == "Healthy", "Tick #1 overall health Healthy olmalı.");
Require(tick1.Reason == "published", "Tick #1 reason published olmalı.");

Require(pipeline.TickIndex == 1, "Pipeline tick index #1 sonrası 1 olmalı.");
Require(memoryPublisher.PublishCount == 1, "Memory publisher #1 sonrası publish count 1 olmalı.");

var summary1 = memoryPublisher.LastSummary.Sanitized();

Console.WriteLine("[4] Summary after tick #1");
Console.WriteLine($"RuntimeId             : {summary1.RuntimeId}");
Console.WriteLine($"OverallHealth         : {summary1.OverallHealth}");
Console.WriteLine($"SensorCount           : {summary1.SensorCount}");
Console.WriteLine($"HealthySensorCount    : {summary1.HealthySensorCount}");
Console.WriteLine($"FusionEngine          : {summary1.FusionEngineName}");
Console.WriteLine($"FusionProduced        : {summary1.FusionProducedCandidate}");
Console.WriteLine($"FusionConfidence      : {summary1.FusionConfidence:F3}");
Console.WriteLine($"VehicleId             : {summary1.VehicleId}");
Console.WriteLine($"HasState              : {summary1.HasState}");
Console.WriteLine($"State                 : X={summary1.StateX:F3}, Y={summary1.StateY:F3}, Z={summary1.StateZ:F3}, Yaw={summary1.StateYawDeg:F3}");
Console.WriteLine($"LastStateDecision     : {summary1.LastStateDecision}");
Console.WriteLine($"LastStateAccepted     : {summary1.LastStateAccepted}");
Console.WriteLine($"AcceptedUpdateCount   : {summary1.AcceptedStateUpdateCount}");
Console.WriteLine($"RejectedUpdateCount   : {summary1.RejectedStateUpdateCount}");
Console.WriteLine($"Summary               : {summary1.Summary}");
Console.WriteLine();

Require(summary1.RuntimeId == "telemetry_pipeline_smoke_runtime", "Summary #1 runtime id doğru olmalı.");
Require(summary1.OverallHealth == "Healthy", "Summary #1 health Healthy olmalı.");
Require(!summary1.HasCriticalIssue, "Summary #1 critical issue olmamalı.");
Require(!summary1.HasWarnings, "Summary #1 warning olmamalı.");
Require(summary1.SensorCount == 2, "Summary #1 sensor count 2 olmalı.");
Require(summary1.HealthySensorCount == 2, "Summary #1 healthy sensor count 2 olmalı.");
Require(summary1.FusionProducedCandidate, "Summary #1 fusion produced true olmalı.");
Require(summary1.FusionConfidence >= 0.90, "Summary #1 fusion confidence yüksek olmalı.");
Require(summary1.VehicleId == vehicleId, "Summary #1 vehicle id doğru olmalı.");
Require(summary1.HasState, "Summary #1 has state true olmalı.");
Require(Math.Abs(summary1.StateX - truth.Position.X) < 0.25, "Summary #1 StateX truth/GPS X'e yakın olmalı.");
Require(Math.Abs(summary1.StateY - truth.Position.Y) < 0.25, "Summary #1 StateY truth/GPS Y'ye yakın olmalı.");
Require(Math.Abs(summary1.StateZ - truth.Position.Z) < 0.001, "Summary #1 StateZ truth/GPS Z olmalı.");
Require(Math.Abs(summary1.StateYawDeg - truth.Orientation.YawDeg) < 0.001, "Summary #1 yaw IMU truth yaw olmalı.");
Require(summary1.LastStateDecision == StateUpdateDecision.Accepted.ToString(), "Summary #1 last decision Accepted olmalı.");
Require(summary1.LastStateAccepted, "Summary #1 last accepted true olmalı.");
Require(summary1.AcceptedStateUpdateCount == 1, "Summary #1 accepted count 1 olmalı.");
Require(summary1.RejectedStateUpdateCount == 0, "Summary #1 rejected count 0 olmalı.");

var truth2 = truth with
{
    TimestampUtc = DateTime.UtcNow.AddMilliseconds(200),
    Position = new Vec3(22.10, -8.00, 0.90),
    Velocity = new Vec3(1.20, 0.25, 0.0),
    Orientation = new Orientation(4.0, -2.0, 74.0),
    AngularVelocityDegSec = new Vec3(0.0, 0.0, 5.0),
    TraceId = "telemetry-pipeline-smoke-truth-2"
};

truthProvider.Publish(truth2);

var tick2 = await pipeline.TickAsync();

Console.WriteLine("[5] Pipeline tick #2");
Console.WriteLine($"Executed             : {tick2.Executed}");
Console.WriteLine($"TickIndex            : {tick2.TickIndex}");
Console.WriteLine($"SampleCount          : {tick2.SampleCount}");
Console.WriteLine($"SensorCount          : {tick2.SensorCount}");
Console.WriteLine($"HealthySensorCount   : {tick2.HealthySensorCount}");
Console.WriteLine($"CandidateProduced    : {tick2.CandidateProduced}");
Console.WriteLine($"StateSubmitted       : {tick2.StateSubmitted}");
Console.WriteLine($"StateAccepted        : {tick2.StateAccepted}");
Console.WriteLine($"Published            : {tick2.Published}");
Console.WriteLine($"RuntimeId            : {tick2.RuntimeId}");
Console.WriteLine($"VehicleId            : {tick2.VehicleId}");
Console.WriteLine($"OverallHealth        : {tick2.OverallHealth}");
Console.WriteLine($"Reason               : {tick2.Reason}");
Console.WriteLine();

Require(tick2.Executed, "Tick #2 executed true olmalı.");
Require(tick2.TickIndex == 1, "Tick #2 index 1 olmalı.");
Require(tick2.SampleCount == 2, "Tick #2 sample count 2 olmalı.");
Require(tick2.SensorCount == 2, "Tick #2 sensor count 2 olmalı.");
Require(tick2.HealthySensorCount == 2, "Tick #2 healthy sensor count 2 olmalı.");
Require(tick2.CandidateProduced, "Tick #2 candidate produced true olmalı.");
Require(tick2.StateSubmitted, "Tick #2 state submitted true olmalı.");
Require(tick2.StateAccepted, "Tick #2 state accepted true olmalı.");
Require(tick2.Published, "Tick #2 published true olmalı.");
Require(tick2.RuntimeId == "telemetry_pipeline_smoke_runtime", "Tick #2 runtime id doğru olmalı.");
Require(tick2.VehicleId == vehicleId, "Tick #2 vehicle id doğru olmalı.");
Require(tick2.OverallHealth == "Healthy", "Tick #2 overall health Healthy olmalı.");
Require(tick2.Reason == "published", "Tick #2 reason published olmalı.");

Require(pipeline.TickIndex == 2, "Pipeline tick index #2 sonrası 2 olmalı.");
Require(memoryPublisher.PublishCount == 2, "Memory publisher #2 sonrası publish count 2 olmalı.");

var summary2 = memoryPublisher.LastSummary.Sanitized();

Console.WriteLine("[6] Summary after tick #2");
Console.WriteLine($"State                 : X={summary2.StateX:F3}, Y={summary2.StateY:F3}, Z={summary2.StateZ:F3}, Yaw={summary2.StateYawDeg:F3}");
Console.WriteLine($"AcceptedUpdateCount   : {summary2.AcceptedStateUpdateCount}");
Console.WriteLine($"RejectedUpdateCount   : {summary2.RejectedStateUpdateCount}");
Console.WriteLine();

Require(Math.Abs(summary2.StateX - truth2.Position.X) < 0.25, "Summary #2 StateX yeni truth/GPS X'e yakın olmalı.");
Require(Math.Abs(summary2.StateY - truth2.Position.Y) < 0.25, "Summary #2 StateY yeni truth/GPS Y'ye yakın olmalı.");
Require(Math.Abs(summary2.StateZ - truth2.Position.Z) < 0.001, "Summary #2 StateZ yeni truth/GPS Z olmalı.");
Require(Math.Abs(summary2.StateYawDeg - truth2.Orientation.YawDeg) < 0.001, "Summary #2 yaw yeni IMU truth yaw olmalı.");
Require(summary2.AcceptedStateUpdateCount == 2, "Summary #2 accepted count 2 olmalı.");
Require(summary2.RejectedStateUpdateCount == 0, "Summary #2 rejected count 0 olmalı.");

await pipeline.StopAsync();

Console.WriteLine("[7] Pipeline stopped");
Console.WriteLine($"Started : {pipeline.IsStarted}");
Console.WriteLine();

Require(!pipeline.IsStarted, "Pipeline StopAsync sonrası started false olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: RuntimeTelemetryPipeline gerçek CSharpSensorRuntime + FusionRuntimeHost + RuntimeTelemetryHost zincirini doğru çalıştırdı.");
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

public sealed class MemoryRuntimeTelemetryPublisher : IRuntimeTelemetryPublisher
{
    public int PublishCount { get; private set; }

    public RuntimeTelemetrySummary LastSummary { get; private set; }

    public Task PublishAsync(RuntimeTelemetrySummary summary, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        LastSummary = summary.Sanitized();
        PublishCount++;

        return Task.CompletedTask;
    }
}