using System.Reflection;
using System.Text.Json;
using Hydronom.Core.Domain;
using Hydronom.Runtime.Scenarios.Runtime;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=== Hydronom Runtime Ops Vehicle Profile Telemetry Smoke Test ===");

var runtimeAssembly = typeof(RuntimeScenarioSnapshot).Assembly;
var programType = runtimeAssembly.GetType("Program");

Assert(programType is not null, "Hydronom.Runtime Program type bulunamadı.");

var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

var snapshot = new RuntimeScenarioSnapshot
{
    Message = "ops-vehicle-profile-test",
    HasActiveScenario = true,
    IsRunning = true,
    ScenarioId = "vp4_test_scenario",
    ScenarioName = "VP4 Test Scenario",
    VehicleId = "hydronom-uuv-main",
    ScenarioVehicleId = "scenario-default-vehicle",

    VehicleProfileId = "hydronom_uuv_main_2026",
    VehiclePlatformKind = "UnderwaterVehicle",
    VehicleDisplayName = "Hydronom UUV Main 2026",
    VehicleProfileActive = true,
    VehicleIsUnderwater = true,
    VehicleIsMiniRov = false,
    VehicleHasThrusters = true,
    VehicleHasReverseAuthority = true,
    VehicleCanGenerateLateralForce = true,
    VehicleCanGenerateYawMoment = true,
    VehicleCapabilitySummary = "test capability summary",

    State = "Running",
    RunId = "run-vp4-test",
    CurrentObjectiveId = "obj-1",
    CompletedObjectiveCount = 0,
    TotalObjectiveCount = 3,
    LastDistanceToTargetMeters = 12.5,
    LastDistance3DToTargetMeters = 13.0,
    LastTickSummary = "tick ok",
    SessionSummary = "session ok",
    ActiveObjectiveTargetX = 10,
    ActiveObjectiveTargetY = 20,
    ActiveObjectiveTargetZ = -2,
    ActiveObjectiveToleranceMeters = 1.5,

    RoutePoints = new[]
    {
        new RuntimeScenarioRoutePoint
        {
            Id = "obj-1",
            Label = "Objective 1",
            ObjectiveId = "obj-1",
            Index = 0,
            X = 10,
            Y = 20,
            Z = -2,
            ToleranceMeters = 1.5,
            IsActive = true,
            IsCompleted = false
        }
    },

    WorldObjects = new[]
    {
        new RuntimeScenarioWorldObject
        {
            Id = "gate-1",
            Type = "gate",
            Kind = "gate",
            Name = "Test Gate",
            Label = "Gate 1",
            ObjectiveId = "obj-1",
            X = 10,
            Y = 20,
            Z = -2,
            Radius = 0.5,
            Width = 2.0,
            Height = 1.0,
            Color = "green",
            IsActive = true,
            IsGate = true,
            IsDetectable = true
        }
    }
};

var state = VehicleState.Zero;

var telemetryFrame = InvokePrivateStatic(
    programType!,
    "BuildRuntimeTelemetryFrame",
    snapshot,
    state,
    now,
    "hydronom-uuv-main");

var missionFrame = InvokePrivateStatic(
    programType!,
    "BuildRuntimeMissionStateFrame",
    snapshot,
    state,
    now,
    "hydronom-uuv-main");

var worldFrame = InvokePrivateStatic(
    programType!,
    "BuildRuntimeWorldObjectsFrame",
    snapshot,
    now,
    "hydronom-uuv-main");

AssertVehicleProfileBlock(telemetryFrame, "RuntimeTelemetry");
AssertVehicleProfileBlock(missionFrame, "RuntimeMissionState");
AssertVehicleProfileBlock(worldFrame, "RuntimeWorldObjects");

AssertJsonContains(worldFrame, "\"kind\":\"gate\"", "WorldObjects kind alanı x.Kind üzerinden gelmeli.");
AssertJsonContains(worldFrame, "\"isGate\":true", "WorldObjects isGate alanı taşınmalı.");
AssertJsonContains(worldFrame, "\"width\":2", "WorldObjects width alanı taşınmalı.");

Console.WriteLine(ToJson(telemetryFrame));
Console.WriteLine(ToJson(missionFrame));
Console.WriteLine(ToJson(worldFrame));

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("RUNTIME_OPS_VEHICLE_PROFILE_TELEMETRY_SMOKE_TEST_OK");
Console.ResetColor();

static object InvokePrivateStatic(Type type, string methodName, params object?[] args)
{
    var method = type.GetMethod(
        methodName,
        BindingFlags.NonPublic | BindingFlags.Static);

    Assert(method is not null, $"Method bulunamadı: {methodName}");

    var result = method!.Invoke(null, args);

    Assert(result is not null, $"Method null döndü: {methodName}");

    return result!;
}

static void AssertVehicleProfileBlock(object frame, string expectedType)
{
    var json = ToJson(frame);

    AssertJsonContains(frame, $"\"type\":\"{expectedType}\"", $"{expectedType} type yanlış.");
    AssertJsonContains(frame, "\"vehicleId\":\"hydronom-uuv-main\"", $"{expectedType} vehicleId yok.");
    AssertJsonContains(frame, "\"vehicleProfileId\":\"hydronom_uuv_main_2026\"", $"{expectedType} vehicleProfileId yok.");
    AssertJsonContains(frame, "\"vehiclePlatformKind\":\"UnderwaterVehicle\"", $"{expectedType} platform yok.");
    AssertJsonContains(frame, "\"vehicleDisplayName\":\"Hydronom UUV Main 2026\"", $"{expectedType} displayName yok.");
    AssertJsonContains(frame, "\"vehicleProfileActive\":true", $"{expectedType} vehicleProfileActive yok.");
    AssertJsonContains(frame, "\"vehicleIsUnderwater\":true", $"{expectedType} vehicleIsUnderwater yok.");
    AssertJsonContains(frame, "\"vehicleIsMiniRov\":false", $"{expectedType} vehicleIsMiniRov yok.");

    Assert(json.Contains("\"vehicleProfile\""), $"{expectedType} nested vehicleProfile bloğu yok.");
    Assert(json.Contains("\"capabilities\""), $"{expectedType} nested capabilities bloğu yok.");
    Assert(json.Contains("\"hasThrusters\":true"), $"{expectedType} hasThrusters yok.");
    Assert(json.Contains("\"hasReverseAuthority\":true"), $"{expectedType} hasReverseAuthority yok.");
    Assert(json.Contains("\"canGenerateLateralForce\":true"), $"{expectedType} canGenerateLateralForce yok.");
    Assert(json.Contains("\"canGenerateYawMoment\":true"), $"{expectedType} canGenerateYawMoment yok.");
}

static void AssertJsonContains(object frame, string expected, string message)
{
    var json = ToJson(frame);

    Assert(json.Contains(expected, StringComparison.Ordinal), message + $" Beklenen: {expected}");
}

static string ToJson(object value)
{
    return JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    });
}

static void Assert(bool condition, string message)
{
    if (condition)
        return;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();

    Environment.ExitCode = 1;
    throw new InvalidOperationException(message);
}