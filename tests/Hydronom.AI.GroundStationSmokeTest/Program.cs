using Hydronom.GroundStation.AI;
using System.Net.Http;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var useLocalLlama =
    args.Any(a => string.Equals(a, "--local", StringComparison.OrdinalIgnoreCase)) ||
    string.Equals(Environment.GetEnvironmentVariable("HYDRONOM_AI_PROVIDER"), "local", StringComparison.OrdinalIgnoreCase);

var model =
    Environment.GetEnvironmentVariable("HYDRONOM_AI_MODEL") ??
    "qwen2.5:3b-instruct";

var endpoint =
    Environment.GetEnvironmentVariable("HYDRONOM_AI_ENDPOINT") ??
    "http://localhost:11434/api/generate";

var gatewaySnapshotUrl =
    Environment.GetEnvironmentVariable("HYDRONOM_GATEWAY_SNAPSHOT_URL") ??
    "http://localhost:5186/snapshot";

using var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(300)
};

GroundAiMissionAssistant assistant;

if (useLocalLlama)
{
    assistant = GroundAiMissionAssistant.CreateLocalLlama(
        http,
        endpoint,
        model);
}
else
{
    assistant = GroundAiMissionAssistant.CreateFake();
}

Console.WriteLine("=== HYDRONOM AI GROUND STATION SMOKE TEST ===");
Console.WriteLine();
Console.WriteLine($"Provider mode: {(useLocalLlama ? "local-llama" : "fake")}");
Console.WriteLine($"Model: {model}");
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Gateway snapshot: {gatewaySnapshotUrl}");
Console.WriteLine();

try
{
    var snapshotJson = await TryReadGatewaySnapshotAsync(http, gatewaySnapshotUrl);

    GroundAiRuntimeContext? liveContext = null;

    if (!string.IsNullOrWhiteSpace(snapshotJson))
    {
        liveContext = GroundAiRuntimeContextBuilder.FromGatewaySnapshotJson(snapshotJson);

        Console.WriteLine("LIVE GATEWAY SNAPSHOT CONTEXT BUILT");
        Console.WriteLine("-----------------------------------");
        Console.WriteLine(liveContext.OperatorSummary);
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine("Gateway snapshot okunamadı; fallback test context kullanılacak.");
        Console.WriteLine();
    }

    var fallbackStory =
        "Tekne başlangıç noktasından çıksın, sağdaki kırmızı şamandırayı güvenli mesafeden geçsin, " +
        "kapıdan geçsin ve bitiş bölgesinde kontrollü şekilde durup görevi tamamlasın. " +
        "Rüzgar varmış gibi dikkatli ve güvenli ilerlesin.";

    var fallbackRuntimeContext =
        "RuntimeId=hydronom_runtime; Mode=Simulation; Armed=false; Scenario=teknofest_surface_gate; " +
        "LoopHz=50; ControlHz=50; ActuatorCommandHz=100; LastCommandSafety=Allowed; " +
        "No direct AI runtime command is allowed.";

    var fallbackVehicleContext =
        "VehicleId=hydronom-main; Type=SurfaceVessel; Navigation=Waypoint; " +
        "Sensors=sim_imu, sim_gps; Actuators=4 thrusters; CanReverse=false; " +
        "AI may only suggest and explain.";

    var fallbackMissionState =
        "Mission active=false; CurrentPhase=PreStart; Route=Start -> RedBuoyPass -> Gate -> Finish; " +
        "Progress=0%; LastKnownDistanceToFirstTarget=12.5m; OperatorApprovalRequired=true.";

    var fallbackVehicleState =
        "Pose x=0.0 y=0.0 z=0.0 yaw=0deg; Speed=0.0m/s; Battery=15.4V; " +
        "Health=Nominal; ControlMode=Disarmed.";

    var fallbackSensorState =
        "IMU healthy=true ageMs=12; GPS healthy=true ageMs=180 fix=simulated hdop=0.9; " +
        "Depth unavailable for surface mission; Camera unavailable.";

    var fallbackActuatorState =
        "Thrusters=4; AllocationStatus=OK; ReverseClamp=0; Saturation=0%; LastCommands=0,0,0,0.";

    var fallbackSafetyState =
        "EmergencyStop=false; SafetyLimiter=Active; GroundCommandSafetyGate=Enabled; " +
        "AI direct command authority=false; Human approval required before mission start.";

    var fallbackRecentEvents =
        "T-30s runtime started; T-20s gateway connected; T-10s scenario loaded; T-5s AI smoke test requested.";

    var fallbackPerformanceState =
        "Runtime RAM approx 190MB idle; Gateway RAM approx 175MB idle; " +
        "Previous local AI qwen2.5:3b-instruct latency around 33s for mission planning.";

    var story = fallbackStory;

    var runtimeContext = liveContext?.RuntimeContext ?? fallbackRuntimeContext;
    var missionState = liveContext?.MissionState ?? fallbackMissionState;
    var vehicleState = liveContext?.VehicleState ?? fallbackVehicleState;
    var sensorState = liveContext?.SensorState ?? fallbackSensorState;
    var actuatorState = liveContext?.ActuatorState ?? fallbackActuatorState;
    var safetyState = liveContext?.SafetyState ?? fallbackSafetyState;
    var worldState = liveContext?.WorldState ?? "World state unavailable.";
    var recentEvents = fallbackRecentEvents;
    var performanceState = liveContext?.OperatorSummary ?? fallbackPerformanceState;

    await RunCaseAsync(
        "LIVE RUNTIME ANALYSIS",
        () => assistant.AnalyzeRuntimeAsync(
            runtimeContext,
            missionState,
            vehicleState,
            sensorState,
            actuatorState,
            safetyState,
            CancellationToken.None));

    await RunCaseAsync(
        "LIVE MISSION SUMMARY",
        () => assistant.SummarizeMissionAsync(
            missionState,
            recentEvents,
            performanceState,
            safetyState,
            CancellationToken.None));

    await RunCaseAsync(
        "LIVE FAULT / CONCERN EXPLANATION",
        () => assistant.ExplainFaultAsync(
            liveContext?.OperatorSummary ??
            "Vehicle speed remains zero because vehicle is DISARMED and operator approval has not been given.",
            runtimeContext,
            sensorState,
            actuatorState,
            safetyState,
            CancellationToken.None));

    await RunCaseAsync(
        "LIVE RISK ASSESSMENT",
        () => assistant.AssessRiskAsync(
            story,
            vehicleState,
            worldState,
            sensorState,
            safetyState,
            CancellationToken.None));

    Console.WriteLine();
    Console.WriteLine("All AI smoke cases completed.");
}
catch (Exception ex)
{
    Console.WriteLine("AI smoke test FAILED.");
    Console.WriteLine(ex);
    Environment.ExitCode = 1;
}

static async Task<string?> TryReadGatewaySnapshotAsync(HttpClient http, string url)
{
    try
    {
        using var response = await http.GetAsync(url).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Gateway snapshot HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}");
            return null;
        }

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Gateway snapshot okunamadı: {ex.Message}");
        return null;
    }
}

static async Task RunCaseAsync(
    string title,
    Func<Task<GroundAiMissionResult>> action)
{
    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine(title);
    Console.WriteLine("============================================================");

    var started = DateTime.UtcNow;
    var result = await action().ConfigureAwait(false);
    var elapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;

    PrintResult(result, elapsedMs);
}

static void PrintResult(GroundAiMissionResult result, double measuredLatencyMs)
{
    Console.WriteLine($"Provider: {result.Provider}");
    Console.WriteLine($"ResultKind: {result.ResultKind}");
    Console.WriteLine($"OperationKind: {result.OperationKind}");
    Console.WriteLine($"MeasuredLatencyMs: {measuredLatencyMs:F0}");
    Console.WriteLine($"DiagnosticsLatencyMs: {result.LatencyMs:F0}");
    Console.WriteLine($"Allowed: {result.Safety.Allowed}");
    Console.WriteLine($"RequiresHumanApproval: {result.RequiresHumanApproval}");
    Console.WriteLine($"SafetyReason: {result.Safety.Reason}");
    Console.WriteLine();

    Console.WriteLine($"PlanId: {result.Plan.Id}");
    Console.WriteLine($"Goal: {result.Plan.Goal}");
    Console.WriteLine($"StepCount: {result.Plan.Steps.Count}");
    Console.WriteLine();

    foreach (var step in result.Plan.Steps.OrderBy(s => s.Index))
    {
        Console.WriteLine($"[{step.Index}] {step.Title}");
        Console.WriteLine($"    {step.Description}");

        if (step.ExpectedTools.Count > 0)
            Console.WriteLine($"    Tools: {string.Join(", ", step.ExpectedTools)}");

        Console.WriteLine();
    }

    Console.WriteLine("Validation issues:");
    foreach (var issue in result.Validation.Issues)
    {
        Console.WriteLine($"- {issue.Severity} | {issue.Code} | {issue.Message}");
    }
}