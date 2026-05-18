using System.Net;
using System.Text;
using Hydronom.Core.Communication.Commands;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Pipeline;
using Hydronom.Core.Communication.Security;
using Hydronom.Core.Communication.Telemetry;
using Hydronom.Core.Communication.Transport;
using Hydronom.Core.Communication.Transport.Tcp;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE TCP TRANSPORT SMOKE TEST");
Console.WriteLine("=================================================");

var secretKey = "hydronom-tcp-transport-smoke-secret-key";
var sessionId = "tcp-transport-smoke-session";
var port = GetPort();

await using var server = new HydronomTcpPacketServerTransport(
    transportId: "tcp-runtime-server",
    listenAddress: IPAddress.Loopback,
    port: port);

await using var client = new HydronomTcpPacketClientTransport(
    transportId: "tcp-ground-client",
    host: "127.0.0.1",
    port: port);

Console.WriteLine();
Console.WriteLine("[1] TCP server/client start testi");

await server.StartAsync();
await client.StartAsync();

await Task.Delay(150);

Require(server.IsRunning, "TCP server çalışıyor.");
Require(client.IsRunning, "TCP client çalışıyor.");

Console.WriteLine();
Console.WriteLine("[2] TCP client → server secure command testi");

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
    reason: "TCP transport smoke arm command");

var armPacket = commandSender.BuildOutgoingCommandPacket(
    armCommand,
    correlationId: "tcp-arm-001");

Require(armPacket.Accepted, "Arm command packet üretildi.");
Require(armPacket.EncodedMessage.SizeBytes > 0, "Arm command binary byte üretti.");

await client.SendAsync(HydronomTransportPacket.Create(
    bytes: armPacket.EncodedMessage.Bytes,
    sourceId: "operator-console",
    targetId: "runtime-main",
    transportId: "tcp-ground-client",
    channelId: "command",
    sequence: armCommand.Sequence));

var serverCommandTransportPacket = await WaitForPacketAsync(server, TimeSpan.FromSeconds(3));

Require(serverCommandTransportPacket is not null, "TCP server command packet aldı.");
Require(serverCommandTransportPacket!.ChannelId == "command", "Command channel korundu.");
Require(serverCommandTransportPacket.SizeBytes == armPacket.EncodedMessage.SizeBytes, "Command packet byte boyutu korundu.");

var receivedCommand = commandReceiver.Receive(serverCommandTransportPacket.Bytes);

Require(receivedCommand.Accepted, "TCP üstünden gelen command accepted.");
Require(receivedCommand.Command is not null, "TCP command decode edildi.");
Require(receivedCommand.Command!.Kind == HydronomCommandKind.Arm, "TCP command kind Arm.");
Require(receivedCommand.AuthorityDecision is { Allowed: true }, "TCP command authority allowed.");

Console.WriteLine();
Console.WriteLine("[3] TCP replay command reddi testi");

await client.SendAsync(HydronomTransportPacket.Create(
    bytes: armPacket.EncodedMessage.Bytes,
    sourceId: "operator-console",
    targetId: "runtime-main",
    transportId: "tcp-ground-client",
    channelId: "command",
    sequence: armCommand.Sequence));

var replayTransportPacket = await WaitForPacketAsync(server, TimeSpan.FromSeconds(3));

Require(replayTransportPacket is not null, "TCP server replay packet aldı.");

var replayResult = commandReceiver.Receive(replayTransportPacket!.Bytes);

Require(!replayResult.Accepted, "TCP replay command reddedildi.");
Require(replayResult.Status == HydronomSecureCommandReceiveStatus.SecurityRejected, "TCP replay SecurityRejected.");
Require(replayResult.Reason == "REPLAY_DETECTED", "TCP replay reason doğru.");

Console.WriteLine();
Console.WriteLine("[4] TCP server → client telemetry testi");

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
    correlationId: "tcp-telemetry-001");

Require(telemetryPacket.ShouldSend, "Telemetry packet gönderime hazır.");
Require(telemetryPacket.PacketBytes > 0, "Telemetry packet byte üretti.");

await server.SendAsync(HydronomTransportPacket.Create(
    bytes: telemetryPacket.EncodedMessage.Bytes,
    sourceId: "runtime-main",
    targetId: "ground-station",
    transportId: "tcp-runtime-server",
    channelId: "telemetry",
    sequence: telemetryFrame.Sequence));

var clientTelemetryTransportPacket = await WaitForPacketAsync(client, TimeSpan.FromSeconds(3));

Require(clientTelemetryTransportPacket is not null, "TCP client telemetry packet aldı.");
Require(clientTelemetryTransportPacket!.ChannelId == "telemetry", "Telemetry channel korundu.");
Require(clientTelemetryTransportPacket.SizeBytes == telemetryPacket.PacketBytes, "Telemetry packet byte boyutu korundu.");

var incomingTelemetry = telemetryReceiver.ReadIncomingTelemetryPacket(
    clientTelemetryTransportPacket.Bytes);

Require(incomingTelemetry.Accepted, "TCP telemetry receiver accepted.");
Require(incomingTelemetry.SecurityResult is { Accepted: true }, "TCP telemetry HMAC doğrulandı.");
Require(incomingTelemetry.TelemetryFrame is not null, "TCP telemetry decode edildi.");
RequireClose(incomingTelemetry.TelemetryFrame!.PositionXM, 12.35, 0.001, "TCP telemetry PositionX doğru.");
RequireClose(incomingTelemetry.TelemetryFrame.YawRad, 1.571, 0.0001, "TCP telemetry Yaw doğru.");
RequireClose(incomingTelemetry.TelemetryFrame.SpeedMps, 0.43, 0.0001, "TCP telemetry Speed doğru.");

Console.WriteLine();
Console.WriteLine("[5] TCP stats raporu");

var serverStats = server.SnapshotStats();
var clientStats = client.SnapshotStats();

Console.WriteLine($"Server SentPackets      : {serverStats.SentPackets}");
Console.WriteLine($"Server ReceivedPackets  : {serverStats.ReceivedPackets}");
Console.WriteLine($"Server SentBytes        : {serverStats.SentBytes}");
Console.WriteLine($"Server ReceivedBytes    : {serverStats.ReceivedBytes}");
Console.WriteLine($"Server DroppedPackets   : {serverStats.DroppedPackets}");

Console.WriteLine($"Client SentPackets      : {clientStats.SentPackets}");
Console.WriteLine($"Client ReceivedPackets  : {clientStats.ReceivedPackets}");
Console.WriteLine($"Client SentBytes        : {clientStats.SentBytes}");
Console.WriteLine($"Client ReceivedBytes    : {clientStats.ReceivedBytes}");
Console.WriteLine($"Client DroppedPackets   : {clientStats.DroppedPackets}");

Require(serverStats.ReceivedPackets >= 2, "Server en az iki command packet aldı.");
Require(serverStats.SentPackets >= 1, "Server en az bir telemetry packet gönderdi.");
Require(clientStats.SentPackets >= 2, "Client en az iki command packet gönderdi.");
Require(clientStats.ReceivedPackets >= 1, "Client en az bir telemetry packet aldı.");

Console.WriteLine();
Console.WriteLine("[6] TCP stop testi");

await client.StopAsync();
await server.StopAsync();

Require(!client.IsRunning, "TCP client durdu.");
Require(!server.IsRunning, "TCP server durdu.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" TCP TRANSPORT SMOKE TEST PASSED ✅");
Console.WriteLine("=================================================");

static int GetPort()
{
    // Sabit port çakışmasını azaltmak için test portunu hafif rastgele seçiyoruz.
    return Random.Shared.Next(22000, 26000);
}

static async Task<HydronomTransportPacket?> WaitForPacketAsync(
    IHydronomPacketTransport transport,
    TimeSpan timeout)
{
    var deadline = DateTimeOffset.UtcNow.Add(timeout);

    while (DateTimeOffset.UtcNow < deadline)
    {
        var packet = await transport.TryReceiveAsync();

        if (packet is not null)
        {
            return packet;
        }

        await Task.Delay(20);
    }

    return null;
}

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