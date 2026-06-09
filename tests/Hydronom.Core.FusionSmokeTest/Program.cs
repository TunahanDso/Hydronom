using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
using Hydronom.Core.Sensors.Depth.Models;
using Hydronom.Core.Sensors.Gps.Models;
using Hydronom.Core.Sensors.Imu.Models;
using Hydronom.Core.State.Authority;

Console.WriteLine("=== Hydronom Core Fusion Smoke Test ===");
Console.WriteLine();

var context = FusionContext.Create(
    vehicleId: "FUSION-SMOKE-VEHICLE-001",
    frameId: "map",
    maxSampleAgeMs: 1000.0,
    traceId: "fusion-smoke-test"
);

var now = DateTime.UtcNow;

var gpsSample = CreateGpsSample(now);
var imuSample = CreateImuSample(now);
var depthSample = CreateDepthSample(now);

var samples = new[]
{
    gpsSample,
    imuSample
};

Console.WriteLine("[1] Input samples");
PrintSample(gpsSample);
PrintSample(imuSample);
Console.WriteLine();

var estimator = new GpsImuStateEstimator();

var fused = estimator.Fuse(samples, context);

if (fused is null)
{
    return Fail("GpsImuStateEstimator FusedState üretmedi.");
}

var safeFused = fused.Value.Sanitized();

Console.WriteLine("[2] GpsImuStateEstimator FusedState");
PrintFusedState(safeFused);
Console.WriteLine();

Require(safeFused.IsValid, "FusedState valid olmalı.");
Require(safeFused.VehicleId == context.VehicleId, "FusedState vehicle id context ile aynı olmalı.");
Require(Math.Abs(safeFused.Pose.X - 12.5) < 0.001, "Pose X GPS local X'e yakın olmalı.");
Require(Math.Abs(safeFused.Pose.Y - -4.25) < 0.001, "Pose Y GPS local Y'e yakın olmalı.");
Require(Math.Abs(safeFused.Pose.Z - 1.2) < 0.001, "Pose Z GPS local Z'e yakın olmalı.");
Require(Math.Abs(safeFused.Pose.YawDeg - 47.0) < 0.001, "Pose yaw IMU yaw değerinden gelmeli.");
Require(Math.Abs(safeFused.Attitude.RollDeg - 3.0) < 0.001, "Attitude roll IMU'dan gelmeli.");
Require(Math.Abs(safeFused.Attitude.PitchDeg - -2.0) < 0.001, "Attitude pitch IMU'dan gelmeli.");
Require(Math.Abs(safeFused.Attitude.YawDeg - 47.0) < 0.001, "Attitude yaw IMU'dan gelmeli.");
Require(safeFused.Quality.Confidence >= 0.80, "Fusion confidence yeterli olmalı.");

var candidate = estimator.Estimate(samples, context);

if (candidate is null)
{
    return Fail("GpsImuStateEstimator StateUpdateCandidate üretmedi.");
}

var safeCandidate = candidate.Value.Sanitized();

Console.WriteLine("[3] GpsImuStateEstimator StateUpdateCandidate");
PrintCandidate(safeCandidate);
Console.WriteLine();

Require(safeCandidate.IsFinite, "Candidate finite olmalı.");
Require(safeCandidate.VehicleId == context.VehicleId, "Candidate vehicle id context ile aynı olmalı.");
Require(safeCandidate.SourceKind == VehicleStateSourceKind.CSharpFusion, "Candidate source CSharpFusion olmalı.");
Require(safeCandidate.InputSampleIds.Count == 2, "Candidate iki input sample id taşımalı.");
Require(Math.Abs(safeCandidate.Pose.X - 12.5) < 0.001, "Candidate Pose X GPS'ten gelmeli.");
Require(Math.Abs(safeCandidate.Pose.Y - -4.25) < 0.001, "Candidate Pose Y GPS'ten gelmeli.");
Require(Math.Abs(safeCandidate.Pose.YawDeg - 47.0) < 0.001, "Candidate yaw IMU'dan gelmeli.");

var diagnostics = estimator.LastDiagnostics.Sanitized();

Console.WriteLine("[4] GpsImuStateEstimator Fusion diagnostics");
PrintDiagnostics(diagnostics);
Console.WriteLine();

Require(diagnostics.InputSampleCount == 2, "Diagnostics input sample count 2 olmalı.");
Require(diagnostics.UsedSampleCount == 2, "Diagnostics used sample count 2 olmalı.");
Require(diagnostics.RejectedSampleCount == 0, "Diagnostics rejected sample count 0 olmalı.");
Require(diagnostics.ProducedCandidate, "Diagnostics produced candidate true olmalı.");

Console.WriteLine();
Console.WriteLine("=== CDSE Smoke Tests ===");
Console.WriteLine();

var cdse = new CapabilityDrivenStateEstimator();

var cdseFullSamples = new[]
{
    gpsSample,
    imuSample,
    depthSample
};

var cdseFull = cdse.Fuse(cdseFullSamples, context);

if (cdseFull is null)
{
    return Fail("CDSE GPS + IMU + Depth ile FusedState üretmedi.");
}

var safeCdseFull = cdseFull.Value.Sanitized();

Console.WriteLine("[5] CDSE GPS + IMU + Depth");
PrintFusedState(safeCdseFull);
Console.WriteLine();

Require(safeCdseFull.IsValid, "CDSE full state valid olmalı.");
Require(Math.Abs(safeCdseFull.Pose.X - 12.5) < 0.001, "CDSE full Pose X GPS'ten gelmeli.");
Require(Math.Abs(safeCdseFull.Pose.Y - -4.25) < 0.001, "CDSE full Pose Y GPS'ten gelmeli.");
Require(Math.Abs(safeCdseFull.Pose.Z - -1.8) < 0.001, "CDSE full Pose Z depth'ten negatif derinlik olarak gelmeli.");
Require(Math.Abs(safeCdseFull.Pose.YawDeg - 47.0) < 0.001, "CDSE full yaw IMU'dan gelmeli.");
Require(safeCdseFull.Quality.Confidence >= 0.85, "CDSE full confidence yüksek olmalı.");
Require(safeCdseFull.Quality.Summary.Contains("gps=ok"), "CDSE full summary gps=ok içermeli.");
Require(safeCdseFull.Quality.Summary.Contains("imu=ok"), "CDSE full summary imu=ok içermeli.");
Require(safeCdseFull.Quality.Summary.Contains("depth=ok"), "CDSE full summary depth=ok içermeli.");

var later = now.AddMilliseconds(100.0);
var imuLater = CreateImuSample(later);
var depthLater = CreateDepthSample(later, depthMeters: 1.95);

var cdseDegradedSamples = new[]
{
    imuLater,
    depthLater
};

var cdseDegraded = cdse.Fuse(cdseDegradedSamples, context);

if (cdseDegraded is null)
{
    return Fail("CDSE GPS yokken IMU + Depth ile degraded FusedState üretmedi.");
}

var safeCdseDegraded = cdseDegraded.Value.Sanitized();

Console.WriteLine("[6] CDSE degraded IMU + Depth, GPS missing");
PrintFusedState(safeCdseDegraded);
Console.WriteLine();

Require(safeCdseDegraded.IsValid, "CDSE degraded state valid olmalı.");
Require(Math.Abs(safeCdseDegraded.Pose.X - 12.5) < 0.001, "CDSE degraded X son tahminden korunmalı.");
Require(Math.Abs(safeCdseDegraded.Pose.Y - -4.25) < 0.001, "CDSE degraded Y son tahminden korunmalı.");
Require(Math.Abs(safeCdseDegraded.Pose.Z - -1.95) < 0.001, "CDSE degraded Z depth'ten güncellenmeli.");
Require(Math.Abs(safeCdseDegraded.Pose.YawDeg - 47.0) < 0.001, "CDSE degraded yaw IMU'dan gelmeli.");
Require(safeCdseDegraded.Quality.Confidence >= 0.35, "CDSE degraded confidence görev sürdürmeye yetecek kadar olmalı.");
Require(safeCdseDegraded.Quality.Confidence <= 0.50, "CDSE degraded confidence GPS yokken sınırlı kalmalı.");
Require(safeCdseDegraded.Quality.Summary.Contains("gps=missing"), "CDSE degraded summary gps=missing içermeli.");
Require(safeCdseDegraded.Quality.Summary.Contains("mode=degraded_estimated"), "CDSE degraded summary degraded mode içermeli.");

var cdseDepthOnly = new CapabilityDrivenStateEstimator();
var depthOnlySample = CreateDepthSample(now.AddMilliseconds(200.0), depthMeters: 2.4);

var cdseDepthOnlyResult = cdseDepthOnly.Fuse(
    new[] { depthOnlySample },
    context
);

if (cdseDepthOnlyResult is null)
{
    return Fail("CDSE sadece Depth ile düşük güvenli FusedState üretmedi.");
}

var safeCdseDepthOnly = cdseDepthOnlyResult.Value.Sanitized();

Console.WriteLine("[7] CDSE Depth only");
PrintFusedState(safeCdseDepthOnly);
Console.WriteLine();

Require(safeCdseDepthOnly.IsValid, "CDSE depth-only state valid olmalı.");
Require(Math.Abs(safeCdseDepthOnly.Pose.X) < 0.001, "CDSE depth-only X origin/placeholder kalmalı.");
Require(Math.Abs(safeCdseDepthOnly.Pose.Y) < 0.001, "CDSE depth-only Y origin/placeholder kalmalı.");
Require(Math.Abs(safeCdseDepthOnly.Pose.Z - -2.4) < 0.001, "CDSE depth-only Z depth'ten gelmeli.");
Require(safeCdseDepthOnly.Quality.Confidence > 0.0, "CDSE depth-only confidence sıfırdan büyük olmalı.");
Require(safeCdseDepthOnly.Quality.Confidence <= 0.25, "CDSE depth-only confidence düşük kalmalı.");
Require(safeCdseDepthOnly.Quality.Summary.Contains("gps=missing"), "CDSE depth-only summary gps=missing içermeli.");

var cdseNoUsable = new CapabilityDrivenStateEstimator();
var noUsableResult = cdseNoUsable.Fuse(
    Array.Empty<SensorSample>(),
    context
);

Console.WriteLine("[8] CDSE no usable samples");
Console.WriteLine(noUsableResult is null
    ? "PASS: CDSE hiç veri yokken state uydurmadı."
    : "FAIL: CDSE hiç veri yokken state üretmemeliydi.");

Require(noUsableResult is null, "CDSE hiç usable sample yokken null dönmeli.");

var cdseCandidate = cdse.Estimate(cdseFullSamples, context);

if (cdseCandidate is null)
{
    return Fail("CDSE StateUpdateCandidate üretmedi.");
}

var safeCdseCandidate = cdseCandidate.Value.Sanitized();

Console.WriteLine("[9] CDSE StateUpdateCandidate");
PrintCandidate(safeCdseCandidate);
Console.WriteLine();

Require(safeCdseCandidate.IsFinite, "CDSE candidate finite olmalı.");
Require(safeCdseCandidate.VehicleId == context.VehicleId, "CDSE candidate vehicle id context ile aynı olmalı.");
Require(safeCdseCandidate.SourceKind == VehicleStateSourceKind.CSharpFusion, "CDSE candidate source CSharpFusion olmalı.");
Require(safeCdseCandidate.InputSampleIds.Count == 3, "CDSE candidate üç input sample id taşımalı.");
Require(safeCdseCandidate.Confidence >= 0.85, "CDSE candidate confidence yüksek olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine();
Console.WriteLine("PASS: GPS+IMU estimator ve CDSE smoke testleri başarıyla tamamlandı.");
Console.ResetColor();

return 0;

static SensorSample CreateGpsSample(DateTime timestampUtc)
{
    var identity = SensorIdentity.Create(
        sensorId: "gps0",
        sourceId: "sim_gps",
        dataKind: SensorDataKind.Gps,
        frameId: "gps_link",
        displayName: "Smoke GPS"
    );

    var source = SensorSourceInfo.Sim("sim_gps");

    var data = new GpsSampleData(
        Latitude: 41.0,
        Longitude: 29.0,
        AltitudeMeters: 1.2,
        X: 12.5,
        Y: -4.25,
        Z: 1.2,
        SpeedMps: 2.2360679,
        CourseDeg: 26.565,
        Hdop: 0.9,
        FixType: 3,
        Satellites: 14
    ).Sanitized();

    var timing = SensorTiming.FromCapture(
        captureUtc: timestampUtc,
        receiveUtc: timestampUtc,
        publishUtc: timestampUtc,
        targetRateHz: 5.0,
        effectiveRateHz: 5.0
    );

    var quality = SensorQuality
        .Good(
            backendKind: SensorBackendKind.Sim,
            backendName: "sim_gps",
            simulated: true,
            confidence: 0.95
        )
        .WithTiming(
            ageMs: timing.CaptureAgeMs,
            latencyMs: timing.ReceiveToPublishMs,
            targetRateHz: 5.0,
            effectiveRateHz: 5.0
        );

    return SensorSample.Create(
        sensor: identity,
        source: source,
        sequence: 1,
        dataKind: SensorDataKind.Gps,
        data: data,
        quality: quality,
        timing: timing,
        calibrationId: "fusion_smoke_gps",
        traceId: "fusion-smoke-gps"
    );
}

static SensorSample CreateImuSample(DateTime timestampUtc)
{
    var identity = SensorIdentity.Create(
        sensorId: "imu0",
        sourceId: "sim_imu",
        dataKind: SensorDataKind.Imu,
        frameId: "imu_link",
        displayName: "Smoke IMU"
    );

    var source = SensorSourceInfo.Sim("sim_imu");

    var data = new ImuSampleData(
        Ax: 0.3,
        Ay: -0.1,
        Az: 9.85,
        GxRadSec: DegToRad(1.5),
        GyRadSec: DegToRad(-0.5),
        GzRadSec: DegToRad(12.0),
        Mx: null,
        My: null,
        Mz: null,
        RollDeg: 3.0,
        PitchDeg: -2.0,
        YawDeg: 47.0,
        TemperatureC: 25.0
    ).Sanitized();

    var timing = SensorTiming.FromCapture(
        captureUtc: timestampUtc,
        receiveUtc: timestampUtc,
        publishUtc: timestampUtc,
        targetRateHz: 20.0,
        effectiveRateHz: 20.0
    );

    var quality = SensorQuality
        .Good(
            backendKind: SensorBackendKind.Sim,
            backendName: "sim_imu",
            simulated: true,
            confidence: 0.95
        )
        .WithTiming(
            ageMs: timing.CaptureAgeMs,
            latencyMs: timing.ReceiveToPublishMs,
            targetRateHz: 20.0,
            effectiveRateHz: 20.0
        );

    return SensorSample.Create(
        sensor: identity,
        source: source,
        sequence: 1,
        dataKind: SensorDataKind.Imu,
        data: data,
        quality: quality,
        timing: timing,
        calibrationId: "fusion_smoke_imu",
        traceId: "fusion-smoke-imu"
    );
}

static SensorSample CreateDepthSample(DateTime timestampUtc, double depthMeters = 1.8)
{
    var identity = SensorIdentity.Create(
        sensorId: "depth0",
        sourceId: "sim_depth",
        dataKind: SensorDataKind.Depth,
        frameId: "depth_link",
        displayName: "Smoke Depth"
    );

    var source = SensorSourceInfo.Sim("sim_depth");

    var data = new DepthSampleData(
        DepthMeters: depthMeters,
        PressureKPa: 101.3 + depthMeters * 9.81,
        AltitudeMeters: null,
        TemperatureC: 22.0,
        Valid: true
    ).Sanitized();

    var timing = SensorTiming.FromCapture(
        captureUtc: timestampUtc,
        receiveUtc: timestampUtc,
        publishUtc: timestampUtc,
        targetRateHz: 10.0,
        effectiveRateHz: 10.0
    );

    var quality = SensorQuality
        .Good(
            backendKind: SensorBackendKind.Sim,
            backendName: "sim_depth",
            simulated: true,
            confidence: 0.95
        )
        .WithTiming(
            ageMs: timing.CaptureAgeMs,
            latencyMs: timing.ReceiveToPublishMs,
            targetRateHz: 10.0,
            effectiveRateHz: 10.0
        );

    return SensorSample.Create(
        sensor: identity,
        source: source,
        sequence: 1,
        dataKind: SensorDataKind.Depth,
        data: data,
        quality: quality,
        timing: timing,
        calibrationId: "fusion_smoke_depth",
        traceId: "fusion-smoke-depth"
    );
}

static void PrintSample(SensorSample sample)
{
    Console.WriteLine(
        $"- {sample.Sensor.SensorId} | source={sample.Sensor.SourceId} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}"
    );
}

static void PrintFusedState(FusedState state)
{
    Console.WriteLine($"VehicleId  : {state.VehicleId}");
    Console.WriteLine($"Timestamp  : {state.TimestampUtc:O}");
    Console.WriteLine($"Pose       : X={state.Pose.X:F3}, Y={state.Pose.Y:F3}, Z={state.Pose.Z:F3}, Yaw={state.Pose.YawDeg:F3}");
    Console.WriteLine($"Twist      : Vx={state.Twist.Vx:F3}, Vy={state.Twist.Vy:F3}, Vz={state.Twist.Vz:F3}, Speed={state.Twist.SpeedMps:F3}, YawRate={state.Twist.YawRateDegSec:F3}");
    Console.WriteLine($"Attitude   : R={state.Attitude.RollDeg:F3}, P={state.Attitude.PitchDeg:F3}, Y={state.Attitude.YawDeg:F3}");
    Console.WriteLine($"Confidence : {state.Quality.Confidence:F3}");
    Console.WriteLine($"Summary    : {state.Quality.Summary}");
}

static void PrintCandidate(StateUpdateCandidate candidate)
{
    Console.WriteLine($"CandidateId : {candidate.CandidateId}");
    Console.WriteLine($"VehicleId   : {candidate.VehicleId}");
    Console.WriteLine($"SourceKind  : {candidate.SourceKind}");
    Console.WriteLine($"Confidence  : {candidate.Confidence:F3}");
    Console.WriteLine($"Pose        : X={candidate.Pose.X:F3}, Y={candidate.Pose.Y:F3}, Z={candidate.Pose.Z:F3}, Yaw={candidate.Pose.YawDeg:F3}");
    Console.WriteLine($"Inputs      : {string.Join(", ", candidate.InputSampleIds)}");
    Console.WriteLine($"Reason      : {candidate.Reason}");
}

static void PrintDiagnostics(Hydronom.Core.Fusion.Diagnostics.FusionDiagnostics diagnostics)
{
    Console.WriteLine($"Engine             : {diagnostics.FusionEngineName}");
    Console.WriteLine($"Input sample count : {diagnostics.InputSampleCount}");
    Console.WriteLine($"Used sample count  : {diagnostics.UsedSampleCount}");
    Console.WriteLine($"Rejected count     : {diagnostics.RejectedSampleCount}");
    Console.WriteLine($"Produced candidate : {diagnostics.ProducedCandidate}");
    Console.WriteLine($"Confidence         : {diagnostics.Confidence:F3}");
    Console.WriteLine($"Summary            : {diagnostics.Summary}");
}

static void Require(bool condition, string message)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {message}");
        return;
    }

    throw new InvalidOperationException(message);
}

static int Fail(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();
    return 1;
}

static double DegToRad(double deg)
{
    return deg * Math.PI / 180.0;
}