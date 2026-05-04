using Hydronom.Core.State.Authority;
using Hydronom.Core.State.Models;
using Hydronom.Runtime.StateRuntime;

Console.WriteLine("=== Hydronom Runtime Pipeline Smoke Test ===");
Console.WriteLine();

var vehicleId = "PIPELINE-SMOKE-VEHICLE-001";

var policy = StateAuthorityPolicy.CSharpPrimary with
{
    MaxStateAgeMs = 1_000.0,
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

var initial = store.Current;

Console.WriteLine("[1] Initial state");
PrintState(initial);
Console.WriteLine();

Require(initial.VehicleId == vehicleId, "Initial state vehicle id doğru olmalı.");
Require(initial.FrameId == "map", "Initial frame map olmalı.");
Require(store.AcceptedUpdateCount == 0, "Başlangıç accepted count 0 olmalı.");
Require(store.RejectedUpdateCount == 0, "Başlangıç rejected count 0 olmalı.");

var validCandidateTime = initial.TimestampUtc.AddMilliseconds(500);

var validCandidate = CreateCandidate(
    vehicleId: vehicleId,
    timestampUtc: validCandidateTime,
    x: 5.0,
    y: 2.0,
    z: 0.0,
    yawDeg: 15.0,
    confidence: 0.95,
    reason: "VALID_FUSION_CANDIDATE"
);

var acceptedResult = pipeline.Submit(validCandidate, validCandidateTime.AddMilliseconds(50));

Console.WriteLine("[2] Valid candidate result");
PrintResult(acceptedResult);
Console.WriteLine();

Require(acceptedResult.Accepted, "Valid candidate kabul edilmeli.");
Require(acceptedResult.Decision == StateUpdateDecision.Accepted, "Decision Accepted olmalı.");
Require(store.AcceptedUpdateCount == 1, "Accepted update count 1 olmalı.");
Require(store.RejectedUpdateCount == 0, "Rejected update count 0 kalmalı.");

var afterAccept = store.Current;

Console.WriteLine("[3] Store after accepted candidate");
PrintState(afterAccept);
Console.WriteLine();

Require(Math.Abs(afterAccept.Pose.X - 5.0) < 0.001, "Store Pose X accepted candidate ile güncellenmeli.");
Require(Math.Abs(afterAccept.Pose.Y - 2.0) < 0.001, "Store Pose Y accepted candidate ile güncellenmeli.");
Require(Math.Abs(afterAccept.Pose.YawDeg - 15.0) < 0.001, "Store yaw accepted candidate ile güncellenmeli.");
Require(afterAccept.SourceKind == VehicleStateSourceKind.CSharpFusion, "Store source CSharpFusion olmalı.");
Require(afterAccept.Confidence >= 0.90, "Store confidence yüksek olmalı.");

var teleportCandidateTime = afterAccept.TimestampUtc.AddMilliseconds(500);

var teleportCandidate = CreateCandidate(
    vehicleId: vehicleId,
    timestampUtc: teleportCandidateTime,
    x: 1_000.0,
    y: 1_000.0,
    z: 0.0,
    yawDeg: 20.0,
    confidence: 0.95,
    reason: "TELEPORT_CANDIDATE"
);

var rejectedResult = pipeline.Submit(teleportCandidate, teleportCandidateTime.AddMilliseconds(50));

Console.WriteLine("[4] Teleport candidate result");
PrintResult(rejectedResult);
Console.WriteLine();

Require(!rejectedResult.Accepted, "Teleport candidate reddedilmeli.");
Require(
    rejectedResult.Decision == StateUpdateDecision.RejectedTeleportDetected ||
    rejectedResult.Decision == StateUpdateDecision.RejectedPhysicallyImpossible,
    "Teleport candidate teleport veya physically impossible nedeniyle reddedilmeli."
);

Require(store.AcceptedUpdateCount == 1, "Accepted update count 1 kalmalı.");
Require(store.RejectedUpdateCount == 1, "Rejected update count 1 olmalı.");

var afterReject = store.Current;

Console.WriteLine("[5] Store after rejected candidate");
PrintState(afterReject);
Console.WriteLine();

Require(Math.Abs(afterReject.Pose.X - afterAccept.Pose.X) < 0.001, "Rejected update sonrası store X korunmalı.");
Require(Math.Abs(afterReject.Pose.Y - afterAccept.Pose.Y) < 0.001, "Rejected update sonrası store Y korunmalı.");
Require(Math.Abs(afterReject.Pose.YawDeg - afterAccept.Pose.YawDeg) < 0.001, "Rejected update sonrası store yaw korunmalı.");

var snapshot = store.GetSnapshot();

Console.WriteLine("[6] Store snapshot");
Console.WriteLine($"VehicleId       : {snapshot.VehicleId}");
Console.WriteLine($"Has state       : {snapshot.HasState}");
Console.WriteLine($"Accepted count  : {snapshot.AcceptedUpdateCount}");
Console.WriteLine($"Rejected count  : {snapshot.RejectedUpdateCount}");
Console.WriteLine($"Recent results  : {snapshot.RecentResults.Count}");
Console.WriteLine($"Summary         : {snapshot.Summary}");
Console.WriteLine();

Require(snapshot.HasState, "Snapshot has state true olmalı.");
Require(snapshot.AcceptedUpdateCount == 1, "Snapshot accepted count 1 olmalı.");
Require(snapshot.RejectedUpdateCount == 1, "Snapshot rejected count 1 olmalı.");
Require(snapshot.RecentResults.Count == 2, "Snapshot recent results 2 olmalı.");

var telemetry = telemetryBridge.Project(store);

Console.WriteLine("[7] State authority telemetry");
Console.WriteLine($"VehicleId          : {telemetry.VehicleId}");
Console.WriteLine($"Has state          : {telemetry.HasState}");
Console.WriteLine($"Last accepted      : {telemetry.LastUpdateAccepted}");
Console.WriteLine($"Last decision      : {telemetry.LastDecision}");
Console.WriteLine($"Accepted count     : {telemetry.AcceptedUpdateCount}");
Console.WriteLine($"Rejected count     : {telemetry.RejectedUpdateCount}");
Console.WriteLine($"Summary            : {telemetry.Summary}");
Console.WriteLine();

Require(telemetry.HasState, "Telemetry has state true olmalı.");
Require(!telemetry.LastUpdateAccepted, "Telemetry son update rejected göstermeli.");
Require(telemetry.RejectedUpdateCount == 1, "Telemetry rejected count 1 olmalı.");
Require(telemetry.AcceptedUpdateCount == 1, "Telemetry accepted count 1 olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: StateAuthority + VehicleStateStore pipeline valid update'i kabul etti, teleport update'i reddetti.");
Console.ResetColor();

return 0;

static StateUpdateCandidate CreateCandidate(
    string vehicleId,
    DateTime timestampUtc,
    double x,
    double y,
    double z,
    double yawDeg,
    double confidence,
    string reason)
{
    var pose = new VehiclePose(
        X: x,
        Y: y,
        Z: z,
        YawDeg: yawDeg,
        FrameId: "map"
    );

    var twist = new VehicleTwist(
        Vx: 2.0,
        Vy: 0.0,
        Vz: 0.0,
        YawRateDegSec: 10.0
    );

    var attitude = new VehicleAttitude(
        RollDeg: 0.0,
        PitchDeg: 0.0,
        YawDeg: yawDeg,
        RollRateDegSec: 0.0,
        PitchRateDegSec: 0.0,
        YawRateDegSec: 10.0
    );

    return new StateUpdateCandidate(
        CandidateId: Guid.NewGuid().ToString("N"),
        VehicleId: vehicleId,
        TimestampUtc: timestampUtc,
        Pose: pose,
        Twist: twist,
        Attitude: attitude,
        SourceKind: VehicleStateSourceKind.CSharpFusion,
        Confidence: confidence,
        FrameId: "map",
        Reason: reason,
        InputSampleIds: new[] { "fusion-input-001", "fusion-input-002" },
        TraceId: Guid.NewGuid().ToString("N")
    ).Sanitized();
}

static void PrintState(VehicleOperationalState state)
{
    Console.WriteLine($"VehicleId  : {state.VehicleId}");
    Console.WriteLine($"Timestamp  : {state.TimestampUtc:O}");
    Console.WriteLine($"Pose       : X={state.Pose.X:F3}, Y={state.Pose.Y:F3}, Z={state.Pose.Z:F3}, Yaw={state.Pose.YawDeg:F3}");
    Console.WriteLine($"Source     : {state.SourceKind}");
    Console.WriteLine($"Confidence : {state.Confidence:F3}");
    Console.WriteLine($"Frame      : {state.FrameId}");
}

static void PrintResult(StateUpdateResult result)
{
    Console.WriteLine($"Accepted       : {result.Accepted}");
    Console.WriteLine($"Decision       : {result.Decision}");
    Console.WriteLine($"Reason         : {result.Reason}");
    Console.WriteLine($"Position delta : {result.PositionDeltaMeters:F3} m");
    Console.WriteLine($"Implied speed  : {result.ImpliedSpeedMps:F3} m/s");
    Console.WriteLine($"Yaw delta      : {result.YawDeltaDeg:F3} deg");
    Console.WriteLine($"Yaw rate       : {result.ImpliedYawRateDegSec:F3} deg/s");
}

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