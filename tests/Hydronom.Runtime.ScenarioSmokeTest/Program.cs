using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.Scenarios.Execution;
using Hydronom.Runtime.Scenarios.Telemetry;
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
Console.WriteLine($"Name             : {scenario.Name}");
Console.WriteLine($"Objects          : {scenario.Objects.Count}");
Console.WriteLine($"Objectives       : {scenario.Objectives.Count}");
Console.WriteLine($"Fault injections : {scenario.FaultInjections.Count}");

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

RunSingleEvaluationSmokeTest(scenario, runner);
RunTimelineEvaluationSmokeTest(scenario, runner);
var executionResult = RunKinematicExecutionSmokeTest(scenario);
RunScenarioExecutionTelemetrySmokeTest(executionResult);

Console.WriteLine();
Console.WriteLine("=== Scenario smoke test passed ===");

static void RunSingleEvaluationSmokeTest(
    Hydronom.Core.Scenarios.Models.ScenarioDefinition scenario,
    RuntimeScenarioTestRunner runner)
{
    var now = DateTime.UtcNow;

    var nearFirstWaypoint = new RuntimeScenarioVehicleState
    {
        VehicleId = scenario.VehicleId,
        TimestampUtc = now,
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
            StartedUtc = now.AddSeconds(-10),
            TimestampUtc = now
        });

    Console.WriteLine();
    Console.WriteLine("Single evaluation report:");
    PrintReport(report);

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

    if (!string.Equals(report.FinalObjectiveId, "reach_wp_2", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected next/final objective to be reach_wp_2, got {report.FinalObjectiveId}.");
    }
}

static void RunTimelineEvaluationSmokeTest(
    Hydronom.Core.Scenarios.Models.ScenarioDefinition scenario,
    RuntimeScenarioTestRunner runner)
{
    var start = DateTime.UtcNow;

    var timeline = new List<RuntimeScenarioVehicleState>
    {
        new()
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = start,
            X = 0.0,
            Y = 0.0,
            Z = 0.0,
            YawDeg = 0.0
        },
        new()
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = start.AddSeconds(15),
            X = 12.0,
            Y = 4.0,
            Z = 0.0,
            YawDeg = 20.0
        },
        new()
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = start.AddSeconds(30),
            X = 24.0,
            Y = -4.0,
            Z = 0.0,
            YawDeg = -20.0
        },
        new()
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = start.AddSeconds(45),
            X = 36.0,
            Y = 4.0,
            Z = 0.0,
            YawDeg = 20.0
        },
        new()
        {
            VehicleId = scenario.VehicleId,
            TimestampUtc = start.AddSeconds(60),
            X = 48.0,
            Y = 0.0,
            Z = 0.0,
            YawDeg = 0.0
        }
    };

    var report = runner.RunTimelineEvaluation(
        scenario,
        timeline,
        new RuntimeScenarioTestOptions
        {
            StartedUtc = start,
            TimestampUtc = start,
            CurrentObjectiveId = "reach_wp_1"
        });

    Console.WriteLine();
    Console.WriteLine("Timeline evaluation report:");
    PrintReport(report);

    if (report.TotalObjectiveCount != scenario.Objectives.Count)
    {
        throw new InvalidOperationException("Timeline report objective count does not match scenario objective count.");
    }

    if (report.CompletedObjectiveCount != scenario.Objectives.Count)
    {
        throw new InvalidOperationException(
            $"Expected all objectives completed. Completed={report.CompletedObjectiveCount}, Total={scenario.Objectives.Count}");
    }

    if (!string.Equals(report.FinalStatus, Hydronom.Core.Scenarios.Runtime.ScenarioRunStatus.Completed, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected final status Completed, got {report.FinalStatus}.");
    }

    if (!string.Equals(report.JudgeStatus, Hydronom.Core.Scenarios.Judging.ScenarioJudgeStatus.Success, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected judge status Success, got {report.JudgeStatus}.");
    }

    if (report.Score < scenario.MinimumSuccessScore)
    {
        throw new InvalidOperationException(
            $"Expected score >= minimum success score. Score={report.Score}, Minimum={scenario.MinimumSuccessScore}");
    }

    if (Math.Abs(report.CompletionRatio - 1.0) > 0.0001)
    {
        throw new InvalidOperationException($"Expected completion ratio 1.0, got {report.CompletionRatio}");
    }
}

static ScenarioExecutionResult RunKinematicExecutionSmokeTest(
    Hydronom.Core.Scenarios.Models.ScenarioDefinition scenario)
{
    var executor = new ScenarioKinematicExecutor();

    var result = executor.Execute(
        scenario,
        new ScenarioExecutionOptions
        {
            DtSeconds = 0.2,
            CruiseSpeedMetersPerSecond = 1.5,
            VerticalSpeedMetersPerSecond = 0.5,
            MaxDurationSeconds = 300.0,
            KeepTimelineSamples = true,
            MaxStoredTimelineSamples = 5000
        });

    Console.WriteLine();
    Console.WriteLine("Kinematic execution report:");
    Console.WriteLine($"  RunId              : {result.RunId}");
    Console.WriteLine($"  FinalStatus        : {result.FinalStatus}");
    Console.WriteLine($"  IsSuccess          : {result.IsSuccess}");
    Console.WriteLine($"  IsTimedOut         : {result.IsTimedOut}");
    Console.WriteLine($"  SimElapsedSeconds  : {result.SimulatedElapsedSeconds}");
    Console.WriteLine($"  TickCount          : {result.TickCount}");
    Console.WriteLine($"  TimelineSamples    : {result.Timeline.Count}");
    PrintReport(result.Report);

    if (!result.IsSuccess)
    {
        throw new InvalidOperationException($"Expected kinematic execution success. Summary={result.Summary}");
    }

    if (result.IsTimedOut)
    {
        throw new InvalidOperationException("Kinematic execution should not time out.");
    }

    if (result.Timeline.Count == 0)
    {
        throw new InvalidOperationException("Expected kinematic executor to keep timeline samples.");
    }

    if (result.Report.CompletedObjectiveCount != scenario.Objectives.Count)
    {
        throw new InvalidOperationException(
            $"Expected kinematic executor to complete all objectives. Completed={result.Report.CompletedObjectiveCount}, Total={scenario.Objectives.Count}");
    }

    if (!string.Equals(result.Report.FinalStatus, Hydronom.Core.Scenarios.Runtime.ScenarioRunStatus.Completed, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected report final status Completed, got {result.Report.FinalStatus}.");
    }

    if (!string.Equals(result.Report.JudgeStatus, Hydronom.Core.Scenarios.Judging.ScenarioJudgeStatus.Success, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected report judge status Success, got {result.Report.JudgeStatus}.");
    }

    return result;
}

static void RunScenarioExecutionTelemetrySmokeTest(ScenarioExecutionResult executionResult)
{
    var projector = new ScenarioExecutionTelemetryProjector();

    var timelineSummaries = projector.ProjectTimeline(executionResult);
    var finalSummary = projector.ProjectFinalSummary(executionResult);

    Console.WriteLine();
    Console.WriteLine("Scenario execution telemetry report:");
    Console.WriteLine($"  TimelineTelemetryCount : {timelineSummaries.Count}");
    Console.WriteLine($"  FinalRuntimeId         : {finalSummary.RuntimeId}");
    Console.WriteLine($"  FinalVehicleId         : {finalSummary.VehicleId}");
    Console.WriteLine($"  FinalOverallHealth     : {finalSummary.OverallHealth}");
    Console.WriteLine($"  FinalHasState          : {finalSummary.HasState}");
    Console.WriteLine($"  FinalStateX            : {finalSummary.StateX}");
    Console.WriteLine($"  FinalStateY            : {finalSummary.StateY}");
    Console.WriteLine($"  FinalStateZ            : {finalSummary.StateZ}");
    Console.WriteLine($"  FinalYawDeg            : {finalSummary.StateYawDeg}");
    Console.WriteLine($"  FinalSummary           : {finalSummary.Summary}");

    if (timelineSummaries.Count == 0)
    {
        throw new InvalidOperationException("Expected timeline telemetry summaries.");
    }

    if (!string.Equals(finalSummary.RuntimeId, "hydronom_scenario_executor", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected RuntimeId hydronom_scenario_executor, got {finalSummary.RuntimeId}.");
    }

    if (!finalSummary.HasState)
    {
        throw new InvalidOperationException("Expected final telemetry summary to contain state.");
    }

    if (!string.Equals(finalSummary.OverallHealth, "Healthy", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected final telemetry health Healthy, got {finalSummary.OverallHealth}.");
    }

    if (Math.Abs(finalSummary.StateX - executionResult.Report.FinalVehicleX) > 0.0001)
    {
        throw new InvalidOperationException("Final telemetry StateX does not match report final X.");
    }

    if (Math.Abs(finalSummary.StateY - executionResult.Report.FinalVehicleY) > 0.0001)
    {
        throw new InvalidOperationException("Final telemetry StateY does not match report final Y.");
    }

    if (Math.Abs(finalSummary.StateZ - executionResult.Report.FinalVehicleZ) > 0.0001)
    {
        throw new InvalidOperationException("Final telemetry StateZ does not match report final Z.");
    }

    var first = timelineSummaries[0];
    var last = timelineSummaries[^1];

    if (!first.HasState || !last.HasState)
    {
        throw new InvalidOperationException("Expected first and last timeline telemetry summaries to contain state.");
    }

    if (last.AcceptedStateUpdateCount < first.AcceptedStateUpdateCount)
    {
        throw new InvalidOperationException("Telemetry accepted state update count should be monotonic.");
    }
}

static void PrintReport(Hydronom.Core.Scenarios.Reports.ScenarioRunReport report)
{
    Console.WriteLine($"  ReportRunId        : {report.RunId}");
    Console.WriteLine($"  FinalStatus        : {report.FinalStatus}");
    Console.WriteLine($"  JudgeStatus        : {report.JudgeStatus}");
    Console.WriteLine($"  Score              : {report.Score}");
    Console.WriteLine($"  Penalty            : {report.Penalty}");
    Console.WriteLine($"  NetScore           : {report.NetScore}");
    Console.WriteLine($"  CompletionRatio    : {report.CompletionRatio}");
    Console.WriteLine($"  CompletedObjectives: {report.CompletedObjectiveCount}/{report.TotalObjectiveCount}");
    Console.WriteLine($"  FinalObjective     : {report.FinalObjectiveId}");
    Console.WriteLine($"  Summary            : {report.Summary}");
}