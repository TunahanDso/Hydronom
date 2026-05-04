using Hydronom.Core.Fusion.Estimation;
using Hydronom.Core.Fusion.Models;
using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Common.Quality;
using Hydronom.Core.Sensors.Common.Timing;
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

Console.WriteLine("[2] FusedState");
Console.WriteLine($"VehicleId  : {safeFused.VehicleId}");
Console.WriteLine($"Timestamp  : {safeFused.TimestampUtc:O}");
Console.WriteLine($"Pose       : X={safeFused.Pose.X:F3}, Y={safeFused.Pose.Y:F3}, Z={safeFused.Pose.Z:F3}, Yaw={safeFused.Pose.YawDeg:F3}");
Console.WriteLine($"Twist      : Vx={safeFused.Twist.Vx:F3}, Vy={safeFused.Twist.Vy:F3}, Speed={safeFused.Twist.SpeedMps:F3}, YawRate={safeFused.Twist.YawRateDegSec:F3}");
Console.WriteLine($"Attitude   : R={safeFused.Attitude.RollDeg:F3}, P={safeFused.Attitude.PitchDeg:F3}, Y={safeFused.Attitude.YawDeg:F3}");
Console.WriteLine($"Confidence : {safeFused.Quality.Confidence:F3}");
Console.WriteLine($"Summary    : {safeFused.Quality.Summary}");
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

Console.WriteLine("[3] StateUpdateCandidate");
Console.WriteLine($"CandidateId : {safeCandidate.CandidateId}");
Console.WriteLine($"VehicleId   : {safeCandidate.VehicleId}");
Console.WriteLine($"SourceKind  : {safeCandidate.SourceKind}");
Console.WriteLine($"Confidence  : {safeCandidate.Confidence:F3}");
Console.WriteLine($"Pose        : X={safeCandidate.Pose.X:F3}, Y={safeCandidate.Pose.Y:F3}, Z={safeCandidate.Pose.Z:F3}, Yaw={safeCandidate.Pose.YawDeg:F3}");
Console.WriteLine($"Inputs      : {string.Join(", ", safeCandidate.InputSampleIds)}");
Console.WriteLine($"Reason      : {safeCandidate.Reason}");
Console.WriteLine();

Require(safeCandidate.IsFinite, "Candidate finite olmalı.");
Require(safeCandidate.VehicleId == context.VehicleId, "Candidate vehicle id context ile aynı olmalı.");
Require(safeCandidate.SourceKind == VehicleStateSourceKind.CSharpFusion, "Candidate source CSharpFusion olmalı.");
Require(safeCandidate.InputSampleIds.Count == 2, "Candidate iki input sample id taşımalı.");
Require(Math.Abs(safeCandidate.Pose.X - 12.5) < 0.001, "Candidate Pose X GPS'ten gelmeli.");
Require(Math.Abs(safeCandidate.Pose.Y - -4.25) < 0.001, "Candidate Pose Y GPS'ten gelmeli.");
Require(Math.Abs(safeCandidate.Pose.YawDeg - 47.0) < 0.001, "Candidate yaw IMU'dan gelmeli.");

var diagnostics = estimator.LastDiagnostics.Sanitized();

Console.WriteLine("[4] Fusion diagnostics");
Console.WriteLine($"Engine             : {diagnostics.FusionEngineName}");
Console.WriteLine($"Input sample count : {diagnostics.InputSampleCount}");
Console.WriteLine($"Used sample count  : {diagnostics.UsedSampleCount}");
Console.WriteLine($"Rejected count     : {diagnostics.RejectedSampleCount}");
Console.WriteLine($"Produced candidate : {diagnostics.ProducedCandidate}");
Console.WriteLine($"Confidence         : {diagnostics.Confidence:F3}");
Console.WriteLine($"Summary            : {diagnostics.Summary}");
Console.WriteLine();

Require(diagnostics.InputSampleCount == 2, "Diagnostics input sample count 2 olmalı.");
Require(diagnostics.UsedSampleCount == 2, "Diagnostics used sample count 2 olmalı.");
Require(diagnostics.RejectedSampleCount == 0, "Diagnostics rejected sample count 0 olmalı.");
Require(diagnostics.ProducedCandidate, "Diagnostics produced candidate true olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: GPS + IMU fusion estimator FusedState ve StateUpdateCandidate üretti.");
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

static void PrintSample(SensorSample sample)
{
    Console.WriteLine(
        $"- {sample.Sensor.SensorId} | source={sample.Sensor.SourceId} | kind={sample.DataKind} | valid={sample.IsValid} | seq={sample.Sequence}"
    );
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