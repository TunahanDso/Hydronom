using Hydronom.Core.Domain;
using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Simulation.Truth;
using Hydronom.Core.State.Authority;
using Hydronom.Runtime.FusionRuntime;
using Hydronom.Runtime.Sensors.Backends.Common;
using Hydronom.Runtime.Sensors.Diagnostics;
using Hydronom.Runtime.Sensors.Runtime;
using Hydronom.Runtime.Simulation.Physics;
using Hydronom.Runtime.StateRuntime;

Console.WriteLine("=== Hydronom Runtime CDSE Degraded Smoke Test ===");
Console.WriteLine();

var vehicleId = "CDSE-DEGRADED-SMOKE-001";

var truthProvider = new PhysicsTruthProvider("CdseDegradedSmokeTruthProvider");

var truth = new PhysicsTruthState(
    VehicleId: vehicleId,
    TimestampUtc: DateTime.UtcNow,

    /*
     * GPS kapalı olduğu için X/Y doğrudan ölçülemeyecek.
     * CDSE ilk degraded acquisition sırasında X/Y için düşük güvenli origin/placeholder kullanabilir.
     *
     * Z negatif veriliyor çünkü Hydronom world convention:
     * - Z yukarı yöndür.
     * - Depth pozitif aşağı yöndür.
     * - depth = -Z
     */
    Position: new Vec3(9.0, -3.0, -1.80),
    Velocity: new Vec3(0.0, 0.0, -0.05),
    Acceleration: Vec3.Zero,
    Orientation: new Orientation(1.5, -0.75, 123.0),
    AngularVelocityDegSec: new Vec3(0.0, 0.0, 3.0),
    AngularAccelerationDegSec: Vec3.Zero,
    LastAppliedLoads: PhysicsLoads.Zero,
    EnvironmentSummary: "CDSE_DEGRADED_SMOKE_GPS_DISABLED",
    FrameId: "map",
    TraceId: "cdse-degraded-smoke-truth"
);

truthProvider.Publish(truth);

var sensorOptions = SensorRuntimeOptions.Default();
sensorOptions.Mode = SensorRuntimeMode.CSharpPrimary;
sensorOptions.EnableDefaultSimSensors = true;
sensorOptions.EnableImu = true;
sensorOptions.EnableGps = false;
sensorOptions.EnableDepth = true;
sensorOptions.EnableLidar = false;
sensorOptions.EnableCamera = false;

var registry = new SensorBackendRegistry()
    .Register(
        key: "sim_imu",
        factory: _ => new Hydronom.Runtime.Sensors.Backends.Sim.SimImuSensor(
            truthProvider: truthProvider)
    )
    .Register(
        key: "sim_depth",
        factory: _ => new Hydronom.Runtime.Sensors.Backends.Sim.SimDepthSensor(
            truthProvider: truthProvider)
    );

var sensorRuntime = new SensorRuntimeBuilder(registry).Build(sensorOptions);

Console.WriteLine("[1] Sensor runtime created");
Console.WriteLine($"Runtime type : {sensorRuntime.GetType().Name}");
Console.WriteLine($"Runtime mode : {sensorRuntime.Mode}");

if (sensorRuntime is not CSharpSensorRuntime csharpSensorRuntime)
{
    return Fail("Sensor runtime CSharpSensorRuntime değil.");
}

Console.WriteLine($"Backend count : {csharpSensorRuntime.BackendCount}");
Console.WriteLine();

Require(csharpSensorRuntime.BackendCount == 2, "Runtime içinde IMU + Depth olmak üzere 2 backend olmalı.");

await sensorRuntime.StartAsync();

var samples = await sensorRuntime.ReadBatchAsync();
var sensorHealth = sensorRuntime.GetHealth();
var sensorCapabilities = new RuntimeSensorCapabilityProjector()
    .Project(sensorRuntime)
    .Sanitized();

await sensorRuntime.StopAsync();

Console.WriteLine("[2] Sensor batch read");
Console.WriteLine($"Sample count       : {samples.Count}");
Console.WriteLine($"Sensor count       : {sensorHealth.SensorCount}");
Console.WriteLine($"Healthy count      : {sensorHealth.HealthyCount}");
Console.WriteLine($"Has critical issue : {sensorHealth.HasCriticalIssue}");
Console.WriteLine($"Capability count   : {sensorCapabilities.CapabilityCount}");
Console.WriteLine($"Capability summary : {sensorCapabilities.Summary}");
Console.WriteLine($"Has global pos     : {sensorCapabilities.HasGlobalPosition}");
Console.WriteLine($"Has local pos      : {sensorCapabilities.HasLocalPosition}");
Console.WriteLine($"Has attitude       : {sensorCapabilities.HasAttitude}");
Console.WriteLine($"Has depth          : {sensorCapabilities.HasDepth}");

foreach (var sample in samples)
{
    Console.WriteLine(
        $"- {sample.Sensor.SensorId} | source={sample.Sensor.SourceId} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}"
    );
}

Console.WriteLine();

Require(samples.Count == 2, "Sensor batch içinde 2 sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Imu), "Sensor batch içinde IMU sample olmalı.");
Require(samples.Any(x => x.DataKind == SensorDataKind.Depth), "Sensor batch içinde Depth sample olmalı.");
Require(!samples.Any(x => x.DataKind == SensorDataKind.Gps), "Sensor batch içinde GPS sample olmamalı.");
Require(sensorHealth.SensorCount == 2, "Sensor health sensor count 2 olmalı.");
Require(sensorHealth.HealthyCount == 2, "Sensor health healthy count 2 olmalı.");
Require(!sensorHealth.HasCriticalIssue, "Sensor health critical issue olmamalı.");

Require(sensorCapabilities.CapabilityCount > 0, "Degraded capability snapshot boş olmamalı.");
Require(!sensorCapabilities.HasGlobalPosition, "GPS kapalıyken global_position capability false olmalı.");
Require(!sensorCapabilities.HasLocalPosition, "GPS kapalıyken local_position capability false olmalı.");
Require(sensorCapabilities.HasAttitude, "IMU açıkken attitude capability true olmalı.");
Require(sensorCapabilities.HasDepth, "Depth açıkken depth capability true olmalı.");
Require(sensorCapabilities.Summary.Contains("global_position:missing", StringComparison.OrdinalIgnoreCase), "Capability summary global_position:missing içermeli.");
Require(sensorCapabilities.Summary.Contains("depth:available", StringComparison.OrdinalIgnoreCase), "Capability summary depth:available içermeli.");

var policy = StateAuthorityPolicy.CSharpPrimary with
{
    MaxStateAgeMs = 2_000.0,

    /*
     * Bu testin amacı GPS yokken degraded CDSE adayını kabul ettirmektir.
     * CDSE IMU + Depth ile yaklaşık 0.48 confidence üretebilir.
     * 0.45 bu senaryoyu kabul eder; depth-only gibi çok düşük güvenli adayları ise dışarıda bırakır.
     */
    MinConfidence = 0.45,

    MaxTeleportDistanceMeters = 25.0,
    MaxPlausibleSpeedMps = 25.0,
    MaxPlausibleYawRateDegSec = 180.0,
    RequireFrameMatch = true
};

var authority = new StateAuthorityManager(policy);
var store = new VehicleStateStore(vehicleId, StateAuthorityMode.CSharpPrimary);
var pipeline = new StateUpdatePipeline(authority, store);
var telemetryBridge = new StateTelemetryBridge();

var estimator = new CapabilityDrivenStateEstimator();
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
    traceId: "cdse-degraded-smoke"
);

var tickResult = fusionHost.Tick(
    samples,
    context,
    utcNow: DateTime.UtcNow
);

var safeTick = tickResult.Sanitized();

Console.WriteLine("[3] FusionRuntimeHost degraded tick result");
Console.WriteLine($"Estimator            : {estimator.Name}");
Console.WriteLine($"Input samples        : {safeTick.InputSampleCount}");
Console.WriteLine($"Candidate produced   : {safeTick.CandidateProduced}");
Console.WriteLine($"State submitted      : {safeTick.StateUpdateSubmitted}");
Console.WriteLine($"State accepted       : {safeTick.StateUpdateAccepted}");
Console.WriteLine($"Decision             : {safeTick.Decision}");
Console.WriteLine($"Reason               : {safeTick.Reason}");
Console.WriteLine();

Require(estimator.Name == "capability_driven_state_estimator", "Estimator CDSE olmalı.");
Require(safeTick.InputSampleCount == 2, "Fusion host 2 input sample görmeli.");
Require(safeTick.CandidateProduced, "Fusion host GPS yokken de candidate üretmeli.");
Require(safeTick.StateUpdateSubmitted, "Fusion host degraded candidate'ı state pipeline'a göndermeli.");
Require(safeTick.StateUpdateAccepted, "StateAuthority IMU + Depth degraded candidate'ı kabul etmeli.");
Require(safeTick.Decision == StateUpdateDecision.Accepted, "State update decision Accepted olmalı.");

var current = store.Current;

Console.WriteLine("[4] VehicleStateStore degraded current state");
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
Require(current.Confidence >= 0.45, "Store current degraded confidence authority eşiğini geçmeli.");
Require(current.Confidence <= 0.50, "Store current confidence GPS yokken sınırlı kalmalı.");
Require(current.QualitySummary.Contains("INITIAL_ACQUISITION", StringComparison.OrdinalIgnoreCase), "Store quality initial acquisition işaretini taşımalı.");

/*
 * GPS yokken X/Y kesin ölçüm değildir.
 * İlk degraded acquisition'da CDSE X/Y için origin/placeholder kullanabilir.
 */
Require(Math.Abs(current.Pose.X) < 0.001, "GPS yokken ilk degraded X origin/placeholder kalmalı.");
Require(Math.Abs(current.Pose.Y) < 0.001, "GPS yokken ilk degraded Y origin/placeholder kalmalı.");

Require(Math.Abs(current.Pose.Z - truth.Position.Z) < 0.05, "Depth sayesinde Z truth/depth değerine yakın olmalı.");
Require(Math.Abs(current.Pose.YawDeg - truth.Orientation.YawDeg) < 0.001, "Yaw IMU/truth yaw değerinden gelmeli.");

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

Require(fusionDiagnostics.FusionEngineName == "capability_driven_state_estimator", "Fusion diagnostics engine CDSE olmalı.");
Require(fusionDiagnostics.InputSampleCount == 2, "Fusion diagnostics input count 2 olmalı.");
Require(fusionDiagnostics.UsedSampleCount == 2, "Fusion diagnostics used count 2 olmalı.");
Require(fusionDiagnostics.ProducedCandidate, "Fusion diagnostics produced candidate true olmalı.");
Require(fusionDiagnostics.Confidence >= 0.45, "Fusion diagnostics degraded confidence authority eşiğini geçmeli.");
Require(fusionDiagnostics.Confidence <= 0.50, "Fusion diagnostics confidence GPS yokken sınırlı kalmalı.");
Require(fusionDiagnostics.Summary.Contains("gps=missing", StringComparison.OrdinalIgnoreCase), "Fusion diagnostics GPS kaybını göstermeli.");
Require(fusionDiagnostics.Summary.Contains("imu=ok", StringComparison.OrdinalIgnoreCase), "Fusion diagnostics IMU kullanımını göstermeli.");
Require(fusionDiagnostics.Summary.Contains("depth=ok", StringComparison.OrdinalIgnoreCase), "Fusion diagnostics Depth kullanımını göstermeli.");
Require(fusionDiagnostics.Summary.Contains("degraded_estimated", StringComparison.OrdinalIgnoreCase), "Fusion diagnostics degraded mode göstermeli.");

var debugBridge = new FusionTelemetryBridge();
var debugSummary = debugBridge.ProjectDebugSummary(safeTick);

Console.WriteLine("[7] Fusion telemetry debug summary");
Console.WriteLine(debugSummary);
Console.WriteLine();

Require(!string.IsNullOrWhiteSpace(debugSummary), "Fusion telemetry debug summary boş olmamalı.");
Require(debugSummary.Contains("accepted=True", StringComparison.OrdinalIgnoreCase), "Debug summary accepted=True içermeli.");
Require(debugSummary.Contains("capability_driven_state_estimator", StringComparison.OrdinalIgnoreCase), "Debug summary CDSE engine adını içermeli.");
Require(debugSummary.Contains("gps=missing", StringComparison.OrdinalIgnoreCase), "Debug summary GPS kaybını içermeli.");
Require(debugSummary.Contains("depth=ok", StringComparison.OrdinalIgnoreCase), "Debug summary Depth kullanımını içermeli.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: Runtime CDSE degraded smoke test GPS yokken IMU + Depth ile authoritative degraded state ve capability snapshot üretti.");
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