using System.Text;
using Hydronom.Core.Communication.Telemetry;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE TELEMETRY DELTA SMOKE TEST");
Console.WriteLine("=================================================");

var codec = new CompactTelemetryCodec();

var previous = CreateFrame(
    sequence: 100,
    x: 12.35,
    y: 8.42,
    z: -0.15,
    yaw: 1.571,
    speed: 0.43,
    batteryPercent: 73.4,
    distance: 17.86,
    risk: 0.12);

var currentSmallChange = previous with
{
    Sequence = 101,
    PositionXM = 12.354,
    YawRad = 1.5714,
    SpeedMps = 0.434,
    BatteryPercent = 73.45
};

var currentMeaningfulChange = previous with
{
    Sequence = 102,
    PositionXM = 12.37,
    YawRad = 1.574,
    SpeedMps = 0.47,
    BatteryPercent = 73.4,
    DistanceToTargetM = 17.80,
    RiskScore01 = 0.13
};

Console.WriteLine();
Console.WriteLine("[1] Previous yoksa full frame testi");

var defaultBuilder = new CompactTelemetryDeltaBuilder();

var firstDelta = defaultBuilder.BuildDelta(null, previous);

Require(firstDelta.FieldMask == CompactTelemetryField.All, "Previous yokken full frame üretildi.");

var firstBytes = codec.Encode(firstDelta);
Require(firstBytes.Length > 0, "First full delta encode edildi.");

Console.WriteLine();
Console.WriteLine("[2] Eşik altında küçük değişim testi");

var smallDelta = defaultBuilder.BuildDelta(previous, currentSmallChange);

Require(smallDelta.FieldMask == CompactTelemetryField.None, "Eşik altı küçük değişimler gönderilmedi.");
Require(!defaultBuilder.HasMeaningfulChange(previous, currentSmallChange), "HasMeaningfulChange false döndü.");

var smallBytes = codec.Encode(smallDelta);

Console.WriteLine();
Console.WriteLine("[3] Anlamlı değişim field mask testi");

var meaningfulDelta = defaultBuilder.BuildDelta(previous, currentMeaningfulChange);

Require(meaningfulDelta.Has(CompactTelemetryField.PositionX), "PositionX değişimi delta maskesine girdi.");
Require(meaningfulDelta.Has(CompactTelemetryField.Yaw), "Yaw değişimi delta maskesine girdi.");
Require(meaningfulDelta.Has(CompactTelemetryField.Speed), "Speed değişimi delta maskesine girdi.");
Require(meaningfulDelta.Has(CompactTelemetryField.DistanceToTarget), "Distance değişimi delta maskesine girdi.");
Require(meaningfulDelta.Has(CompactTelemetryField.RiskScore), "RiskScore değişimi delta maskesine girdi.");

Require(!meaningfulDelta.Has(CompactTelemetryField.PositionY), "PositionY değişmediği için delta maskesine girmedi.");
Require(!meaningfulDelta.Has(CompactTelemetryField.BatteryPercent), "BatteryPercent değişmediği için delta maskesine girmedi.");

Require(defaultBuilder.HasMeaningfulChange(previous, currentMeaningfulChange), "HasMeaningfulChange true döndü.");

var meaningfulBytes = codec.Encode(meaningfulDelta);
var meaningfulDecoded = codec.Decode(meaningfulBytes);

Require(meaningfulDecoded.Has(CompactTelemetryField.PositionX), "Encoded delta PositionX içeriyor.");
Require(meaningfulDecoded.Has(CompactTelemetryField.Yaw), "Encoded delta Yaw içeriyor.");
Require(meaningfulDecoded.Has(CompactTelemetryField.Speed), "Encoded delta Speed içeriyor.");
Require(!meaningfulDecoded.Has(CompactTelemetryField.PositionY), "Encoded delta PositionY içermiyor.");

RequireClose(meaningfulDecoded.PositionXM, 12.37, 0.001, "Delta PositionX doğru çözüldü.");
RequireClose(meaningfulDecoded.YawRad, 1.574, 0.0001, "Delta Yaw doğru çözüldü.");
RequireClose(meaningfulDecoded.SpeedMps, 0.47, 0.0001, "Delta Speed doğru çözüldü.");
RequireClose(meaningfulDecoded.DistanceToTargetM, 17.80, 0.001, "Delta Distance doğru çözüldü.");
RequireClose(meaningfulDecoded.RiskScore01, 0.13, 0.001, "Delta Risk doğru çözüldü.");

Console.WriteLine();
Console.WriteLine("[4] LowBandwidth delta hassasiyet testi");

var lowBandwidthBuilder = new CompactTelemetryDeltaBuilder(
    CompactTelemetryDeltaOptions.LowBandwidth);

var lowBandwidthDelta = lowBandwidthBuilder.BuildDelta(previous, currentMeaningfulChange);

Require(!lowBandwidthDelta.Has(CompactTelemetryField.PositionX), "LowBandwidth 2cm PositionX değişimini bastırdı.");
Require(!lowBandwidthDelta.Has(CompactTelemetryField.Speed), "LowBandwidth 0.04m/s Speed değişimini bastırdı.");
Require(!lowBandwidthDelta.Has(CompactTelemetryField.RiskScore), "LowBandwidth 0.01 Risk değişimini sınırda/bastırdı.");
Require(lowBandwidthDelta.Has(CompactTelemetryField.DistanceToTarget), "LowBandwidth 6cm Distance değişimini gönderdi.");

Console.WriteLine();
Console.WriteLine("[5] ForcedFields testi");

var forcedBuilder = new CompactTelemetryDeltaBuilder(
    CompactTelemetryDeltaOptions.Default with
    {
        ForcedFields =
            CompactTelemetryField.BatteryVoltage |
            CompactTelemetryField.BatteryPercent
    });

var forcedDelta = forcedBuilder.BuildDelta(previous, currentSmallChange);

Require(forcedDelta.Has(CompactTelemetryField.BatteryVoltage), "ForcedFields BatteryVoltage ekledi.");
Require(forcedDelta.Has(CompactTelemetryField.BatteryPercent), "ForcedFields BatteryPercent ekledi.");

Console.WriteLine();
Console.WriteLine("[6] Boyut karşılaştırması");

var fullBytes = codec.Encode(previous with
{
    FieldMask = CompactTelemetryField.All
});

Console.WriteLine($"Full compact telemetry       : {fullBytes.Length} byte");
Console.WriteLine($"First full delta             : {firstBytes.Length} byte");
Console.WriteLine($"No-change delta              : {smallBytes.Length} byte");
Console.WriteLine($"Meaningful delta             : {meaningfulBytes.Length} byte");

Require(meaningfulBytes.Length < fullBytes.Length, "Meaningful delta full compact telemetry'den küçük.");
Require(smallBytes.Length < meaningfulBytes.Length, "No-change delta meaningful delta'dan küçük.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" TELEMETRY DELTA SMOKE TEST PASSED ✅");
Console.WriteLine("=================================================");

static CompactTelemetryFrame CreateFrame(
    ulong sequence,
    double x,
    double y,
    double z,
    double yaw,
    double speed,
    double batteryPercent,
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
        PositionZM = z,

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
        BatteryPercent = batteryPercent,

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