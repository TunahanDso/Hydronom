using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.Scenarios.Mission;

Console.WriteLine("=== Hydronom Teknofest Advanced Full Scenario Smoke Test ===");

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
        "teknofest_2026_advanced_full"));

Console.WriteLine($"Scenario package: {scenarioPath}");

if (!Directory.Exists(scenarioPath))
{
    throw new DirectoryNotFoundException($"Scenario package not found: {scenarioPath}");
}

var loader = new ScenarioLoader();
var scenario = await loader.LoadAsync(scenarioPath);

Console.WriteLine();
Console.WriteLine("Loaded scenario:");
Console.WriteLine($"  Id          : {scenario.Id}");
Console.WriteLine($"  Name        : {scenario.Name}");
Console.WriteLine($"  Family      : {scenario.ScenarioFamily}");
Console.WriteLine($"  Vehicle     : {scenario.VehicleId}");
Console.WriteLine($"  Platform    : {scenario.VehiclePlatform}");
Console.WriteLine($"  Objects     : {scenario.Objects.Count}");
Console.WriteLine($"  Objectives  : {scenario.Objectives.Count}");
Console.WriteLine($"  Tags        : {scenario.Tags.Count}");

Require(scenario.Id == "teknofest_2026_advanced_full", "Scenario id mismatch.");
Require(scenario.Objects.Count >= 30, "Expected rich Teknofest world object set.");
Require(scenario.Objectives.Count >= 16, "Expected full two-stage objective chain.");

Require(
    scenario.Tags.TryGetValue("scenario.package", out var packageTag) &&
    string.Equals(packageTag, "true", StringComparison.OrdinalIgnoreCase),
    "Expected scenario.package=true tag.");

Require(
    scenario.Tags.TryGetValue("scenario.profileAware", out var profileAwareTag) &&
    string.Equals(profileAwareTag, "true", StringComparison.OrdinalIgnoreCase),
    "Expected scenario.profileAware=true tag.");

var requiredObjectKinds = new[]
{
    "guidance_board",
    "track_stripe",
    "mini_rov_drop_zone",
    "pipe_entry",
    "pipe_segment",
    "clue_marker",
    "gate_left",
    "gate_right",
    "controlled_zone",
    "no_go_zone",
    "finish_zone"
};

foreach (var kind in requiredObjectKinds)
{
    var count = scenario.Objects.Count(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"  Object kind {kind,-22}: {count}");
    Require(count > 0, $"Expected at least one object kind={kind}");
}

var missingTargetObjectives = scenario.Objectives
    .Where(x => string.IsNullOrWhiteSpace(x.TargetObjectId))
    .Select(x => x.Id)
    .ToArray();

Require(
    missingTargetObjectives.Length == 0,
    $"All objectives should have TargetObjectId. Missing={string.Join(", ", missingTargetObjectives)}");

var adapter = new ScenarioMissionAdapter();
var plan = adapter.BuildPlan(scenario);

Console.WriteLine();
Console.WriteLine("Mission plan:");
Console.WriteLine($"  ScenarioId  : {plan.ScenarioId}");
Console.WriteLine($"  Targets     : {plan.Targets.Count}");
Console.WriteLine($"  FirstTarget : {plan.FirstTarget?.ObjectiveId ?? "null"}");

Require(plan.HasTargets, "Mission plan should have targets.");
Require(plan.Targets.Count == scenario.Objectives.Count, "Every objective should become a mission target.");
Require(plan.Targets.Any(x => x.Kind == "pipe_entry"), "Mission plan should include pipe_entry target.");
Require(plan.Targets.Any(x => x.Kind == "clue_marker"), "Mission plan should include clue_marker target.");
Require(plan.Targets.Any(x => x.Kind == "controlled_zone"), "Mission plan should include controlled_zone target.");

var routeTask = adapter.ToRouteTaskDefinition(plan);

Console.WriteLine();
Console.WriteLine("Route task:");
Console.WriteLine($"  Name      : {routeTask.Name}");
Console.WriteLine($"  Waypoints : {routeTask.Waypoints.Count}");

Require(routeTask.Waypoints.Count == plan.Targets.Count, "Route task waypoint count should match plan targets.");

Console.WriteLine();
Console.WriteLine("TEKNOFEST_ADVANCED_FULL_SCENARIO_SMOKE_TEST_OK");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
