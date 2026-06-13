using System.Text;
using Hydronom.Core.Communication.Codecs;
using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Security;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE SECURE COMMAND SMOKE TEST");
Console.WriteLine("=================================================");

var secretKey = "hydronom-secure-command-smoke-secret-key";

var sender = new HydronomSecureCommandPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: "secure-command-smoke-session");

var receiver = new HydronomSecureCommandPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: "secure-command-smoke-session");

Console.WriteLine();
Console.WriteLine("[1] Arm command packet testi");

var armCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "ground-station",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 10,
    operatorId: "operator-001",
    reason: "Smoke test arm");

var armPacket = sender.BuildOutgoingCommandPacket(
    armCommand,
    correlationId: "corr-arm-001");

Require(armPacket.Accepted, "Arm packet üretildi.");
Require(armPacket.Command is not null, "Arm packet command taşıyor.");
Require(armPacket.Envelope is not null, "Arm packet envelope taşıyor.");
Require(armPacket.EncodedMessage.SizeBytes > 0, "Arm packet binary byte üretti.");

Require(armPacket.Envelope!.Type == HydronomMessageType.Arm, "Arm envelope Type doğru.");
Require(armPacket.Envelope.Priority == HydronomMessagePriority.Critical, "Arm priority Critical.");
Require(armPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.RequiresAck), "Arm RequiresAck flag taşıyor.");
Require(armPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSafetyCritical), "Arm SafetyCritical flag taşıyor.");
Require(armPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsOperatorCommand), "Arm OperatorCommand flag taşıyor.");
Require(armPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "Arm Signed flag taşıyor.");
Require(armPacket.Envelope.SecurityTag is { Length: 32 }, "Arm HMAC tag 32 byte.");

var armIncoming = receiver.ReadIncomingCommandPacket(armPacket.EncodedMessage.Bytes);

Require(armIncoming.Accepted, "Arm packet receiver tarafından kabul edildi.");
Require(armIncoming.SecurityResult is { Accepted: true }, "Arm HMAC doğrulandı.");
Require(armIncoming.Command is not null, "Arm command decode edildi.");
Require(armIncoming.Command!.Kind == HydronomCommandKind.Arm, "Arm command kind doğru.");
Require(armIncoming.Command.Authority == HydronomCommandAuthority.Operator, "Arm authority doğru.");
Require(armIncoming.Command.OperatorId == "operator-001", "Arm operator id doğru.");
Require(armIncoming.Command.VehicleId == "hydronom-main", "Arm vehicle id doğru.");

Console.WriteLine();
Console.WriteLine("[2] EmergencyStop command packet testi");

var emergencyCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.EmergencyStop,
    authority: HydronomCommandAuthority.EmergencyConsole,
    sourceId: "emergency-console",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 20,
    operatorId: "safety-001",
    reason: "Smoke test emergency stop");

var emergencyPacket = sender.BuildOutgoingCommandPacket(
    emergencyCommand,
    correlationId: "corr-estop-001");

Require(emergencyPacket.Accepted, "Emergency packet üretildi.");
Require(emergencyPacket.Envelope is not null, "Emergency envelope taşıyor.");
Require(emergencyPacket.Envelope!.Type == HydronomMessageType.EmergencyStop, "Emergency Type doğru.");
Require(emergencyPacket.Envelope.Priority == HydronomMessagePriority.Emergency, "Emergency priority Emergency.");
Require(emergencyPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.RequiresAck), "Emergency RequiresAck flag taşıyor.");
Require(emergencyPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSafetyCritical), "Emergency SafetyCritical flag taşıyor.");
Require(emergencyPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "Emergency Signed flag taşıyor.");

var emergencyIncoming = receiver.ReadIncomingCommandPacket(emergencyPacket.EncodedMessage.Bytes);

Require(emergencyIncoming.Accepted, "Emergency packet receiver tarafından kabul edildi.");
Require(emergencyIncoming.Command is not null, "Emergency command decode edildi.");
Require(emergencyIncoming.Command!.Kind == HydronomCommandKind.EmergencyStop, "Emergency kind doğru.");
Require(emergencyIncoming.Command.IsEmergency, "Emergency helper doğru.");

Console.WriteLine();
Console.WriteLine("[3] MissionCommand priority testi");

var missionCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.GroundStation,
    sourceId: "ground-station",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 30,
    operatorId: "operator-001",
    reason: "Start scenario",
    parameters: new Dictionary<string, string>
    {
        ["scenarioId"] = "teknofest-2026-parkur-1",
        ["command"] = "StartScenario"
    });

var missionPacket = sender.BuildOutgoingCommandPacket(
    missionCommand,
    correlationId: "corr-mission-001");

Require(missionPacket.Accepted, "Mission packet üretildi.");
Require(missionPacket.Envelope is not null, "Mission envelope taşıyor.");
Require(missionPacket.Envelope!.Type == HydronomMessageType.MissionCommand, "Mission envelope Type doğru.");
Require(missionPacket.Envelope.Priority == HydronomMessagePriority.High, "Mission priority High.");
Require(missionPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.RequiresAck), "Mission RequiresAck flag taşıyor.");
Require(missionPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSafetyCritical), "Mission SafetyCritical flag taşıyor.");

var missionIncoming = receiver.ReadIncomingCommandPacket(missionPacket.EncodedMessage.Bytes);

Require(missionIncoming.Accepted, "Mission packet receiver tarafından kabul edildi.");
Require(missionIncoming.Command is not null, "Mission command decode edildi.");
Require(missionIncoming.Command!.Kind == HydronomCommandKind.MissionCommand, "Mission kind doğru.");
Require(missionIncoming.Command.Parameters.TryGetValue("scenarioId", out var scenarioId), "Mission scenarioId parametresi geldi.");
Require(scenarioId == "teknofest-2026-parkur-1", "Mission scenarioId değeri doğru.");

Console.WriteLine();
Console.WriteLine("[4] Payload bozma HMAC reddi testi");

var tamperedPacketBytes = missionPacket.EncodedMessage.Bytes.ToArray();
tamperedPacketBytes[^20] ^= 0x33;

var tamperedResult = receiver.ReadIncomingCommandPacket(tamperedPacketBytes);

Require(!tamperedResult.Accepted, "Bozulmuş packet reddedildi.");
Require(
    tamperedResult.Reason.StartsWith("BINARY_DECODE_FAILED", StringComparison.Ordinal) ||
    tamperedResult.Reason == "SECURITY_TAG_INVALID",
    "Bozulmuş packet CRC veya HMAC tarafında yakalandı.");

Console.WriteLine();
Console.WriteLine("[6] Wrong VehicleId yakalama testi");

var adapter = new HydronomCommandEnvelopeAdapter();
var binaryCodec = new BinaryHydronomCodec();
var security = new HmacHydronomSecurityProvider(secretKey);

var wrongVehicleCommand = armCommand with
{
    CommandId = Guid.NewGuid().ToString("N"),
    Sequence = 50
};

var wrongVehicleEnvelope = adapter.ToEnvelope(
    wrongVehicleCommand,
    sessionId: "secure-command-smoke-session",
    correlationId: "corr-wrong-vehicle") with
{
    VehicleId = "wrong-vehicle"
};

var protectedWrongVehicle = security.Protect(
    wrongVehicleEnvelope,
    HydronomSecurityProfile.Race);

var wrongVehicleEncoded = binaryCodec.Encode(protectedWrongVehicle);

var wrongVehicleResult = receiver.ReadIncomingCommandPacket(wrongVehicleEncoded.Bytes);

Require(!wrongVehicleResult.Accepted, "Wrong VehicleId command reddedildi.");
Require(
    wrongVehicleResult.Reason.StartsWith("COMMAND_ENVELOPE_INVALID", StringComparison.Ordinal),
    $"Wrong VehicleId command envelope validasyonunda yakalandı. Reason={wrongVehicleResult.Reason}");

Console.WriteLine();
Console.WriteLine("[7] Wrong ContentType yakalama testi");

var wrongContentEnvelope = adapter.ToEnvelope(
    armCommand with
    {
        CommandId = Guid.NewGuid().ToString("N"),
        Sequence = 60
    },
    sessionId: "secure-command-smoke-session",
    correlationId: "corr-wrong-content") with
{
    ContentType = "application/json"
};

var protectedWrongContent = security.Protect(
    wrongContentEnvelope,
    HydronomSecurityProfile.Race);

var wrongContentEncoded = binaryCodec.Encode(protectedWrongContent);

var wrongContentResult = receiver.ReadIncomingCommandPacket(wrongContentEncoded.Bytes);

Require(!wrongContentResult.Accepted, "Wrong ContentType command reddedildi.");
Require(wrongContentResult.Reason.StartsWith("COMMAND_ENVELOPE_INVALID", StringComparison.Ordinal), "Wrong ContentType command envelope validasyonunda yakalandı.");

Console.WriteLine();
Console.WriteLine("[8] Boyut raporu");

Console.WriteLine($"Arm payload bytes       : {armPacket.PayloadBytes} byte");
Console.WriteLine($"Arm packet bytes        : {armPacket.PacketBytes} byte");
Console.WriteLine($"Emergency payload bytes : {emergencyPacket.PayloadBytes} byte");
Console.WriteLine($"Emergency packet bytes  : {emergencyPacket.PacketBytes} byte");
Console.WriteLine($"Mission payload bytes   : {missionPacket.PayloadBytes} byte");
Console.WriteLine($"Mission packet bytes    : {missionPacket.PacketBytes} byte");

Require(armPacket.PacketBytes > armPacket.PayloadBytes, "Arm packet envelope/security overhead içeriyor.");
Require(emergencyPacket.PacketBytes > emergencyPacket.PayloadBytes, "Emergency packet envelope/security overhead içeriyor.");
Require(missionPacket.PacketBytes > missionPacket.PayloadBytes, "Mission packet envelope/security overhead içeriyor.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" SECURE COMMAND SMOKE TEST PASSED ✅");
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