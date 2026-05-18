using System.Text;
using System.Text.Json;
using Hydronom.Core.Communication.Telemetry;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE COMPACT TELEMETRY SMOKE TEST");
Console.WriteLine("=================================================");

var codec = new CompactTelemetryCodec();

Console.WriteLine();
Console.WriteLine("[1] Full telemetry encode/decode testi");

var fullFrame = new CompactTelemetryFrame
{
    VehicleId = "hydronom-main",
    Sequence = 42,
    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    FieldMask = CompactTelemetryField.All,

    PositionXM = 12.345,
    PositionYM = -8.421,
    PositionZM = -0.153,

    RollRad = 0.012,
    PitchRad = -0.024,
    YawRad = 1.571,

    VelocityXMps = 0.42,
    VelocityYMps = -0.08,
    VelocityZMps = 0.01,

    AngularVelocityXRadps = 0.001,
    AngularVelocityYRadps = -0.002,
    AngularVelocityZRadps = 0.031,

    SpeedMps = 0.43,
    HeadingErrorRad = -0.128,
    DistanceToTargetM = 17.86,

    BatteryVoltageV = 15.82,
    BatteryPercent = 73.4,

    MissionProgress01 = 0.37,
    RiskScore01 = 0.12,

    ForceXN = 8.7,
    ForceYN = -1.2,
    ForceZN = 0.4,

    TorqueXNm = 0.12,
    TorqueYNm = -0.08,
    TorqueZNm = 0.31
};

var fullBytes = codec.Encode(fullFrame);
var fullDecoded = codec.Decode(fullBytes);

Require(fullBytes.Length > 0, "Full telemetry byte üretti.");
Require(fullDecoded.VehicleId == fullFrame.VehicleId, "VehicleId korundu.");
Require(fullDecoded.Sequence == fullFrame.Sequence, "Sequence korundu.");
Require(fullDecoded.FieldMask == fullFrame.FieldMask, "Field mask korundu.");

RequireClose(fullDecoded.PositionXM, 12.35, 0.001, "PositionX cm hassasiyetinde çözüldü.");
RequireClose(fullDecoded.PositionYM, -8.42, 0.001, "PositionY cm hassasiyetinde çözüldü.");
RequireClose(fullDecoded.PositionZM, -0.15, 0.001, "PositionZ cm hassasiyetinde çözüldü.");

RequireClose(fullDecoded.YawRad, 1.571, 0.0001, "Yaw millirad hassasiyetinde çözüldü.");
RequireClose(fullDecoded.SpeedMps, 0.43, 0.0001, "Speed cm/s hassasiyetinde çözüldü.");
RequireClose(fullDecoded.DistanceToTargetM, 17.86, 0.001, "Distance cm hassasiyetinde çözüldü.");

RequireClose(fullDecoded.BatteryVoltageV, 15.82, 0.001, "Battery voltage centivolt hassasiyetinde çözüldü.");
RequireClose(fullDecoded.BatteryPercent, 73.4, 0.001, "Battery percent permille hassasiyetinde çözüldü.");

RequireClose(fullDecoded.MissionProgress01, 0.37, 0.001, "Mission progress çözüldü.");
RequireClose(fullDecoded.RiskScore01, 0.12, 0.001, "Risk score çözüldü.");

RequireClose(fullDecoded.ForceXN, 8.7, 0.001, "ForceX decinewton hassasiyetinde çözüldü.");
RequireClose(fullDecoded.TorqueZNm, 0.31, 0.001, "TorqueZ centinewton-meter hassasiyetinde çözüldü.");

Console.WriteLine();
Console.WriteLine("[2] Pose-only field mask testi");

var poseFrame = fullFrame with
{
    Sequence = 43,
    FieldMask = CompactTelemetryField.AllPose
};

var poseBytes = codec.Encode(poseFrame);
var poseDecoded = codec.Decode(poseBytes);

Require(poseDecoded.Has(CompactTelemetryField.PositionX), "Pose frame PositionX içeriyor.");
Require(poseDecoded.Has(CompactTelemetryField.Yaw), "Pose frame Yaw içeriyor.");
Require(!poseDecoded.Has(CompactTelemetryField.Speed), "Pose frame Speed içermiyor.");
Require(!poseDecoded.Has(CompactTelemetryField.BatteryVoltage), "Pose frame BatteryVoltage içermiyor.");

RequireClose(poseDecoded.PositionXM, 12.35, 0.001, "Pose PositionX doğru.");
RequireClose(poseDecoded.YawRad, 1.571, 0.0001, "Pose Yaw doğru.");
RequireClose(poseDecoded.SpeedMps, 0.0, 0.0001, "Mask dışı Speed default kaldı.");

Console.WriteLine();
Console.WriteLine("[3] Control-only field mask testi");

var controlFrame = fullFrame with
{
    Sequence = 44,
    FieldMask = CompactTelemetryField.AllControl
};

var controlBytes = codec.Encode(controlFrame);
var controlDecoded = codec.Decode(controlBytes);

Require(controlDecoded.Has(CompactTelemetryField.Speed), "Control frame Speed içeriyor.");
Require(controlDecoded.Has(CompactTelemetryField.HeadingError), "Control frame HeadingError içeriyor.");
Require(controlDecoded.Has(CompactTelemetryField.DistanceToTarget), "Control frame DistanceToTarget içeriyor.");
Require(!controlDecoded.Has(CompactTelemetryField.PositionX), "Control frame PositionX içermiyor.");

RequireClose(controlDecoded.SpeedMps, 0.43, 0.0001, "Control Speed doğru.");
RequireClose(controlDecoded.HeadingErrorRad, -0.128, 0.0001, "Control HeadingError doğru.");
RequireClose(controlDecoded.DistanceToTargetM, 17.86, 0.001, "Control Distance doğru.");

Console.WriteLine();
Console.WriteLine("[4] Power + mission field mask testi");

var powerMissionFrame = fullFrame with
{
    Sequence = 45,
    FieldMask =
        CompactTelemetryField.AllPower |
        CompactTelemetryField.AllMission
};

var powerMissionBytes = codec.Encode(powerMissionFrame);
var powerMissionDecoded = codec.Decode(powerMissionBytes);

Require(powerMissionDecoded.Has(CompactTelemetryField.BatteryVoltage), "PowerMission BatteryVoltage içeriyor.");
Require(powerMissionDecoded.Has(CompactTelemetryField.BatteryPercent), "PowerMission BatteryPercent içeriyor.");
Require(powerMissionDecoded.Has(CompactTelemetryField.MissionProgress), "PowerMission MissionProgress içeriyor.");
Require(powerMissionDecoded.Has(CompactTelemetryField.RiskScore), "PowerMission RiskScore içeriyor.");
Require(!powerMissionDecoded.Has(CompactTelemetryField.Yaw), "PowerMission Yaw içermiyor.");

RequireClose(powerMissionDecoded.BatteryVoltageV, 15.82, 0.001, "PowerMission voltage doğru.");
RequireClose(powerMissionDecoded.BatteryPercent, 73.4, 0.001, "PowerMission percent doğru.");
RequireClose(powerMissionDecoded.MissionProgress01, 0.37, 0.001, "PowerMission progress doğru.");
RequireClose(powerMissionDecoded.RiskScore01, 0.12, 0.001, "PowerMission risk doğru.");

Console.WriteLine();
Console.WriteLine("[5] Boyut karşılaştırması");

var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(fullFrame);

Console.WriteLine($"Full compact telemetry       : {fullBytes.Length} byte");
Console.WriteLine($"Pose-only compact telemetry  : {poseBytes.Length} byte");
Console.WriteLine($"Control-only compact telemetry: {controlBytes.Length} byte");
Console.WriteLine($"Power+mission compact        : {powerMissionBytes.Length} byte");
Console.WriteLine($"Full JSON telemetry          : {jsonBytes.Length} byte");

Require(fullBytes.Length < jsonBytes.Length, "Full compact telemetry JSON'dan küçük.");
Require(poseBytes.Length < fullBytes.Length, "Pose-only full telemetry'den küçük.");
Require(controlBytes.Length < fullBytes.Length, "Control-only full telemetry'den küçük.");
Require(powerMissionBytes.Length < fullBytes.Length, "Power+mission full telemetry'den küçük.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" COMPACT TELEMETRY SMOKE TEST PASSED ✅");
Console.WriteLine("=================================================");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();

        throw new InvalidOperationException(message);
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[OK] {message}");
    Console.ResetColor();
}

static void RequireClose(
    double actual,
    double expected,
    double tolerance,
    string message)
{
    var diff = Math.Abs(actual - expected);

    if (diff > tolerance)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message} actual={actual} expected={expected} diff={diff}");
        Console.ResetColor();

        throw new InvalidOperationException(message);
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[OK] {message} actual={actual:0.###}");
    Console.ResetColor();
}