using Hydronom.Runtime.Scenarios;
using Hydronom.Runtime.Scenarios.Execution;
using Hydronom.Runtime.Scenarios.Replay;
using Hydronom.Runtime.Scenarios.Telemetry;
using Hydronom.Runtime.Telemetry;

Console.WriteLine("=== Hydronom Scenario TCP Replay Smoke Test ===");

var host = Environment.GetEnvironmentVariable("HYDRONOM_SCENARIO_REPLAY_HOST");
var portText = Environment.GetEnvironmentVariable("HYDRONOM_SCENARIO_REPLAY_PORT");

host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 5055;

Console.WriteLine($"TCP server: {host}:{port}");

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

using var cts = new CancellationTokenSource();

var server = new TcpJsonServer(
    host: host,
    port: port,
    onFrame: null,
    onCapability: null,
    logRawFrames: false,
    sendDefaultSubscribe: false);

var serverTask = Task.Run(() => server.StartAsync(cts.Token));

Console.WriteLine("TCP server started.");
Console.WriteLine("Start HydronomOps.Gateway now if it is not already running.");
Console.WriteLine("Waiting 4 seconds for Gateway/runtime client connection...");

await Task.Delay(4000);

var loader = new ScenarioLoader();
var scenario = await loader.LoadAsync(scenarioPath);

Console.WriteLine($"Loaded scenario: {scenario.Id}");
Console.WriteLine($"Objects          : {scenario.Objects.Count}");
Console.WriteLine($"Objectives       : {scenario.Objectives.Count}");

var executor = new ScenarioKinematicExecutor();

var executionResult = executor.Execute(
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
Console.WriteLine("Scenario execution:");
Console.WriteLine($"  RunId              : {executionResult.RunId}");
Console.WriteLine($"  FinalStatus        : {executionResult.FinalStatus}");
Console.WriteLine($"  IsSuccess          : {executionResult.IsSuccess}");
Console.WriteLine($"  TimelineSamples    : {executionResult.Timeline.Count}");
Console.WriteLine($"  CompletedObjectives: {executionResult.Report.CompletedObjectiveCount}/{executionResult.Report.TotalObjectiveCount}");
Console.WriteLine($"  Score              : {executionResult.Report.Score}");

if (!executionResult.IsSuccess)
{
    throw new InvalidOperationException($"Scenario execution failed. Summary={executionResult.Summary}");
}

var tcpPublisher = new TcpRuntimeTelemetryPublisher(server);
var projector = new ScenarioExecutionTelemetryProjector();

var replayPublisher = new ScenarioTelemetryReplayPublisher(
    projector,
    tcpPublisher);

Console.WriteLine();
Console.WriteLine("Publishing scenario telemetry replay over TCP...");

var replayResult = await replayPublisher.PublishTimelineAsync(
    executionResult,
    new ScenarioTelemetryReplayOptions
    {
        DelayBetweenFramesMs = 30,
        FrameStride = 2,
        PublishFinalSummary = true
    },
    cts.Token);

Console.WriteLine();
Console.WriteLine("TCP replay publish result:");
Console.WriteLine($"  Published             : {replayResult.Published}");
Console.WriteLine($"  TimelineFrameCount    : {replayResult.TimelineFrameCount}");
Console.WriteLine($"  PublishedFrameCount   : {replayResult.PublishedFrameCount}");
Console.WriteLine($"  SkippedFrameCount     : {replayResult.SkippedFrameCount}");
Console.WriteLine($"  PublishedFinalSummary : {replayResult.PublishedFinalSummary}");
Console.WriteLine($"  Summary               : {replayResult.Summary}");

if (!replayResult.Published)
{
    throw new InvalidOperationException("Expected TCP replay to publish at least one frame.");
}

Console.WriteLine();
Console.WriteLine("Replay published. Check Gateway:");
Console.WriteLine("  Invoke-RestMethod http://localhost:5186/runtime/telemetry-summary | ConvertTo-Json -Depth 10");
Console.WriteLine("  Invoke-RestMethod http://localhost:5186/runtime/diagnostics | ConvertTo-Json -Depth 10");

Console.WriteLine();
Console.WriteLine("Keeping TCP server alive for 15 seconds so Gateway can receive the final frames...");
await Task.Delay(15000);

cts.Cancel();

try
{
    await serverTask;
}
catch (OperationCanceledException)
{
}

Console.WriteLine("=== Scenario TCP replay smoke test completed ===");