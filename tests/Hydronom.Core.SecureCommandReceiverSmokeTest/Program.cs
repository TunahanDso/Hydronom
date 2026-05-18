using System.Text;
using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("======================================================");
Console.WriteLine(" HYDRONOM CORE SECURE COMMAND RECEIVER SMOKE TEST");
Console.WriteLine("======================================================");

var secretKey = "hydronom-secure-command-receiver-smoke-secret-key";
var sessionId = "secure-command-receiver-smoke-session";

var sender = new HydronomSecureCommandPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

var racePolicy = HydronomCommandAuthorityPolicy.Race
    .WithKnownSource("operator-console", HydronomCommandAuthority.Operator)
    .WithKnownSource("observer-console", HydronomCommandAuthority.Observer)
    .WithKnownSource("emergency-console", HydronomCommandAuthority.EmergencyConsole)
    .WithKnownSource("ground-station", HydronomCommandAuthority.GroundStation);

var receiver = new HydronomSecureCommandReceiver(
    hmacSecretKey: secretKey,
    authorityPolicy: racePolicy,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

Console.WriteLine();
Console.WriteLine("[1] Valid Operator Arm → Accepted testi");

var validArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    sequence: 100,
    operatorId: "operator-001",
    reason: "Operator aracı göreve hazırlıyor.");

var validArmPacket = sender.BuildOutgoingCommandPacket(
    validArm,
    correlationId: "receiver-valid-arm");

var validArmResult = receiver.Receive(validArmPacket.EncodedMessage.Bytes);

Require(validArmResult.Accepted, "Valid Arm receiver tarafından kabul edildi.");
Require(validArmResult.Status == HydronomSecureCommandReceiveStatus.Accepted, "Valid Arm status Accepted.");
Require(validArmResult.Command is not null, "Valid Arm command taşıyor.");
Require(validArmResult.Command!.Kind == HydronomCommandKind.Arm, "Valid Arm kind doğru.");
Require(validArmResult.SecurityResult is { Accepted: true }, "Valid Arm security accepted.");
Require(validArmResult.AuthorityDecision is { Allowed: true }, "Valid Arm authority accepted.");

Console.WriteLine();
Console.WriteLine("[2] Observer Arm → AuthorityRejected testi");

var observerArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Observer,
    sourceId: "observer-console",
    sequence: 200,
    operatorId: "observer-001",
    reason: "Observer yanlışlıkla arm deniyor.");

var observerArmPacket = sender.BuildOutgoingCommandPacket(
    observerArm,
    correlationId: "receiver-observer-arm");

var observerArmResult = receiver.Receive(observerArmPacket.EncodedMessage.Bytes);

Require(!observerArmResult.Accepted, "Observer Arm reddedildi.");
Require(observerArmResult.Status == HydronomSecureCommandReceiveStatus.AuthorityRejected, "Observer Arm AuthorityRejected.");
Require(observerArmResult.AuthorityDecision is { Allowed: false }, "Observer Arm authority decision rejected.");
Require(
    observerArmResult.Reason is "OBSERVER_STATUS_ONLY" or "COMMAND_NOT_ALLOWED_FOR_AUTHORITY",
    $"Observer Arm doğru sebeple reddedildi. Reason={observerArmResult.Reason}");

Console.WriteLine();
Console.WriteLine("[3] EmergencyStop → Accepted testi");

var emergency = CreateCommand(
    kind: HydronomCommandKind.EmergencyStop,
    authority: HydronomCommandAuthority.EmergencyConsole,
    sourceId: "emergency-console",
    sequence: 300,
    operatorId: "safety-001",
    reason: "Acil durdurma testi.");

var emergencyPacket = sender.BuildOutgoingCommandPacket(
    emergency,
    correlationId: "receiver-estop");

var emergencyResult = receiver.Receive(emergencyPacket.EncodedMessage.Bytes);

Require(emergencyResult.Accepted, "EmergencyStop accepted.");
Require(emergencyResult.Command is not null, "EmergencyStop command taşıyor.");
Require(emergencyResult.Command!.Kind == HydronomCommandKind.EmergencyStop, "EmergencyStop kind doğru.");
Require(emergencyResult.AuthorityDecision is { Allowed: true }, "EmergencyStop authority accepted.");

Console.WriteLine();
Console.WriteLine("[4] Replay packet → SecurityRejected testi");

var replayResult = receiver.Receive(emergencyPacket.EncodedMessage.Bytes);

Require(!replayResult.Accepted, "Replay packet reddedildi.");
Require(replayResult.Status == HydronomSecureCommandReceiveStatus.SecurityRejected, "Replay SecurityRejected.");
Require(replayResult.Reason == "REPLAY_DETECTED", "Replay reason doğru.");

Console.WriteLine();
Console.WriteLine("[5] Bozuk binary packet → DecodeRejected testi");

var corrupted = validArmPacket.EncodedMessage.Bytes.ToArray();
corrupted[^5] ^= 0x55;

var corruptedResult = receiver.Receive(corrupted);

Require(!corruptedResult.Accepted, "Bozuk packet reddedildi.");
Require(corruptedResult.Status == HydronomSecureCommandReceiveStatus.DecodeRejected, "Bozuk packet DecodeRejected.");
Require(corruptedResult.Reason.StartsWith("BINARY_DECODE_FAILED", StringComparison.Ordinal), "Bozuk packet binary/CRC tarafında yakalandı.");

Console.WriteLine();
Console.WriteLine("[6] Wrong ContentType → CommandInvalid testi");

var adapter = new HydronomCommandEnvelopeAdapter();
var binaryCodec = new BinaryHydronomCodec();
var security = new HmacHydronomSecurityProvider(secretKey);

var wrongContentCommand = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    sequence: 400,
    operatorId: "operator-001",
    reason: "Wrong content type testi.");

var wrongContentEnvelope = adapter.ToEnvelope(
    wrongContentCommand,
    sessionId: sessionId,
    correlationId: "receiver-wrong-content") with
{
    ContentType = "application/json"
};

var protectedWrongContent = security.Protect(
    wrongContentEnvelope,
    HydronomSecurityProfile.Race);

var wrongContentEncoded = binaryCodec.Encode(protectedWrongContent);
var wrongContentResult = receiver.Receive(wrongContentEncoded.Bytes);

Require(!wrongContentResult.Accepted, "Wrong ContentType reddedildi.");
Require(wrongContentResult.Status == HydronomSecureCommandReceiveStatus.CommandInvalid, "Wrong ContentType CommandInvalid.");
Require(wrongContentResult.Reason.StartsWith("COMMAND_ENVELOPE_INVALID", StringComparison.Ordinal), "Wrong ContentType command envelope validation tarafında yakalandı.");

Console.WriteLine();
Console.WriteLine("[7] Wrong source authority → AuthorityRejected testi");

var mismatch = CreateCommand(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "ground-station",
    sequence: 500,
    operatorId: "operator-001",
    reason: "GroundStation kendini Operator gibi gösteriyor.");

var mismatchPacket = sender.BuildOutgoingCommandPacket(
    mismatch,
    correlationId: "receiver-mismatch");

var mismatchResult = receiver.Receive(mismatchPacket.EncodedMessage.Bytes);

Require(!mismatchResult.Accepted, "Source authority mismatch reddedildi.");
Require(mismatchResult.Status == HydronomSecureCommandReceiveStatus.AuthorityRejected, "Source authority mismatch AuthorityRejected.");
Require(mismatchResult.Reason == "SOURCE_AUTHORITY_MISMATCH", "Source authority mismatch reason doğru.");

Console.WriteLine();
Console.WriteLine("[8] Boyut raporu");

Console.WriteLine($"Valid Arm packet bytes       : {validArmPacket.PacketBytes} byte");
Console.WriteLine($"Observer Arm packet bytes    : {observerArmPacket.PacketBytes} byte");
Console.WriteLine($"Emergency packet bytes       : {emergencyPacket.PacketBytes} byte");
Console.WriteLine($"Wrong content packet bytes   : {wrongContentEncoded.SizeBytes} byte");
Console.WriteLine($"Mismatch packet bytes        : {mismatchPacket.PacketBytes} byte");

Console.WriteLine();
Console.WriteLine("======================================================");
Console.WriteLine(" SECURE COMMAND RECEIVER SMOKE TEST PASSED ✅");
Console.WriteLine("======================================================");

static HydronomCommandFrame CreateCommand(
    HydronomCommandKind kind,
    HydronomCommandAuthority authority,
    string sourceId,
    ulong sequence,
    string operatorId,
    string reason)
{
    return HydronomCommandFrame.Create(
        kind: kind,
        authority: authority,
        sourceId: sourceId,
        targetId: "runtime-main",
        vehicleId: "hydronom-main",
        sequence: sequence,
        operatorId: operatorId,
        reason: reason,
        parameters: new Dictionary<string, string>
        {
            ["smoke"] = "true"
        });
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