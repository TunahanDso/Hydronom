using System.Text.Json;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.World.Projection;
using Hydronom.Runtime.World.Runtime;

Console.WriteLine("=== Hydronom Runtime Scenario Smoke Test ===");
Console.WriteLine();

var scenarioPath = Path.Combine(
    Path.GetTempPath(),
    $"hydronom_scenario_smoke_{Guid.NewGuid():N}.json");

try
{
    var scenario = CreateScenario();

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    await File.WriteAllTextAsync(
        scenarioPath,
        JsonSerializer.Serialize(scenario, jsonOptions));

    Console.WriteLine("[1] Scenario JSON oluşturuldu");
    Console.WriteLine($"Path : {scenarioPath}");
    Console.WriteLine();

    var loader = new ScenarioLoader();
    var loadedScenario = await loader.LoadAsync(scenarioPath);

    Console.WriteLine("[2] ScenarioLoader ile yüklendi");
    Console.WriteLine($"Id      : {loadedScenario.Id}");
    Console.WriteLine($"Name    : {loadedScenario.Name}");
    Console.WriteLine($"Objects : {loadedScenario.Objects.Count}");
    Console.WriteLine();

    Require(
        loadedScenario.Id == "scenario_smoke_test",
        "Scenario Id doğru okunmalı.");

    Require(
        loadedScenario.Objects.Count == 5,
        "Scenario içinde 5 obje okunmalı.");

    var worldModel = new RuntimeWorldModel();
    var binder = new ScenarioRuntimeBinder();

    var boundObjects = binder.Bind(
        loadedScenario,
        worldModel,
        clearBeforeBind: true);

    Console.WriteLine("[3] ScenarioRuntimeBinder ile RuntimeWorldModel'e bind edildi");
    Console.WriteLine($"Bound objects : {boundObjects.Count}");
    Console.WriteLine($"World count   : {worldModel.Count}");
    Console.WriteLine();

    Require(
        boundObjects.Count == 5,
        "5 aktif obje bind edilmeli.");

    Require(
        worldModel.Count == 5,
        "RuntimeWorldModel içinde 5 obje olmalı.");

    var snapshot = worldModel.Snapshot();

    RequireKind(snapshot, "buoy", expectedCount: 2);
    RequireKind(snapshot, "dock", expectedCount: 1);
    RequireKind(snapshot, "gate", expectedCount: 1);
    RequireKind(snapshot, "no_go_zone", expectedCount: 1);

    var diagnostics = worldModel.GetDiagnostics();

    Console.WriteLine("[4] World diagnostics");
    Console.WriteLine($"Object count        : {diagnostics.ObjectCount}");
    Console.WriteLine($"Active object count : {diagnostics.ActiveObjectCount}");
    Console.WriteLine($"Blocking count      : {diagnostics.BlockingObjectCount}");
    Console.WriteLine($"Summary             : {diagnostics.Summary}");
    Console.WriteLine();

    Require(
        diagnostics.ObjectCount == 5,
        "Diagnostics object count 5 olmalı.");

    Require(
        diagnostics.ActiveObjectCount == 5,
        "Diagnostics active object count 5 olmalı.");

    Require(
        diagnostics.BlockingObjectCount >= 3,
        "En az buoy/no_go_zone objeleri blocking sayılmalı.");

    var projector = new RuntimeWorldTelemetryProjector();
    var telemetry = projector.Project(worldModel);

    Console.WriteLine("[5] RuntimeWorldTelemetryProjector telemetry üretti");
    Console.WriteLine($"Frame id         : {telemetry.FrameId}");
    Console.WriteLine($"World id         : {telemetry.WorldId}");
    Console.WriteLine($"Object count     : {telemetry.ObjectCount}");
    Console.WriteLine($"Visible count    : {telemetry.VisibleObjectCount}");
    Console.WriteLine($"Detectable count : {telemetry.DetectableObjectCount}");
    Console.WriteLine($"Summary          : {telemetry.Summary}");
    Console.WriteLine();

    Require(
        telemetry.ObjectCount == 5,
        "Telemetry object count 5 olmalı.");

    Require(
        telemetry.VisibleObjectCount == 5,
        "Telemetry visible object count 5 olmalı.");

    Require(
        telemetry.Objects.Any(x => x.ObjectId == "buoy_01"),
        "Telemetry içinde buoy_01 olmalı.");

    Require(
        telemetry.Objects.Any(x => x.ObjectId == "no_go_north" && x.Collidable),
        "Telemetry içinde no_go_north collidable olmalı.");

    Console.WriteLine("PASS: Scenario loading + runtime world binding + telemetry projection başarılı.");
}
finally
{
    if (File.Exists(scenarioPath))
    {
        File.Delete(scenarioPath);
    }
}

static ScenarioDefinition CreateScenario()
{
    return new ScenarioDefinition
    {
        Id = "scenario_smoke_test",
        Name = "Scenario Smoke Test Course",
        Description = "Bu test senaryosu Hydronom scenario/world loading temelini doğrular.",
        Version = "1.0.0",
        CoordinateFrame = "local_metric",
        Objects = new[]
        {
            new ScenarioWorldObjectDefinition
            {
                Id = "buoy_01",
                Kind = "buoy",
                Name = "First Buoy",
                Layer = "mission",
                X = 10.0,
                Y = 0.0,
                Z = 0.0,
                Radius = 0.5,
                IsBlocking = true,
                Tags = new Dictionary<string, string>
                {
                    ["role"] = "course_marker"
                }
            },
            new ScenarioWorldObjectDefinition
            {
                Id = "buoy_02",
                Kind = "buoy",
                Name = "Second Buoy",
                Layer = "mission",
                X = 20.0,
                Y = 5.0,
                Z = 0.0,
                Radius = 0.5,
                IsBlocking = true,
                Tags = new Dictionary<string, string>
                {
                    ["role"] = "course_marker"
                }
            },
            new ScenarioWorldObjectDefinition
            {
                Id = "dock_main",
                Kind = "dock",
                Name = "Main Dock",
                Layer = "mission",
                X = -5.0,
                Y = -4.0,
                Z = 0.0,
                Width = 4.0,
                Height = 2.0,
                YawDeg = 15.0,
                IsBlocking = false,
                Tags = new Dictionary<string, string>
                {
                    ["role"] = "start_area"
                }
            },
            new ScenarioWorldObjectDefinition
            {
                Id = "gate_01",
                Kind = "gate",
                Name = "Navigation Gate",
                Layer = "navigation",
                X = 30.0,
                Y = 0.0,
                Z = 0.0,
                Width = 5.0,
                Height = 1.0,
                IsBlocking = false,
                Tags = new Dictionary<string, string>
                {
                    ["role"] = "checkpoint"
                }
            },
            new ScenarioWorldObjectDefinition
            {
                Id = "no_go_north",
                Kind = "no_go_zone",
                Name = "North No-Go Zone",
                Layer = "safety",
                X = 15.0,
                Y = 15.0,
                Z = 0.0,
                Width = 8.0,
                Height = 8.0,
                IsBlocking = true,
                Tags = new Dictionary<string, string>
                {
                    ["risk"] = "restricted"
                }
            }
        }
    };
}

static void RequireKind(
    IReadOnlyList<Hydronom.Core.World.Models.HydronomWorldObject> objects,
    string kind,
    int expectedCount)
{
    var actual = objects.Count(x => x.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));

    Require(
        actual == expectedCount,
        $"Kind={kind} için beklenen count={expectedCount}, actual={actual}");
}

static void Require(bool condition, string message)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {message}");
        return;
    }

    Console.WriteLine($"FAIL: {message}");
    throw new InvalidOperationException(message);
}