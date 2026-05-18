using System.Text;
using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("====================================================");
Console.WriteLine(" HYDRONOM CORE TELEMETRY ENVELOPE SMOKE TEST");
Console.WriteLine("====================================================");

var adapter = new CompactTelemetryEnvelopeAdapter();

var fullFrame = CreateFrame(
    sequence: 500,
    fieldMask: CompactTelemetryField.All,
    x: 12.35,
    y: 8.42,
    yaw: 1.571,
    speed: 0.43,
    distance: 17.86,
    risk: 0.12);

Console.WriteLine();
Console.WriteLine("[1] Full frame → envelope snapshot testi");

var fullEnvelope = adapter.ToEnvelope(
    frame: fullFrame,
    sourceId: "runtime-main",
    targetId: "ground-station",
    priority: HydronomMessagePriority.High,
    extraFlags: HydronomMessageFlags.RequiresAck,
    sessionId: "session-envelope-001",
    correlationId: "corr-full-001");

Require(fullEnvelope.Type == HydronomMessageType.FusedState, "Envelope type FusedState.");
Require(fullEnvelope.Priority == HydronomMessagePriority.High, "Envelope priority High.");
Require(fullEnvelope.VehicleId == fullFrame.VehicleId, "Envelope VehicleId doğru.");
Require(fullEnvelope.Sequence == fullFrame.Sequence, "Envelope Sequence doğru.");
Require(fullEnvelope.SourceId == "runtime-main", "Envelope SourceId doğru.");
Require(fullEnvelope.TargetId == "ground-station", "Envelope TargetId doğru.");
Require(fullEnvelope.ContentType == CompactTelemetryEnvelopeAdapter.CompactTelemetryContentType, "Envelope ContentType compact telemetry.");
Require(fullEnvelope.Payload.Length > 0, "Envelope payload dolu.");
Require(fullEnvelope.Flags.HasFlag(HydronomMessageFlags.IsSnapshot), "Full frame IsSnapshot flag aldı.");
Require(!fullEnvelope.Flags.HasFlag(HydronomMessageFlags.IsDelta), "Full frame IsDelta flag almadı.");
Require(fullEnvelope.Flags.HasFlag(HydronomMessageFlags.RequiresAck), "Extra RequiresAck flag korundu.");

var fullDecoded = adapter.FromEnvelope(fullEnvelope);

Require(fullDecoded.VehicleId == fullFrame.VehicleId, "Envelope payload VehicleId doğru çözüldü.");
Require(fullDecoded.Sequence == fullFrame.Sequence, "Envelope payload Sequence doğru çözüldü.");
Require(fullDecoded.FieldMask == CompactTelemetryField.All, "Envelope payload FieldMask doğru çözüldü.");
RequireClose(fullDecoded.PositionXM, 12.35, 0.001, "Envelope payload PositionX doğru.");
RequireClose(fullDecoded.YawRad, 1.571, 0.0001, "Envelope payload Yaw doğru.");
RequireClose(fullDecoded.SpeedMps, 0.43, 0.0001, "Envelope payload Speed doğru.");

Console.WriteLine();
Console.WriteLine("[2] Delta frame → envelope delta flag testi");

var previousFrame = fullFrame;

var currentFrame = fullFrame with
{
    Sequence = 501,
    PositionXM = 12.37,
    YawRad = 1.574,
    SpeedMps = 0.47,
    DistanceToTargetM = 17.80,
    RiskScore01 = 0.13
};

var deltaBuilder = new CompactTelemetryDeltaBuilder();
var deltaEnvelope = adapter.BuildDeltaEnvelope(
    previous: previousFrame,
    current: currentFrame,
    deltaBuilder: deltaBuilder,
    sourceId: "runtime-main",
    targetId: "ground-station",
    priority: HydronomMessagePriority.High,
    extraFlags: HydronomMessageFlags.None,
    sessionId: "session-envelope-001",
    correlationId: "corr-delta-001");

Require(deltaEnvelope.Flags.HasFlag(HydronomMessageFlags.IsDelta), "Delta frame IsDelta flag aldı.");
Require(!deltaEnvelope.Flags.HasFlag(HydronomMessageFlags.IsSnapshot), "Delta frame IsSnapshot flag almadı.");
Require(deltaEnvelope.Sequence == currentFrame.Sequence, "Delta envelope sequence current frame ile aynı.");

var deltaDecoded = adapter.FromEnvelope(deltaEnvelope);

Require(deltaDecoded.Has(CompactTelemetryField.PositionX), "Delta payload PositionX içeriyor.");
Require(deltaDecoded.Has(CompactTelemetryField.Yaw), "Delta payload Yaw içeriyor.");
Require(deltaDecoded.Has(CompactTelemetryField.Speed), "Delta payload Speed içeriyor.");
Require(deltaDecoded.Has(CompactTelemetryField.DistanceToTarget), "Delta payload Distance içeriyor.");
Require(deltaDecoded.Has(CompactTelemetryField.RiskScore), "Delta payload Risk içeriyor.");
Require(!deltaDecoded.Has(CompactTelemetryField.PositionY), "Delta payload PositionY içermiyor.");

RequireClose(deltaDecoded.PositionXM, 12.37, 0.001, "Delta payload PositionX doğru.");
RequireClose(deltaDecoded.YawRad, 1.574, 0.0001, "Delta payload Yaw doğru.");
RequireClose(deltaDecoded.SpeedMps, 0.47, 0.0001, "Delta payload Speed doğru.");

Console.WriteLine();
Console.WriteLine("[3] VehicleId uyuşmazlığı yakalama testi");

var wrongVehicleEnvelope = fullEnvelope with
{
    VehicleId = "wrong-vehicle"
};

var vehicleMismatchCaught = false;

try
{
    _ = adapter.FromEnvelope(wrongVehicleEnvelope);
}
catch (InvalidDataException)
{
    vehicleMismatchCaught = true;
}

Require(vehicleMismatchCaught, "VehicleId uyuşmazlığı yakalandı.");

Console.WriteLine();
Console.WriteLine("[4] Sequence uyuşmazlığı yakalama testi");

var wrongSequenceEnvelope = fullEnvelope with
{
    Sequence = fullEnvelope.Sequence + 999
};

var sequenceMismatchCaught = false;

try
{
    _ = adapter.FromEnvelope(wrongSequenceEnvelope);
}
catch (InvalidDataException)
{
    sequenceMismatchCaught = true;
}

Require(sequenceMismatchCaught, "Sequence uyuşmazlığı yakalandı.");

Console.WriteLine();
Console.WriteLine("[5] Yanlış content type yakalama testi");

var wrongContentTypeEnvelope = fullEnvelope with
{
    ContentType = "application/json"
};

var contentTypeCaught = false;

try
{
    _ = adapter.FromEnvelope(wrongContentTypeEnvelope);
}
catch (InvalidDataException)
{
    contentTypeCaught = true;
}

Require(contentTypeCaught, "Yanlış ContentType yakalandı.");

Console.WriteLine();
Console.WriteLine("[6] Envelope + HMAC + Binary codec zincir testi");

var security = new HmacHydronomSecurityProvider(
    "hydronom-envelope-smoke-secret-key");

var protectedEnvelope = security.Protect(
    deltaEnvelope,
    HydronomSecurityProfile.Race);

Require(protectedEnvelope.SecurityTag is { Length: > 0 }, "Envelope HMAC tag aldı.");
Require(protectedEnvelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "Envelope IsSigned flag aldı.");

var binaryCodec = new BinaryHydronomCodec();
var encodedEnvelope = binaryCodec.Encode(protectedEnvelope);
var decodedEnvelope = binaryCodec.Decode(encodedEnvelope);

Require(encodedEnvelope.Bytes.Length > 0, "Protected envelope binary encode edildi.");
Require(decodedEnvelope.SecurityTag is { Length: > 0 }, "Binary decode sonrası SecurityTag korundu.");
Require(decodedEnvelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "Binary decode sonrası IsSigned korundu.");
Require(decodedEnvelope.Flags.HasFlag(HydronomMessageFlags.IsDelta), "Binary decode sonrası IsDelta korundu.");

var verifyResult = security.Verify(
    decodedEnvelope,
    HydronomSecurityProfile.Race);

Require(verifyResult.Accepted, "Binary decode sonrası HMAC doğrulaması kabul edildi.");

var decodedTelemetry = adapter.FromEnvelope(decodedEnvelope);

Require(decodedTelemetry.Has(CompactTelemetryField.PositionX), "HMAC+Binary sonrası telemetry PositionX içeriyor.");
RequireClose(decodedTelemetry.PositionXM, 12.37, 0.001, "HMAC+Binary sonrası PositionX doğru.");
RequireClose(decodedTelemetry.YawRad, 1.574, 0.0001, "HMAC+Binary sonrası Yaw doğru.");

Console.WriteLine();
Console.WriteLine("[7] Boyut raporu");

Console.WriteLine($"Full envelope payload bytes     : {fullEnvelope.Payload.Length} byte");
Console.WriteLine($"Delta envelope payload bytes    : {deltaEnvelope.Payload.Length} byte");
Console.WriteLine($"Signed binary envelope bytes    : {encodedEnvelope.SizeBytes} byte");
Console.WriteLine($"Security tag bytes              : {protectedEnvelope.SecurityTag?.Length ?? 0} byte");

Require(deltaEnvelope.Payload.Length < fullEnvelope.Payload.Length, "Delta payload full payload'dan küçük.");

Console.WriteLine();
Console.WriteLine("====================================================");
Console.WriteLine(" TELEMETRY ENVELOPE SMOKE TEST PASSED ✅");
Console.WriteLine("====================================================");

static CompactTelemetryFrame CreateFrame(
    ulong sequence,
    CompactTelemetryField fieldMask,
    double x,
    double y,
    double yaw,
    double speed,
    double distance,
    double risk)
{
    return new CompactTelemetryFrame
    {
        VehicleId = "hydronom-main",
        Sequence = sequence,
        TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        FieldMask = fieldMask,

        PositionXM = x,
        PositionYM = y,
        PositionZM = -0.15,

        RollRad = 0.012,
        PitchRad = -0.024,
        YawRad = yaw,

        VelocityXMps = speed,
        VelocityYMps = 0.0,
        VelocityZMps = 0.0,

        AngularVelocityXRadps = 0.001,
        AngularVelocityYRadps = -0.002,
        AngularVelocityZRadps = 0.031,

        SpeedMps = speed,
        HeadingErrorRad = -0.128,
        DistanceToTargetM = distance,

        BatteryVoltageV = 15.82,
        BatteryPercent = 73.4,

        MissionProgress01 = 0.37,
        RiskScore01 = risk,

        ForceXN = 8.7,
        ForceYN = -1.2,
        ForceZN = 0.4,

        TorqueXNm = 0.12,
        TorqueYNm = -0.08,
        TorqueZNm = 0.31
    };
}

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