using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.Testing.Scenarios;
using Hydronom.Runtime.World.Runtime;

Console.WriteLine("=== Hydronom Runtime Scenario Smoke Test ===");

var scenarioPath = Path.GetFullPath(
    Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "src",
        "Hydronom.Runtime",
        "Scenarios",
        "Samples",
        "teknofest_2026_parkur_1_point_tracking.json"));

Console.WriteLine($"Scenario path: {scenarioPath}");

if (!File.Exists(scenarioPath))
{
    throw new FileNotFoundException("Scenario sample file not found.", scenarioPath);
}

var loader = new ScenarioLoader();
var scenario = await loader.LoadAsync(scenarioPath);

Console.WriteLine($"Loaded scenario: {scenario.Id}");
Console.WriteLine($"Name           : {scenario.Name}");
Console.WriteLine($"Objects        : {scenario.Objects.Count}");
Console.WriteLine($"Objectives     : {scenario.Objectives.Count}");
Console.WriteLine($"Fault injections: {scenario.FaultInjections.Count}");

if (scenario.Objects.Count == 0)
{
    throw new InvalidOperationException("Scenario objects should not be empty.");
}

if (scenario.Objectives.Count == 0)
{
    throw new InvalidOperationException("Scenario objectives should not be empty.");
}

var worldModel = new RuntimeWorldModel();
var binder = new ScenarioRuntimeBinder();
var boundObjects = binder.Bind(scenario, worldModel);

Console.WriteLine($"Bound world objects: {boundObjects.Count}");

if (boundObjects.Count != scenario.Objects.Count(x => x.IsActive))
{
    throw new InvalidOperationException("Bound object count does not match active scenario object count.");
}

var runner = new RuntimeScenarioTestRunner();

var nearFirstWaypoint = new RuntimeScenarioVehicleState
{
    VehicleId = scenario.VehicleId,
    TimestampUtc = DateTime.UtcNow,
    X = 12.0,
    Y = 4.0,
    Z = 0.0,
    YawDeg = 0.0,
    Vx = 0.0,
    Vy = 0.0,
    Vz = 0.0
};

var report = runner.RunSingleEvaluation(
    scenario,
    nearFirstWaypoint,
    new RuntimeScenarioTestOptions
    {
        CurrentObjectiveId = "reach_wp_1",
        StartedUtc = DateTime.UtcNow.AddSeconds(-10),
        TimestampUtc = DateTime.UtcNow
    });

Console.WriteLine();
Console.WriteLine("Scenario report:");
Console.WriteLine($"  RunId              : {report.RunId}");
Console.WriteLine($"  FinalStatus        : {report.FinalStatus}");
Console.WriteLine($"  JudgeStatus        : {report.JudgeStatus}");
Console.WriteLine($"  Score              : {report.Score}");
Console.WriteLine($"  Penalty            : {report.Penalty}");
Console.WriteLine($"  NetScore           : {report.NetScore}");
Console.WriteLine($"  CompletionRatio    : {report.CompletionRatio}");
Console.WriteLine($"  CompletedObjectives: {report.CompletedObjectiveCount}/{report.TotalObjectiveCount}");
Console.WriteLine($"  FinalObjective     : {report.FinalObjectiveId}");
Console.WriteLine($"  Summary            : {report.Summary}");

if (report.TotalObjectiveCount != scenario.Objectives.Count)
{
    throw new InvalidOperationException("Report objective count does not match scenario objective count.");
}

if (report.CompletedObjectiveCount < 1)
{
    throw new InvalidOperationException("Expected at least first waypoint objective to be completed.");
}

if (report.Score <= 0.0)
{
    throw new InvalidOperationException("Expected positive score after reaching first waypoint.");
}

Console.WriteLine();
Console.WriteLine("=== Scenario smoke test passed ===");