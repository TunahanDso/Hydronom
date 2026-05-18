using System.Text;
using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.RuntimeBridge;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Transport;
using Hydronom.Core.Communication.Transport.InMemory;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("============================================================");
Console.WriteLine(" HYDRONOM CORE RUNTIME COMMAND BRIDGE + ACK SMOKE TEST");
Console.WriteLine("============================================================");

var secretKey = "hydronom-runtime-bridge-ack-smoke-secret-key";
var sessionId = "runtime-bridge-ack-smoke-session";

var sender = new HydronomSecureCommandPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

var authorityPolicy = HydronomCommandAuthorityPolicy.Race
    .WithKnownSource("operator-console", HydronomCommandAuthority.Operator)
    .WithKnownSource("ground-station", HydronomCommandAuthority.GroundStation)
    .WithKnownSource("observer-console", HydronomCommandAuthority.Observer)
    .WithKnownSource("emergency-console", HydronomCommandAuthority.EmergencyConsole);

var runtimeReceiver = new HydronomSecureRuntimeCommandReceiver(
    hmacSecretKey: secretKey,
    authorityPolicy: authorityPolicy,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

var ackPipeline = new HydronomSecureRuntimeCommandAckPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

Console.WriteLine();
Console.WriteLine("[1] Valid Arm command → Runtime intent testi");

var armCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 100,
    operatorId: "operator-001",
    reason: "Runtime bridge smoke arm command");

var armPacket = sender.BuildOutgoingCommandPacket(
    armCommand,
    correlationId: "runtime-bridge-arm-001");

Require(armPacket.Accepted, "Arm secure command packet üretildi.");
Require(armPacket.EncodedMessage.SizeBytes > 0, "Arm secure command byte üretti.");

var armRuntimeResult = runtimeReceiver.Receive(armPacket.EncodedMessage.Bytes);

Require(armRuntimeResult.Accepted, "Arm secure runtime receiver accepted.");
Require(armRuntimeResult.Status == HydronomSecureRuntimeCommandStatus.Accepted, "Arm runtime status Accepted.");
Require(armRuntimeResult.Intent is not null, "Arm runtime intent üretildi.");
Require(armRuntimeResult.Intent!.Kind == HydronomRuntimeCommandIntentKind.Arm, "Arm intent kind doğru.");
Require(armRuntimeResult.Intent.SourceCommandKind == HydronomCommandKind.Arm, "Arm source command kind doğru.");
Require(armRuntimeResult.Intent.VehicleId == "hydronom-main", "Arm intent VehicleId doğru.");
Require(armRuntimeResult.Intent.OperatorId == "operator-001", "Arm intent OperatorId doğru.");
Require(armRuntimeResult.Intent.SafetyCritical, "Arm intent safety critical.");

Console.WriteLine();
Console.WriteLine("[2] Receive result → Accepted ACK testi");

var armAcceptedAck = HydronomRuntimeCommandAckFactory.FromReceiveResult(
    armRuntimeResult);

Require(armAcceptedAck.Status == HydronomRuntimeCommandAckStatus.Accepted, "Arm receive result Accepted ACK üretti.");
Require(armAcceptedAck.Accepted, "Arm ACK accepted helper true.");
Require(!armAcceptedAck.Rejected, "Arm ACK rejected helper false.");
Require(!string.IsNullOrWhiteSpace(armAcceptedAck.CommandId), "Arm ACK CommandId dolu.");
Require(!string.IsNullOrWhiteSpace(armAcceptedAck.IntentId), "Arm ACK IntentId dolu.");
Require(armAcceptedAck.SourceId == "runtime-main", "Arm ACK SourceId runtime-main.");
Require(armAcceptedAck.TargetId == "operator-console", "Arm ACK TargetId operator-console.");

var armAckPacket = ackPipeline.BuildOutgoingAckPacket(
    armAcceptedAck,
    correlationId: "runtime-bridge-arm-ack-001");

Require(armAckPacket.ReadyToSend, "Arm ACK packet gönderime hazır.");
Require(armAckPacket.Envelope is not null, "Arm ACK envelope var.");
Require(armAckPacket.Envelope!.Flags.HasFlag(Hydronom.Core.Communication.Envelope.HydronomMessageFlags.IsAck), "Arm ACK envelope IsAck flag taşıyor.");
Require(!armAckPacket.Envelope.Flags.HasFlag(Hydronom.Core.Communication.Envelope.HydronomMessageFlags.IsNack), "Arm ACK envelope IsNack taşımıyor.");
Require(armAckPacket.Envelope.SecurityTag is { Length: 32 }, "Arm ACK HMAC tag var.");
Require(armAckPacket.EncodedMessage.SizeBytes > 0, "Arm ACK binary byte üretti.");

var decodedArmAck = ackPipeline.ReadIncomingAckPacket(
    armAckPacket.EncodedMessage.Bytes);

Require(decodedArmAck.Accepted, "Arm ACK tekrar okunup kabul edildi.");
Require(decodedArmAck.SecurityResult is { Accepted: true }, "Arm ACK HMAC doğrulandı.");
Require(decodedArmAck.Ack is not null, "Arm ACK payload decode edildi.");
Require(decodedArmAck.Ack!.CommandId == armAcceptedAck.CommandId, "Arm ACK CommandId korundu.");
Require(decodedArmAck.Ack.IntentId == armAcceptedAck.IntentId, "Arm ACK IntentId korundu.");
Require(decodedArmAck.Ack.Status == HydronomRuntimeCommandAckStatus.Accepted, "Arm ACK status korundu.");

Console.WriteLine();
Console.WriteLine("[3] QueuedForSafetyGate / Applied ACK modeli testi");

var queuedAck = HydronomRuntimeCommandAckFactory.QueuedForSafetyGate(
    armRuntimeResult.Intent!,
    "Arm command queued for safety gate");

Require(queuedAck.Status == HydronomRuntimeCommandAckStatus.QueuedForSafetyGate, "QueuedForSafetyGate ACK status doğru.");
Require(queuedAck.Accepted, "QueuedForSafetyGate accepted helper true.");
Require(!queuedAck.Terminal, "QueuedForSafetyGate terminal değil.");

var appliedAck = HydronomRuntimeCommandAckFactory.Applied(
    armRuntimeResult.Intent!,
    "Arm command applied");

Require(appliedAck.Status == HydronomRuntimeCommandAckStatus.Applied, "Applied ACK status doğru.");
Require(appliedAck.Accepted, "Applied ACK accepted helper true.");
Require(appliedAck.Terminal, "Applied ACK terminal.");

var appliedPacket = ackPipeline.BuildOutgoingAckPacket(
    appliedAck,
    correlationId: "runtime-bridge-arm-applied-ack");

var appliedRead = ackPipeline.ReadIncomingAckPacket(
    appliedPacket.EncodedMessage.Bytes);

Require(appliedRead.Accepted, "Applied ACK packet okunup kabul edildi.");
Require(appliedRead.Ack is { Status: HydronomRuntimeCommandAckStatus.Applied }, "Applied ACK status decode edildi.");

Console.WriteLine();
Console.WriteLine("[4] Observer Arm → AuthorityRejected → NACK testi");

var observerArm = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Observer,
    sourceId: "observer-console",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 200,
    operatorId: "observer-001",
    reason: "Observer arm should be rejected");

var observerPacket = sender.BuildOutgoingCommandPacket(
    observerArm,
    correlationId: "runtime-bridge-observer-arm");

var observerRuntimeResult = runtimeReceiver.Receive(
    observerPacket.EncodedMessage.Bytes);

Require(!observerRuntimeResult.Accepted, "Observer Arm runtime receiver reddetti.");
Require(observerRuntimeResult.Status == HydronomSecureRuntimeCommandStatus.AuthorityRejected, "Observer Arm AuthorityRejected.");
Require(observerRuntimeResult.Intent is null, "Observer Arm intent üretmedi.");

var observerAck = HydronomRuntimeCommandAckFactory.FromReceiveResult(
    observerRuntimeResult);

Require(observerAck.Status == HydronomRuntimeCommandAckStatus.RejectedByAuthority, "Observer ACK RejectedByAuthority.");
Require(observerAck.Rejected, "Observer ACK rejected helper true.");
Require(observerAck.Terminal, "Observer ACK terminal.");
Require(observerAck.TargetId == "observer-console", "Observer ACK target observer-console.");

var observerAckPacket = ackPipeline.BuildOutgoingAckPacket(
    observerAck,
    correlationId: "runtime-bridge-observer-nack");

Require(observerAckPacket.Envelope is not null, "Observer NACK envelope var.");
Require(observerAckPacket.Envelope!.Flags.HasFlag(Hydronom.Core.Communication.Envelope.HydronomMessageFlags.IsAck), "Observer NACK IsAck taşıyor.");
Require(observerAckPacket.Envelope.Flags.HasFlag(Hydronom.Core.Communication.Envelope.HydronomMessageFlags.IsNack), "Observer NACK IsNack taşıyor.");
Require(observerAckPacket.Envelope.Priority == Hydronom.Core.Communication.Envelope.HydronomMessagePriority.Critical, "Observer NACK priority Critical.");

var observerAckRead = ackPipeline.ReadIncomingAckPacket(
    observerAckPacket.EncodedMessage.Bytes);

Require(observerAckRead.Accepted, "Observer NACK okunup kabul edildi.");
Require(observerAckRead.Ack is not null, "Observer NACK payload decode edildi.");
Require(observerAckRead.Ack!.Status == HydronomRuntimeCommandAckStatus.RejectedByAuthority, "Observer NACK status decode edildi.");

Console.WriteLine();
Console.WriteLine("[5] MissionCommand → StartScenario intent testi");

var scenarioCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.GroundStation,
    sourceId: "ground-station",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 300,
    operatorId: "",
    reason: "Start scenario from ground station",
    parameters: new Dictionary<string, string>
    {
        ["command"] = "StartScenario",
        ["scenarioId"] = "teknofest_2026_parkur_1"
    });

var scenarioPacket = sender.BuildOutgoingCommandPacket(
    scenarioCommand,
    correlationId: "runtime-bridge-start-scenario");

var scenarioRuntimeResult = runtimeReceiver.Receive(
    scenarioPacket.EncodedMessage.Bytes);

Require(scenarioRuntimeResult.Accepted, "Scenario command runtime receiver accepted.");
Require(scenarioRuntimeResult.Intent is not null, "Scenario command intent üretildi.");
Require(scenarioRuntimeResult.Intent!.Kind == HydronomRuntimeCommandIntentKind.StartScenario, "Scenario intent StartScenario.");
Require(scenarioRuntimeResult.Intent.TryGetParameter("scenarioId", out var scenarioId), "Scenario intent scenarioId parametresi var.");
Require(scenarioId == "teknofest_2026_parkur_1", "ScenarioId değeri korundu.");

var scenarioAck = HydronomRuntimeCommandAckFactory.QueuedForExecution(
    scenarioRuntimeResult.Intent,
    "Scenario command queued for execution");

var scenarioAckPacket = ackPipeline.BuildOutgoingAckPacket(
    scenarioAck,
    correlationId: "runtime-bridge-scenario-ack");

var scenarioAckRead = ackPipeline.ReadIncomingAckPacket(
    scenarioAckPacket.EncodedMessage.Bytes);

Require(scenarioAckRead.Accepted, "Scenario ACK okunup kabul edildi.");
Require(scenarioAckRead.Ack is not null, "Scenario ACK payload decode edildi.");
Require(scenarioAckRead.Ack!.Status == HydronomRuntimeCommandAckStatus.QueuedForExecution, "Scenario ACK QueuedForExecution.");
Require(scenarioAckRead.Ack.IntentKind == HydronomRuntimeCommandIntentKind.StartScenario, "Scenario ACK intent kind korundu.");

Console.WriteLine();
Console.WriteLine("[6] InMemory transport command + ACK roundtrip testi");

await using var pair = InMemoryHydronomTransportPair.Create(
    aId: "runtime-side",
    bId: "ground-side");

await pair.StartAsync();

var transportCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.Disarm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 400,
    operatorId: "operator-001",
    reason: "Transport disarm roundtrip");

var transportCommandPacket = sender.BuildOutgoingCommandPacket(
    transportCommand,
    correlationId: "runtime-bridge-transport-disarm");

await pair.B.SendAsync(HydronomTransportPacket.Create(
    bytes: transportCommandPacket.EncodedMessage.Bytes,
    sourceId: "operator-console",
    targetId: "runtime-main",
    transportId: "ground-side",
    channelId: "command",
    sequence: transportCommand.Sequence));

var runtimeTransportPacket = await pair.A.TryReceiveAsync();

Require(runtimeTransportPacket is not null, "Runtime side command transport packet aldı.");
Require(runtimeTransportPacket!.ChannelId == "command", "Command channel korundu.");

var transportRuntimeResult = runtimeReceiver.Receive(
    runtimeTransportPacket.Bytes);

Require(transportRuntimeResult.Accepted, "Transport command runtime receiver accepted.");
Require(transportRuntimeResult.Intent is not null, "Transport command intent üretildi.");
Require(transportRuntimeResult.Intent!.Kind == HydronomRuntimeCommandIntentKind.Disarm, "Transport command intent Disarm.");

var transportAck = HydronomRuntimeCommandAckFactory.QueuedForSafetyGate(
    transportRuntimeResult.Intent,
    "Disarm queued for safety gate");

var transportAckPacket = ackPipeline.BuildOutgoingAckPacket(
    transportAck,
    correlationId: "runtime-bridge-transport-ack");

await pair.A.SendAsync(HydronomTransportPacket.Create(
    bytes: transportAckPacket.EncodedMessage.Bytes,
    sourceId: "runtime-main",
    targetId: "operator-console",
    transportId: "runtime-side",
    channelId: "ack",
    sequence: transportAck.Sequence));

var groundAckTransportPacket = await pair.B.TryReceiveAsync();

Require(groundAckTransportPacket is not null, "Ground side ACK transport packet aldı.");
Require(groundAckTransportPacket!.ChannelId == "ack", "ACK channel korundu.");

var groundAckRead = ackPipeline.ReadIncomingAckPacket(
    groundAckTransportPacket.Bytes);

Require(groundAckRead.Accepted, "Ground side ACK read accepted.");
Require(groundAckRead.Ack is not null, "Ground side ACK payload decode edildi.");
Require(groundAckRead.Ack!.Status == HydronomRuntimeCommandAckStatus.QueuedForSafetyGate, "Ground side ACK status QueuedForSafetyGate.");

await pair.StopAsync();

Console.WriteLine();
Console.WriteLine("[7] Boyut raporu");

Console.WriteLine($"Arm command packet bytes       : {armPacket.PacketBytes} byte");
Console.WriteLine($"Arm accepted ACK packet bytes  : {armAckPacket.PacketBytes} byte");
Console.WriteLine($"Observer NACK packet bytes     : {observerAckPacket.PacketBytes} byte");
Console.WriteLine($"Scenario ACK packet bytes      : {scenarioAckPacket.PacketBytes} byte");
Console.WriteLine($"Transport ACK packet bytes     : {transportAckPacket.PacketBytes} byte");

Require(armAckPacket.PacketBytes > 0, "Arm ACK packet byte var.");
Require(observerAckPacket.PacketBytes > 0, "Observer NACK packet byte var.");
Require(scenarioAckPacket.PacketBytes > 0, "Scenario ACK packet byte var.");

Console.WriteLine();
Console.WriteLine("============================================================");
Console.WriteLine(" RUNTIME COMMAND BRIDGE + ACK SMOKE TEST PASSED ✅");
Console.WriteLine("============================================================");

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