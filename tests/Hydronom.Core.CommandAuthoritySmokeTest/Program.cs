using System.Text;
using Hydronom.Core.Communication.Commands;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine(" HYDRONOM CORE COMMAND AUTHORITY SMOKE TEST");
Console.WriteLine("=================================================");

var racePolicy = HydronomCommandAuthorityPolicy.Race
    .WithKnownSource("operator-console", HydronomCommandAuthority.Operator)
    .WithKnownSource("observer-console", HydronomCommandAuthority.Observer)
    .WithKnownSource("emergency-console", HydronomCommandAuthority.EmergencyConsole)
    .WithKnownSource("runtime-main", HydronomCommandAuthority.AutonomousRuntime)
    .WithKnownSource("ground-station", HydronomCommandAuthority.GroundStation);

var validator = new HydronomCommandAuthorityValidator(racePolicy);

Console.WriteLine();
Console.WriteLine("[1] Operator Arm verebilir testi");

var operatorArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    sequence: 1,
    operatorId: "operator-001",
    reason: "Operator aracı göreve hazırlıyor.");

var operatorArmDecision = validator.Validate(operatorArm);

Require(operatorArmDecision.Allowed, "Operator Arm komutu verebilir.");
Require(operatorArmDecision.Reason == "AUTHORITY_ALLOWED", "Operator Arm final allow reason doğru.");

Console.WriteLine();
Console.WriteLine("[2] Observer Arm veremez testi");

var observerArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Observer,
    sourceId: "observer-console",
    sequence: 2,
    operatorId: "observer-001",
    reason: "Observer yanlışlıkla arm deniyor.");

var observerArmDecision = validator.Validate(observerArm);

Require(!observerArmDecision.Allowed, "Observer Arm komutu veremez.");
Require(
    observerArmDecision.Reason is "OBSERVER_STATUS_ONLY" or "COMMAND_NOT_ALLOWED_FOR_AUTHORITY",
    $"Observer Arm doğru sebeple reddedildi. Reason={observerArmDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[3] Observer RequestStatus verebilir testi");

var observerStatus = CreateCommand(
    kind: HydronomCommandKind.RequestStatus,
    authority: HydronomCommandAuthority.Observer,
    sourceId: "observer-console",
    sequence: 3,
    operatorId: "",
    reason: "Observer durum istiyor.",
    safetyCritical: false);

var observerStatusDecision = validator.Validate(observerStatus);

Require(observerStatusDecision.Allowed, "Observer RequestStatus verebilir.");

Console.WriteLine();
Console.WriteLine("[4] EmergencyConsole EmergencyStop verebilir testi");

var emergencyStop = CreateCommand(
    kind: HydronomCommandKind.EmergencyStop,
    authority: HydronomCommandAuthority.EmergencyConsole,
    sourceId: "emergency-console",
    sequence: 4,
    operatorId: "safety-001",
    reason: "Acil durdurma testi.");

var emergencyDecision = validator.Validate(emergencyStop);

Require(emergencyDecision.Allowed, "EmergencyConsole EmergencyStop verebilir.");

Console.WriteLine();
Console.WriteLine("[5] EmergencyConsole MissionCommand veremez testi");

var emergencyMission = CreateCommand(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.EmergencyConsole,
    sourceId: "emergency-console",
    sequence: 5,
    operatorId: "safety-001",
    reason: "Emergency console görev başlatmayı deniyor.");

var emergencyMissionDecision = validator.Validate(emergencyMission);

Require(!emergencyMissionDecision.Allowed, "EmergencyConsole MissionCommand veremez.");
Require(
    emergencyMissionDecision.Reason == "COMMAND_NOT_ALLOWED_FOR_AUTHORITY",
    $"EmergencyConsole MissionCommand izin listesinde yakalandı. Reason={emergencyMissionDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[6] AutonomousRuntime Arm veremez testi");

var runtimeArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.AutonomousRuntime,
    sourceId: "runtime-main",
    sequence: 6,
    operatorId: "",
    reason: "Runtime kendi kendine arm deniyor.");

var runtimeArmDecision = validator.Validate(runtimeArm);

Require(!runtimeArmDecision.Allowed, "AutonomousRuntime Arm veremez.");
Require(
    runtimeArmDecision.Reason is "COMMAND_NOT_ALLOWED_FOR_AUTHORITY" or "AUTONOMOUS_RUNTIME_ARM_DISARM_NOT_ALLOWED",
    $"AutonomousRuntime Arm doğru sebeple reddedildi. Reason={runtimeArmDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[7] AutonomousRuntime EmergencyStop veremez testi");

var runtimeEstop = CreateCommand(
    kind: HydronomCommandKind.EmergencyStop,
    authority: HydronomCommandAuthority.AutonomousRuntime,
    sourceId: "runtime-main",
    sequence: 7,
    operatorId: "",
    reason: "Runtime EStop deniyor.");

var runtimeEstopDecision = validator.Validate(runtimeEstop);

Require(!runtimeEstopDecision.Allowed, "AutonomousRuntime EmergencyStop veremez.");
Require(
    runtimeEstopDecision.Reason is "COMMAND_NOT_ALLOWED_FOR_AUTHORITY" or "AUTONOMOUS_RUNTIME_ESTOP_NOT_ALLOWED",
    $"AutonomousRuntime EStop doğru sebeple reddedildi. Reason={runtimeEstopDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[8] GroundStation MissionCommand verebilir testi");

var groundMission = CreateCommand(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.GroundStation,
    sourceId: "ground-station",
    sequence: 8,
    operatorId: "",
    reason: "Ground station görev başlatıyor.");

var groundMissionDecision = validator.Validate(groundMission);

Require(groundMissionDecision.Allowed, "GroundStation MissionCommand verebilir.");

Console.WriteLine();
Console.WriteLine("[9] KnownSource authority mismatch testi");

var mismatchCommand = CreateCommand(
    kind: HydronomCommandKind.MissionCommand,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "ground-station",
    sequence: 9,
    operatorId: "operator-001",
    reason: "GroundStation kendini Operator gibi gösteriyor.");

var mismatchDecision = validator.Validate(mismatchCommand);

Require(!mismatchDecision.Allowed, "KnownSource authority mismatch reddedildi.");
Require(
    mismatchDecision.Reason == "SOURCE_AUTHORITY_MISMATCH",
    $"KnownSource authority mismatch doğru sebeple reddedildi. Reason={mismatchDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[10] SafetyCritical Reason yoksa reddedilir testi");

var noReasonArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    sequence: 10,
    operatorId: "operator-001",
    reason: "");

var noReasonDecision = validator.Validate(noReasonArm);

Require(!noReasonDecision.Allowed, "SafetyCritical reason boşsa reddedildi.");
Require(
    noReasonDecision.Reason == "SAFETY_CRITICAL_REASON_REQUIRED",
    $"SafetyCritical reason kuralı doğru çalıştı. Reason={noReasonDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[11] OperatorId yoksa Operator komutu reddedilir testi");

var noOperatorIdArm = CreateCommand(
    kind: HydronomCommandKind.Arm,
    authority: HydronomCommandAuthority.Operator,
    sourceId: "operator-console",
    sequence: 11,
    operatorId: "",
    reason: "OperatorId eksik testi.");

var noOperatorIdDecision = validator.Validate(noOperatorIdArm);

Require(!noOperatorIdDecision.Allowed, "OperatorId boşsa Operator komutu reddedildi.");
Require(
    noOperatorIdDecision.Reason == "OPERATOR_ID_REQUIRED",
    $"OperatorId kuralı doğru çalıştı. Reason={noOperatorIdDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[12] Trusted source listesinde olmayan kaynak reddedilir testi");

var untrustedCommand = CreateCommand(
    kind: HydronomCommandKind.RequestStatus,
    authority: HydronomCommandAuthority.Observer,
    sourceId: "unknown-console",
    sequence: 12,
    operatorId: "",
    reason: "Bilinmeyen kaynak status istiyor.",
    safetyCritical: false);

var untrustedDecision = validator.Validate(untrustedCommand);

Require(!untrustedDecision.Allowed, "Trusted source listesinde olmayan kaynak reddedildi.");
Require(
    untrustedDecision.Reason == "SOURCE_NOT_TRUSTED",
    $"Trusted source kuralı doğru çalıştı. Reason={untrustedDecision.Reason}");

Console.WriteLine();
Console.WriteLine("[13] Development developer override testi");

var devPolicy = HydronomCommandAuthorityPolicy.Development;
var devValidator = new HydronomCommandAuthorityValidator(devPolicy);

var devCustom = CreateCommand(
    kind: HydronomCommandKind.Custom,
    authority: HydronomCommandAuthority.Developer,
    sourceId: "dev-console",
    sequence: 13,
    operatorId: "",
    reason: "",
    safetyCritical: false);

var devDecision = devValidator.Validate(devCustom);

Require(devDecision.Allowed, "Development modda Developer override çalışıyor.");

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine(" COMMAND AUTHORITY SMOKE TEST PASSED ✅");
Console.WriteLine("=================================================");

static HydronomCommandFrame CreateCommand(
    HydronomCommandKind kind,
    HydronomCommandAuthority authority,
    string sourceId,
    ulong sequence,
    string operatorId,
    string reason,
    bool? safetyCritical = null)
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
        },
        safetyCritical: safetyCritical);
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