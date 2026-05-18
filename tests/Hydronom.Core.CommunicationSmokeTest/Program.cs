using System.Text;
using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

using CommunicationEnvelope = Hydronom.Core.Communication.Envelope.HydronomEnvelope;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("==============================================");
Console.WriteLine(" HYDRONOM CORE COMMUNICATION SMOKE TEST");
Console.WriteLine("==============================================");

var payload = Encoding.UTF8.GetBytes("""
{
  "vehicleId": "hydronom-main",
  "x": 12.35,
  "y": 8.42,
  "z": -0.15,
  "yawRad": 1.57,
  "speedMps": 0.42,
  "mission": "SurfacePatrol"
}
""");

var envelope = CommunicationEnvelope.Create(
    type: HydronomMessageType.FusedState,
    priority: HydronomMessagePriority.High,
    sourceId: "runtime-main",
    targetId: "ground-station",
    vehicleId: "hydronom-main",
    sequence: 1,
    payload: payload,
    flags: HydronomMessageFlags.RequiresAck,
    sessionId: "session-smoke-001",
    correlationId: "corr-smoke-001",
    contentType: "application/json");

Require(envelope.Type == HydronomMessageType.FusedState, "Envelope type korunuyor.");
Require(envelope.Payload.Length > 0, "Envelope payload dolu.");
Require(envelope.RequiresAck, "RequiresAck flag çalışıyor.");

Console.WriteLine();
Console.WriteLine("[1] JSON codec testi");

var jsonCodec = new JsonHydronomCodec();
var jsonEncoded = jsonCodec.Encode(envelope);
var jsonDecoded = jsonCodec.Decode(jsonEncoded);

Require(jsonEncoded.Bytes.Length > 0, "JSON encode byte üretti.");
Require(jsonDecoded.Type == envelope.Type, "JSON decode type doğru.");
Require(jsonDecoded.Priority == envelope.Priority, "JSON decode priority doğru.");
Require(jsonDecoded.Sequence == envelope.Sequence, "JSON decode sequence doğru.");
Require(jsonDecoded.SourceId == envelope.SourceId, "JSON decode source doğru.");
Require(jsonDecoded.TargetId == envelope.TargetId, "JSON decode target doğru.");
Require(jsonDecoded.VehicleId == envelope.VehicleId, "JSON decode vehicle doğru.");
Require(PayloadEquals(jsonDecoded.Payload, envelope.Payload), "JSON decode payload doğru.");

Console.WriteLine();
Console.WriteLine("[2] Binary codec + CRC testi");

var binaryCodec = new BinaryHydronomCodec();
var binaryEncoded = binaryCodec.Encode(envelope);
var binaryDecoded = binaryCodec.Decode(binaryEncoded);

Require(binaryEncoded.Bytes.Length > 0, "Binary encode byte üretti.");
Require(binaryDecoded.Type == envelope.Type, "Binary decode type doğru.");
Require(binaryDecoded.Priority == envelope.Priority, "Binary decode priority doğru.");
Require(binaryDecoded.Sequence == envelope.Sequence, "Binary decode sequence doğru.");
Require(binaryDecoded.TimestampUnixMs == envelope.TimestampUnixMs, "Binary decode timestamp doğru.");
Require(binaryDecoded.SourceId == envelope.SourceId, "Binary decode source doğru.");
Require(binaryDecoded.TargetId == envelope.TargetId, "Binary decode target doğru.");
Require(binaryDecoded.VehicleId == envelope.VehicleId, "Binary decode vehicle doğru.");
Require(PayloadEquals(binaryDecoded.Payload, envelope.Payload), "Binary decode payload doğru.");

Console.WriteLine();
Console.WriteLine("[3] Binary CRC bozma testi");

var corruptedBinary = binaryEncoded.Bytes.ToArray();
corruptedBinary[^8] ^= 0x5A;

var crcRejected = false;

try
{
    _ = binaryCodec.Decode(binaryEncoded with
    {
        Bytes = corruptedBinary
    });
}
catch (InvalidDataException)
{
    crcRejected = true;
}

Require(crcRejected, "Binary CRC bozuk paketi reddetti.");

Console.WriteLine();
Console.WriteLine("[4] HMAC security testi");

var replayWindow = new AntiReplayWindow();
var security = new HmacHydronomSecurityProvider(
    "hydronom-super-secret-smoke-key",
    replayWindow);

var signed = security.Protect(envelope, HydronomSecurityProfile.Race);

Require(signed.SecurityTag is { Length: > 0 }, "HMAC tag üretildi.");
Require(signed.Flags.HasFlag(HydronomMessageFlags.IsSigned), "IsSigned flag eklendi.");

var verifyResult = security.Verify(signed, HydronomSecurityProfile.Race);

Require(verifyResult.Accepted, "HMAC doğrulama kabul edildi.");

Console.WriteLine();
Console.WriteLine("[5] HMAC payload bozma testi");

var tampered = signed with
{
    Sequence = 2,
    Payload = Encoding.UTF8.GetBytes("bozulmuş payload")
};

var tamperedResult = security.Verify(tampered, HydronomSecurityProfile.Race);

Require(!tamperedResult.Accepted, "Payload bozulunca HMAC reddetti.");
Require(tamperedResult.Reason == "SECURITY_TAG_INVALID", "HMAC red nedeni doğru.");

Console.WriteLine();
Console.WriteLine("[6] Anti-replay testi");

var replayEnvelope = CommunicationEnvelope.Create(
    type: HydronomMessageType.MissionCommand,
    priority: HydronomMessagePriority.Critical,
    sourceId: "ground-station",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 100,
    payload: Encoding.UTF8.GetBytes("""{"command":"Arm"}"""),
    flags: HydronomMessageFlags.RequiresAck | HydronomMessageFlags.IsOperatorCommand,
    sessionId: "session-smoke-001",
    correlationId: "corr-arm-001",
    contentType: "application/json");

var signedReplayEnvelope = security.Protect(replayEnvelope, HydronomSecurityProfile.Race);

var firstReplayCheck = security.Verify(signedReplayEnvelope, HydronomSecurityProfile.Race);
Require(firstReplayCheck.Accepted, "İlk sequence kabul edildi.");

var secondReplayCheck = security.Verify(signedReplayEnvelope, HydronomSecurityProfile.Race);
Require(!secondReplayCheck.Accepted, "Aynı sequence tekrar gelince reddedildi.");
Require(secondReplayCheck.Reason == "REPLAY_DETECTED", "Anti-replay red nedeni doğru.");

Console.WriteLine();
Console.WriteLine("[7] Boyut karşılaştırması");

Console.WriteLine($"JSON encoded size   : {jsonEncoded.SizeBytes} byte");
Console.WriteLine($"Binary encoded size : {binaryEncoded.SizeBytes} byte");
Console.WriteLine($"Payload size        : {payload.Length} byte");

Console.WriteLine();
Console.WriteLine("==============================================");
Console.WriteLine(" COMMUNICATION SMOKE TEST PASSED ✅");
Console.WriteLine("==============================================");

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

static bool PayloadEquals(byte[] left, byte[] right)
{
    return left.AsSpan().SequenceEqual(right);
}