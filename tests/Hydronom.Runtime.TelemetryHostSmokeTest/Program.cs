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

Console.WriteLine("=== Hydronom Runtime Telemetry Host Smoke Test ===");
Console.WriteLine();

var vehicleId = "TELEMETRY-HOST-SMOKE-001";

var truthProvider = new PhysicsTruthProvider("TelemetryHostSmokeTruthProvider");

var truth = new PhysicsTruthState(
    VehicleId: vehicleId,
    TimestampUtc: DateTime.UtcNow,
    Position: new Vec3(14.25, -3.75, 0.75),
    Velocity: new Vec3(1.40, 0.20, 0.0),
    Acceleration: Vec3.Zero,
    Orientation: new Orientation(2.0, -1.0, 38.0),
    AngularVelocityDegSec: new Vec3(0.0, 0.0, 4.5),
    AngularAccelerationDegSec: Vec3.Zero,
    LastAppliedLoads: PhysicsLoads.Zero,
    EnvironmentSummary: "TELEMETRY_HOST_SMOKE",
    FrameId: "map",
    TraceId: "telemetry-host-smoke-truth"
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

Console.WriteLine("[1] Sensor runtime created");
Console.WriteLine($"Runtime type : {sensorRuntime.GetType().Name}");
Console.WriteLine($"Runtime mode : {sensorRuntime.Mode}");
Console.WriteLine($"Truth ready  : {truthProvider.IsAvailable}");
Console.WriteLine();

Require(sensorRuntime.Mode == SensorRuntimeMode.CSharpPrimary, "Sensor runtime CSharpPrimary olmalı.");

await sensorRuntime.StartAsync();

var samples = await sensorRuntime.ReadBatchAsync();
var sensorHealth = sensorRuntime.GetHealth();

await sensorRuntime.StopAsync();

Console.WriteLine("[2] Sensor batch");
Console.WriteLine($"Sample count       : {samples.Count}");
Console.WriteLine($"Sensor count       : {sensorHealth.SensorCount}");
Console.WriteLine($"Healthy count      : {sensorHealth.HealthyCount}");
Console.WriteLine($"Has critical issue : {sensorHealth.HasCriticalIssue}");

foreach (var sample in samples)
{
   Console.WriteLine($"- {sample.Sensor.SensorId} | source={sample.Source} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}");
}
Console.WriteLine();

Require(samples.Count == 2, "Sensor runtime 2 sample üretmeli.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Gps), "GPS sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Imu), "IMU sample olmalı.");
Require(sensorHealth.SensorCount == 2, "Sensor health sensor count 2 olmalı.");
Require(sensorHealth.HealthyCount == 2, "Sensor health healthy count 2 olmalı.");
Require(!sensorHealth.HasCriticalIssue, "Sensor health critical issue olmamalı.");

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

var context = FusionContext.Create(
    vehicleId: vehicleId,
    frameId: "map",
    maxSampleAgeMs: 2_000.0,
    traceId: "telemetry-host-smoke-context"
);

var tick = fusionHost.Tick(samples, context, utcNow: DateTime.UtcNow).Sanitized();

Console.WriteLine("[3] FusionRuntimeHost tick");
Console.WriteLine($"Candidate produced : {tick.CandidateProduced}");
Console.WriteLine($"State submitted    : {tick.StateUpdateSubmitted}");
Console.WriteLine($"State accepted     : {tick.StateUpdateAccepted}");
Console.WriteLine($"Decision           : {tick.Decision}");
Console.WriteLine($"Reason             : {tick.Reason}");
Console.WriteLine();

Require(tick.CandidateProduced, "Fusion host candidate üretmeli.");
Require(tick.StateUpdateSubmitted, "Fusion host state update submit etmeli.");
Require(tick.StateUpdateAccepted, "Fusion host state update kabul etmeli.");
Require(tick.Decision == StateUpdateDecision.Accepted, "Fusion decision Accepted olmalı.");

var memoryPublisher = new MemoryRuntimeTelemetryPublisher();

var telemetryHost = new RuntimeTelemetryHost(
    snapshotBuilder: new RuntimeOperationSnapshotBuilder("telemetry_host_smoke_runtime"),
    telemetryBridge: new TelemetryBridge(),
    publisher: memoryPublisher
);

var publishResult = await telemetryHost.PublishAsync(
    sensorHealth,
    fusionHost,
    stateStore,
    CancellationToken.None
);

Console.WriteLine("[4] RuntimeTelemetryHost publish result");
Console.WriteLine($"Published       : {publishResult.Published}");
Console.WriteLine($"RuntimeId       : {publishResult.RuntimeId}");
Console.WriteLine($"VehicleId       : {publishResult.VehicleId}");
Console.WriteLine($"OverallHealth   : {publishResult.OverallHealth}");
Console.WriteLine($"HasCritical     : {publishResult.HasCriticalIssue}");
Console.WriteLine($"HasWarnings     : {publishResult.HasWarnings}");
Console.WriteLine($"Reason          : {publishResult.Reason}");
Console.WriteLine();

Require(publishResult.Published, "Publish result published true olmalı.");
Require(publishResult.RuntimeId == "telemetry_host_smoke_runtime", "Publish result runtime id doğru olmalı.");
Require(publishResult.VehicleId == vehicleId, "Publish result vehicle id doğru olmalı.");
Require(publishResult.OverallHealth == "Healthy", "Publish result health Healthy olmalı.");
Require(!publishResult.HasCriticalIssue, "Publish result critical issue olmamalı.");
Require(!publishResult.HasWarnings, "Publish result warning olmamalı.");
Require(publishResult.Reason == "published", "Publish result reason published olmalı.");

Console.WriteLine("[5] Memory publisher");
Console.WriteLine($"Publish count : {memoryPublisher.PublishCount}");
Console.WriteLine($"Has state     : {memoryPublisher.LastSummary.HasState}");
Console.WriteLine();

Require(memoryPublisher.PublishCount == 1, "Memory publisher bir kez publish almalı.");

var summary = memoryPublisher.LastSummary.Sanitized();

Console.WriteLine("[6] RuntimeTelemetrySummary");
Console.WriteLine($"RuntimeId             : {summary.RuntimeId}");
Console.WriteLine($"OverallHealth         : {summary.OverallHealth}");
Console.WriteLine($"SensorCount           : {summary.SensorCount}");
Console.WriteLine($"HealthySensorCount    : {summary.HealthySensorCount}");
Console.WriteLine($"FusionEngine          : {summary.FusionEngineName}");
Console.WriteLine($"FusionProduced        : {summary.FusionProducedCandidate}");
Console.WriteLine($"FusionConfidence      : {summary.FusionConfidence:F3}");
Console.WriteLine($"VehicleId             : {summary.VehicleId}");
Console.WriteLine($"HasState              : {summary.HasState}");
Console.WriteLine($"State                 : X={summary.StateX:F3}, Y={summary.StateY:F3}, Z={summary.StateZ:F3}, Yaw={summary.StateYawDeg:F3}");
Console.WriteLine($"StateConfidence       : {summary.StateConfidence:F3}");
Console.WriteLine($"LastStateDecision     : {summary.LastStateDecision}");
Console.WriteLine($"LastStateAccepted     : {summary.LastStateAccepted}");
Console.WriteLine($"AcceptedUpdateCount   : {summary.AcceptedStateUpdateCount}");
Console.WriteLine($"RejectedUpdateCount   : {summary.RejectedStateUpdateCount}");
Console.WriteLine($"Summary               : {summary.Summary}");
Console.WriteLine();

Require(summary.RuntimeId == "telemetry_host_smoke_runtime", "Summary runtime id doğru olmalı.");
Require(summary.OverallHealth == "Healthy", "Summary health Healthy olmalı.");
Require(!summary.HasCriticalIssue, "Summary critical issue olmamalı.");
Require(!summary.HasWarnings, "Summary warning olmamalı.");
Require(summary.SensorCount == 2, "Summary sensor count 2 olmalı.");
Require(summary.HealthySensorCount == 2, "Summary healthy sensor count 2 olmalı.");
Require(summary.FusionEngineName == "gps_imu_state_estimator", "Summary fusion engine doğru olmalı.");
Require(summary.FusionProducedCandidate, "Summary fusion produced true olmalı.");
Require(summary.FusionConfidence >= 0.90, "Summary fusion confidence yüksek olmalı.");
Require(summary.VehicleId == vehicleId, "Summary vehicle id doğru olmalı.");
Require(summary.HasState, "Summary has state true olmalı.");
Require(Math.Abs(summary.StateX - truth.Position.X) < 0.20, "Summary StateX truth/GPS local X'e yakın olmalı.");
Require(Math.Abs(summary.StateY - truth.Position.Y) < 0.20, "Summary StateY truth/GPS local Y'ye yakın olmalı.");
Require(Math.Abs(summary.StateZ - truth.Position.Z) < 0.001, "Summary StateZ truth/GPS local Z olmalı.");
Require(Math.Abs(summary.StateYawDeg - truth.Orientation.YawDeg) < 0.001, "Summary yaw IMU truth yaw olmalı.");
Require(summary.LastStateDecision == StateUpdateDecision.Accepted.ToString(), "Summary last decision Accepted olmalı.");
Require(summary.LastStateAccepted, "Summary last accepted true olmalı.");
Require(summary.AcceptedStateUpdateCount == 1, "Summary accepted update count 1 olmalı.");
Require(summary.RejectedStateUpdateCount == 0, "Summary rejected update count 0 olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: RuntimeTelemetryHost gerçek CSharpSensorRuntime sample'larıyla snapshot, telemetry bridge ve publisher zincirini doğru çalıştırdı.");
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