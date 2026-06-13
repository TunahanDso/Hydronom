using System.Text;
using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Pipeline;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;
using Hydronom.Core.Communication.Transport;
using Hydronom.Core.Communication.Transport.InMemory;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE TRANSPORT SMOKE TEST");
Console.WriteLine("=================================================");

var secretKey = "hydronom-transport-smoke-secret-key";
var sessionId = "transport-smoke-session";

await using var pair = InMemoryHydronomTransportPair.Create(
    aId: "runtime-transport",
    bId: "ground-transport");

await pair.StartAsync();

Console.WriteLine();
Console.WriteLine("[1] InMemory transport start testi");

Require(pair.A.IsRunning, "Transport A çalışıyor.");
Require(pair.B.IsRunning, "Transport B çalışıyor.");

Console.WriteLine();
Console.WriteLine("[2] Secure command transport testi");

var commandSender = new HydronomSecureCommandPipeline(
    hmacSecretKey: secretKey,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

var authorityPolicy = HydronomCommandAuthorityPolicy.Race
    .WithKnownSource("operator-console", HydronomCommandAuthority.Operator)
    .WithKnownSource("ground-station", HydronomCommandAuthority.GroundStation)
    .WithKnownSource("emergency-console", HydronomCommandAuthority.EmergencyConsole);

var commandReceiver = new HydronomSecureCommandReceiver(
    hmacSecretKey: secretKey,
    authorityPolicy: authorityPolicy,
    securityProfile: HydronomSecurityProfile.Race,
    enableSecurity: true,
    sessionId: sessionId);

var armCommand = HydronomCommandFrame.Create(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    targetId: "runtime-main",
    vehicleId: "hydronom-main",
    sequence: 100,
    operatorId: "operator-001",
    reason: "Transport smoke arm command");

var armPacket = commandSender.BuildOutgoingCommandPacket(
    armCommand,
    correlationId: "transport-arm-001");

Require(armPacket.Accepted, "Arm command packet üretildi.");
Require(armPacket.EncodedMessage.SizeBytes > 0, "Arm command packet byte üretti.");

await pair.B.SendAsync(HydronomTransportPacket.Create(
    bytes: armPacket.EncodedMessage.Bytes,
    sourceId: "ground-station",
    targetId: "runtime-main",
    transportId: "ground-transport",
    channelId: "command",
    sequence: armCommand.Sequence));

var receivedCommandTransportPacket = await pair.A.TryReceiveAsync();

Require(receivedCommandTransportPacket is not null, "Runtime transport command packet aldı.");
Require(receivedCommandTransportPacket!.ChannelId == "command", "Command channel korundu.");
Require(receivedCommandTransportPacket.SizeBytes == armPacket.EncodedMessage.SizeBytes, "Command packet byte boyutu korundu.");

var receivedCommandResult = commandReceiver.Receive(receivedCommandTransportPacket.Bytes);

Require(receivedCommandResult.Accepted, "Transport üzerinden gelen command accepted.");
Require(receivedCommandResult.Command is not null, "Transport command decode edildi.");
Require(receivedCommandResult.Command!.Kind == HydronomCommandKind.Arm, "Transport command kind Arm.");
Require(receivedCommandResult.AuthorityDecision is { Allowed: true }, "Transport command authority allowed.");

Console.WriteLine();
Console.WriteLine("[3] Secure command replay transport testi");

await pair.B.SendAsync(HydronomTransportPacket.Create(
    bytes: armPacket.EncodedMessage.Bytes,
    sourceId: "ground-station",
    targetId: "runtime-main",
    transportId: "ground-transport",
    channelId: "command",
    sequence: armCommand.Sequence));

var replayTransportPacket = await pair.A.TryReceiveAsync();

Require(replayTransportPacket is not null, "Replay transport packet alındı.");

var replayResult = commandReceiver.Receive(replayTransportPacket!.Bytes);

Require(!replayResult.Accepted, "Replay command transport üzerinden reddedildi.");
Require(replayResult.Status == HydronomSecureCommandReceiveStatus.SecurityRejected, "Replay SecurityRejected.");
Require(replayResult.Reason == "REPLAY_DETECTED", "Replay reason doğru.");

Console.WriteLine();
Console.WriteLine("[4] Telemetry transport testi");

var telemetryOptions = HydronomCommunicationPipelineOptions.Race with
{
    SourceId = "runtime-main",
    TargetId = "ground-station",
    SessionId = sessionId,
    HmacSecretKey = secretKey
};

var telemetrySender = new HydronomCommunicationPipeline(telemetryOptions);
var telemetryReceiver = new HydronomCommunicationPipeline(telemetryOptions);

var telemetryFrame = CreateTelemetryFrame(
    sequence: 1000,
    x: 12.35,
    y: 8.42,
    yaw: 1.571,
    speed: 0.43,
    distance: 17.86,
    risk: 0.12);

var telemetryPacket = telemetrySender.BuildOutgoingTelemetryPacket(
    telemetryFrame,
    priority: HydronomMessagePriority.High,
    extraFlags: HydronomMessageFlags.None,
    correlationId: "transport-telemetry-001");

Require(telemetryPacket.ShouldSend, "Telemetry packet gönderime hazır.");
Require(telemetryPacket.PacketBytes > 0, "Telemetry packet byte üretti.");

await pair.A.SendAsync(HydronomTransportPacket.Create(
    bytes: telemetryPacket.EncodedMessage.Bytes,
    sourceId: "runtime-main",
    targetId: "ground-station",
    transportId: "runtime-transport",
    channelId: "telemetry",
    sequence: telemetryFrame.Sequence));

var receivedTelemetryTransportPacket = await pair.B.TryReceiveAsync();

Require(receivedTelemetryTransportPacket is not null, "Ground transport telemetry packet aldı.");
Require(receivedTelemetryTransportPacket!.ChannelId == "telemetry", "Telemetry channel korundu.");
Require(receivedTelemetryTransportPacket.SizeBytes == telemetryPacket.PacketBytes, "Telemetry packet byte boyutu korundu.");

var telemetryIncoming = telemetryReceiver.ReadIncomingTelemetryPacket(
    receivedTelemetryTransportPacket.Bytes);

Require(telemetryIncoming.Accepted, "Transport telemetry receiver tarafından kabul edildi.");
Require(telemetryIncoming.TelemetryFrame is not null, "Transport telemetry decode edildi.");
Require(telemetryIncoming.SecurityResult is { Accepted: true }, "Transport telemetry HMAC doğrulandı.");
RequireClose(telemetryIncoming.TelemetryFrame!.PositionXM, 12.35, 0.001, "Transport telemetry PositionX doğru.");
RequireClose(telemetryIncoming.TelemetryFrame.YawRad, 1.571, 0.0001, "Transport telemetry Yaw doğru.");
RequireClose(telemetryIncoming.TelemetryFrame.SpeedMps, 0.43, 0.0001, "Transport telemetry Speed doğru.");

Console.WriteLine();
Console.WriteLine("[5] Transport stop/drop testi");

await pair.StopAsync();

Require(!pair.A.IsRunning, "Transport A durdu.");
Require(!pair.B.IsRunning, "Transport B durdu.");

await pair.A.SendAsync(HydronomTransportPacket.Create(
    bytes: telemetryPacket.EncodedMessage.Bytes,
    sourceId: "runtime-main",
    targetId: "ground-station",
    transportId: "runtime-transport",
    channelId: "telemetry",
    sequence: 9999));

var statsA = pair.A.SnapshotStats();
var statsB = pair.B.SnapshotStats();

Require(statsA.DroppedPackets >= 1, "Stopped transport drop metriği arttı.");

Console.WriteLine();
Console.WriteLine("[6] Transport stats raporu");

Console.WriteLine($"A SentPackets      : {statsA.SentPackets}");
Console.WriteLine($"A ReceivedPackets  : {statsA.ReceivedPackets}");
Console.WriteLine($"A SentBytes        : {statsA.SentBytes}");
Console.WriteLine($"A ReceivedBytes    : {statsA.ReceivedBytes}");
Console.WriteLine($"A DroppedPackets   : {statsA.DroppedPackets}");

Console.WriteLine($"B SentPackets      : {statsB.SentPackets}");
Console.WriteLine($"B ReceivedPackets  : {statsB.ReceivedPackets}");
Console.WriteLine($"B SentBytes        : {statsB.SentBytes}");
Console.WriteLine($"B ReceivedBytes    : {statsB.ReceivedBytes}");
Console.WriteLine($"B DroppedPackets   : {statsB.DroppedPackets}");

Require(statsA.SentPackets >= 1, "Transport A en az bir packet gönderdi.");
Require(statsB.SentPackets >= 2, "Transport B en az iki packet gönderdi.");
Require(statsA.ReceivedPackets >= 2, "Transport A command packetleri aldı.");
Require(statsB.ReceivedPackets >= 1, "Transport B telemetry packet aldı.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" TRANSPORT SMOKE TEST PASSED ✅");
Console.WriteLine("=================================================");

static CompactTelemetryFrame CreateTelemetryFrame(
    ulong sequence,
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
        FieldMask = CompactTelemetryField.All,

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