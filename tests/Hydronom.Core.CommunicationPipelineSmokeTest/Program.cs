using System.Text;
using Hydronom.Core.Communication.Envelope;
using Hydronom.Core.Communication.Pipeline;
using Hydronom.Core.Communication.Telemetry;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("======================================================");
Console.WriteLine(" HYDRONOM CORE COMMUNICATION PIPELINE SMOKE TEST");
Console.WriteLine("======================================================");

Console.WriteLine();
Console.WriteLine("[1] Race pipeline ilk frame full packet testi");

var raceOptions = HydronomCommunicationPipelineOptions.Race with
{
    SourceId = "runtime-main",
    TargetId = "ground-station",
    SessionId = "pipeline-smoke-session",
    HmacSecretKey = "hydronom-pipeline-smoke-secret-key"
};

var senderPipeline = new HydronomCommunicationPipeline(raceOptions);
var receiverPipeline = new HydronomCommunicationPipeline(raceOptions);

var firstFrame = CreateFrame(
    sequence: 1000,
    x: 12.35,
    y: 8.42,
    yaw: 1.571,
    speed: 0.43,
    distance: 17.86,
    risk: 0.12);

var firstPacket = senderPipeline.BuildOutgoingTelemetryPacket(
    firstFrame,
    priority: HydronomMessagePriority.High,
    extraFlags: HydronomMessageFlags.RequiresAck,
    correlationId: "corr-pipeline-full");

Require(firstPacket.ShouldSend, "İlk packet gönderime hazır.");
Require(firstPacket.TelemetryFrame is not null, "İlk packet telemetry frame taşıyor.");
Require(firstPacket.Envelope is not null, "İlk packet envelope taşıyor.");
Require(firstPacket.EncodedMessage.SizeBytes > 0, "İlk packet binary bytes üretti.");
Require(firstPacket.TelemetryFrame!.FieldMask == CompactTelemetryField.All, "İlk packet full telemetry oldu.");
Require(firstPacket.Envelope!.Flags.HasFlag(HydronomMessageFlags.IsSnapshot), "İlk packet snapshot flag taşıyor.");
Require(firstPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "İlk packet signed flag taşıyor.");
Require(firstPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.RequiresAck), "Extra RequiresAck flag korundu.");

var firstIncoming = receiverPipeline.ReadIncomingTelemetryPacket(firstPacket.EncodedMessage.Bytes);

Require(firstIncoming.Accepted, "İlk packet receiver tarafından kabul edildi.");
Require(firstIncoming.TelemetryFrame is not null, "İlk incoming telemetry çözüldü.");
Require(firstIncoming.SecurityResult is { Accepted: true }, "İlk incoming HMAC doğrulandı.");
Require(firstIncoming.TelemetryFrame!.FieldMask == CompactTelemetryField.All, "İlk incoming full mask korundu.");
RequireClose(firstIncoming.TelemetryFrame.PositionXM, 12.35, 0.001, "İlk incoming PositionX doğru.");
RequireClose(firstIncoming.TelemetryFrame.YawRad, 1.571, 0.0001, "İlk incoming Yaw doğru.");
RequireClose(firstIncoming.TelemetryFrame.SpeedMps, 0.43, 0.0001, "İlk incoming Speed doğru.");

Console.WriteLine();
Console.WriteLine("[2] Race pipeline ikinci frame delta packet testi");

var secondFrame = firstFrame with
{
    Sequence = 1001,
    PositionXM = 12.37,
    YawRad = 1.574,
    SpeedMps = 0.47,
    DistanceToTargetM = 17.80,
    RiskScore01 = 0.13
};

var secondPacket = senderPipeline.BuildOutgoingTelemetryPacket(
    secondFrame,
    priority: HydronomMessagePriority.High,
    extraFlags: HydronomMessageFlags.None,
    correlationId: "corr-pipeline-delta");

Require(secondPacket.ShouldSend, "İkinci packet gönderime hazır.");
Require(secondPacket.TelemetryFrame is not null, "İkinci packet telemetry frame taşıyor.");
Require(secondPacket.TelemetryFrame!.FieldMask != CompactTelemetryField.All, "İkinci packet full değil delta oldu.");
Require(secondPacket.TelemetryFrame.Has(CompactTelemetryField.PositionX), "Delta PositionX içeriyor.");
Require(secondPacket.TelemetryFrame.Has(CompactTelemetryField.Yaw), "Delta Yaw içeriyor.");
Require(secondPacket.TelemetryFrame.Has(CompactTelemetryField.Speed), "Delta Speed içeriyor.");
Require(secondPacket.Envelope!.Flags.HasFlag(HydronomMessageFlags.IsDelta), "İkinci packet delta flag taşıyor.");
Require(secondPacket.Envelope.Flags.HasFlag(HydronomMessageFlags.IsSigned), "İkinci packet signed flag taşıyor.");

var secondIncoming = receiverPipeline.ReadIncomingTelemetryPacket(secondPacket.EncodedMessage.Bytes);

Require(secondIncoming.Accepted, "İkinci packet receiver tarafından kabul edildi.");
Require(secondIncoming.TelemetryFrame is not null, "İkinci incoming telemetry çözüldü.");
Require(secondIncoming.TelemetryFrame!.Has(CompactTelemetryField.PositionX), "İkinci incoming PositionX içeriyor.");
Require(secondIncoming.TelemetryFrame.Has(CompactTelemetryField.Yaw), "İkinci incoming Yaw içeriyor.");
Require(secondIncoming.TelemetryFrame.Has(CompactTelemetryField.Speed), "İkinci incoming Speed içeriyor.");
Require(!secondIncoming.TelemetryFrame.Has(CompactTelemetryField.PositionY), "İkinci incoming PositionY içermiyor.");
RequireClose(secondIncoming.TelemetryFrame.PositionXM, 12.37, 0.001, "İkinci incoming PositionX doğru.");
RequireClose(secondIncoming.TelemetryFrame.YawRad, 1.574, 0.0001, "İkinci incoming Yaw doğru.");
RequireClose(secondIncoming.TelemetryFrame.SpeedMps, 0.47, 0.0001, "İkinci incoming Speed doğru.");

Console.WriteLine();
Console.WriteLine("[3] LowBandwidth değişmeyen frame skip testi");

var lowOptions = HydronomCommunicationPipelineOptions.LowBandwidth with
{
    SourceId = "runtime-main",
    TargetId = "ground-station",
    SessionId = "pipeline-low-session",
    HmacSecretKey = "hydronom-low-bandwidth-smoke-key"
};

var lowPipeline = new HydronomCommunicationPipeline(lowOptions);

var lowFirst = lowPipeline.BuildOutgoingTelemetryPacket(
    firstFrame,
    priority: HydronomMessagePriority.High,
    correlationId: "corr-low-first");

Require(lowFirst.ShouldSend, "LowBandwidth ilk frame gönderildi.");

var lowSecondSame = lowPipeline.BuildOutgoingTelemetryPacket(
    firstFrame with
    {
        Sequence = 1002
    },
    priority: HydronomMessagePriority.High,
    correlationId: "corr-low-same");

Require(!lowSecondSame.ShouldSend, "LowBandwidth değişmeyen frame'i göndermedi.");
Require(lowSecondSame.Reason == "NO_MEANINGFUL_TELEMETRY_CHANGE", "LowBandwidth skip nedeni doğru.");

Console.WriteLine();
Console.WriteLine("[4] Bozuk packet CRC/binary decode reddi testi");

var corrupted = secondPacket.EncodedMessage.Bytes.ToArray();
corrupted[^6] ^= 0x44;

var corruptedIncoming = receiverPipeline.ReadIncomingTelemetryPacket(corrupted);

Require(!corruptedIncoming.Accepted, "Bozuk packet reddedildi.");
Require(corruptedIncoming.Reason.StartsWith("BINARY_DECODE_FAILED", StringComparison.Ordinal), "Bozuk packet binary/CRC tarafında yakalandı.");

Console.WriteLine();
Console.WriteLine("[5] Replay packet reddi testi");

var replayIncoming = receiverPipeline.ReadIncomingTelemetryPacket(secondPacket.EncodedMessage.Bytes);

Require(!replayIncoming.Accepted, "Replay packet reddedildi.");
Require(replayIncoming.Reason == "REPLAY_DETECTED", "Replay nedeni doğru.");

Console.WriteLine();
Console.WriteLine("[6] Boyut raporu");

Console.WriteLine($"First full payload bytes      : {firstPacket.PayloadBytes} byte");
Console.WriteLine($"First full packet bytes       : {firstPacket.PacketBytes} byte");
Console.WriteLine($"Second delta payload bytes    : {secondPacket.PayloadBytes} byte");
Console.WriteLine($"Second delta packet bytes     : {secondPacket.PacketBytes} byte");

Require(secondPacket.PayloadBytes < firstPacket.PayloadBytes, "Delta payload full payload'dan küçük.");
Require(secondPacket.PacketBytes < firstPacket.PacketBytes, "Delta packet full packet'tan küçük.");

Console.WriteLine();
Console.WriteLine("======================================================");
Console.WriteLine(" COMMUNICATION PIPELINE SMOKE TEST PASSED ✅");
Console.WriteLine("======================================================");

static CompactTelemetryFrame CreateFrame(
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