using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Services.RuntimeOps;
using HydronomOps.Gateway.Services.State;

Console.WriteLine("=== HydronomOps Gateway RuntimeOps Smoke Test ===");
Console.WriteLine();

var stateStore = new GatewayStateStore();
var projection = new GatewayRuntimeOpsProjectionService();

var vehicleTelemetry = new VehicleTelemetryDto
{
    TimestampUtc = DateTime.UtcNow,
    VehicleId = "GATEWAY-RUNTIME-OPS-001",
    X = 12.5,
    Y = -4.25,
    Z = 0.8,
    RollDeg = 1.0,
    PitchDeg = -0.5,
    YawDeg = 42.0,
    HeadingDeg = 42.0,
    Vx = 1.2,
    Vy = 0.4,
    Vz = 0.0,
    ObstacleAhead = true,
    ObstacleCount = 2
};

var sensorState = new SensorStateDto
{
    TimestampUtc = DateTime.UtcNow,
    VehicleId = "GATEWAY-RUNTIME-OPS-001",
    SensorName = "imu0",
    SensorType = "Imu",
    Source = "sim_imu",
    Backend = "sim",
    IsSimulated = true,
    IsEnabled = true,
    IsHealthy = true,
    ConfiguredRateHz = 20.0,
    EffectiveRateHz = 20.0,
    LastSampleUtc = DateTime.UtcNow
};

var diagnosticsState = new DiagnosticsStateDto
{
    TimestampUtc = DateTime.UtcNow,
    GatewayStatus = "running",
    RuntimeConnected = true,
    HasWebSocketClients = true,
    ConnectedWebSocketClients = 3,
    LastRuntimeMessageUtc = DateTime.UtcNow,
    IngressMessageCount = 15,
    BroadcastMessageCount = 7
};

stateStore.SetVehicleTelemetry(vehicleTelemetry);
stateStore.SetSensorState(sensorState);
stateStore.SetDiagnosticsState(diagnosticsState);
stateStore.SetRuntimeConnected(true);
stateStore.SetPythonConnected(false);
stateStore.SetWebSocketClientCount(3);

for (var i = 0; i < 15; i++)
{
    stateStore.TouchRuntimeMessage($"mock-runtime-line-{i}");
}

for (var i = 0; i < 7; i++)
{
    stateStore.IncrementBroadcastCount();
}

var state = stateStore.GetCurrent();

Console.WriteLine("[1] Gateway state");
Console.WriteLine($"VehicleId         : {state.VehicleId}");
Console.WriteLine($"Runtime connected : {state.RuntimeConnected}");
Console.WriteLine($"Python connected  : {state.PythonConnected}");
Console.WriteLine($"WS clients        : {state.WebSocketClientCount}");
Console.WriteLine($"Ingress count     : {state.TotalMessagesReceived}");
Console.WriteLine($"Broadcast count   : {state.TotalMessagesBroadcast}");
Console.WriteLine();

Require(state.RuntimeConnected, "Gateway state runtime connected true olmalı.");
Require(state.WebSocketClientCount == 3, "Gateway state websocket client count 3 olmalı.");
Require(state.TotalMessagesReceived == 15, "Gateway state ingress count 15 olmalı.");
Require(state.TotalMessagesBroadcast == 7, "Gateway state broadcast count 7 olmalı.");

var telemetrySummary = projection.BuildTelemetrySummary(state);

Console.WriteLine("[2] Runtime telemetry summary");
Console.WriteLine($"RuntimeId           : {telemetrySummary.RuntimeId}");
Console.WriteLine($"OverallHealth       : {telemetrySummary.OverallHealth}");
Console.WriteLine($"RuntimeConnected    : {telemetrySummary.RuntimeConnected}");
Console.WriteLine($"VehicleId           : {telemetrySummary.VehicleId}");
Console.WriteLine($"HasVehicleTelemetry : {telemetrySummary.HasVehicleTelemetry}");
Console.WriteLine($"Pose                : X={telemetrySummary.X:F3}, Y={telemetrySummary.Y:F3}, Z={telemetrySummary.Z:F3}, Yaw={telemetrySummary.YawDeg:F3}");
Console.WriteLine($"ObstacleAhead       : {telemetrySummary.ObstacleAhead}");
Console.WriteLine($"ObstacleCount       : {telemetrySummary.ObstacleCount}");
Console.WriteLine($"SensorHealthy       : {telemetrySummary.SensorHealthy}");
Console.WriteLine($"Summary             : {telemetrySummary.Summary}");
Console.WriteLine();

Require(telemetrySummary.RuntimeId == "hydronom_gateway_runtime", "Telemetry runtime id doğru olmalı.");
Require(telemetrySummary.OverallHealth == "Healthy", "Telemetry health Healthy olmalı.");
Require(!telemetrySummary.HasCriticalIssue, "Telemetry critical issue olmamalı.");
Require(!telemetrySummary.HasWarnings, "Telemetry warning olmamalı.");
Require(telemetrySummary.RuntimeConnected, "Telemetry runtime connected true olmalı.");
Require(telemetrySummary.VehicleId == "GATEWAY-RUNTIME-OPS-001", "Telemetry vehicle id doğru olmalı.");
Require(telemetrySummary.HasVehicleTelemetry, "Telemetry has vehicle telemetry true olmalı.");
Require(Math.Abs(telemetrySummary.X - 12.5) < 0.001, "Telemetry X doğru olmalı.");
Require(Math.Abs(telemetrySummary.Y - -4.25) < 0.001, "Telemetry Y doğru olmalı.");
Require(Math.Abs(telemetrySummary.YawDeg - 42.0) < 0.001, "Telemetry yaw doğru olmalı.");
Require(telemetrySummary.ObstacleAhead, "Telemetry obstacle ahead true olmalı.");
Require(telemetrySummary.ObstacleCount == 2, "Telemetry obstacle count 2 olmalı.");
Require(telemetrySummary.SensorHealthy, "Telemetry sensor healthy true olmalı.");

var diagnostics = projection.BuildDiagnostics(state);

Console.WriteLine("[3] Runtime diagnostics");
Console.WriteLine($"RuntimeId          : {diagnostics.RuntimeId}");
Console.WriteLine($"OverallHealth      : {diagnostics.OverallHealth}");
Console.WriteLine($"HasCriticalIssue   : {diagnostics.HasCriticalIssue}");
Console.WriteLine($"HasWarnings        : {diagnostics.HasWarnings}");
Console.WriteLine($"RuntimeConnected   : {diagnostics.RuntimeConnected}");
Console.WriteLine($"IngressCount       : {diagnostics.IngressMessageCount}");
Console.WriteLine($"BroadcastCount     : {diagnostics.BroadcastMessageCount}");
Console.WriteLine($"HasVehicleTelemetry: {diagnostics.HasVehicleTelemetry}");
Console.WriteLine($"HasSensorState     : {diagnostics.HasSensorState}");
Console.WriteLine($"HasDiagnosticsState: {diagnostics.HasDiagnosticsState}");
Console.WriteLine($"Issue count        : {diagnostics.Issues.Count}");
Console.WriteLine($"Summary            : {diagnostics.Summary}");
Console.WriteLine();

Require(diagnostics.RuntimeId == "hydronom_gateway_runtime", "Diagnostics runtime id doğru olmalı.");
Require(diagnostics.OverallHealth == "Healthy", "Diagnostics health Healthy olmalı.");
Require(!diagnostics.HasCriticalIssue, "Diagnostics critical issue olmamalı.");
Require(!diagnostics.HasWarnings, "Diagnostics warning olmamalı.");
Require(diagnostics.RuntimeConnected, "Diagnostics runtime connected true olmalı.");
Require(diagnostics.IngressMessageCount == 15, "Diagnostics ingress count 15 olmalı.");
Require(diagnostics.BroadcastMessageCount == 7, "Diagnostics broadcast count 7 olmalı.");
Require(diagnostics.HasVehicleTelemetry, "Diagnostics has vehicle telemetry true olmalı.");
Require(diagnostics.HasSensorState, "Diagnostics has sensor state true olmalı.");
Require(diagnostics.HasDiagnosticsState, "Diagnostics has diagnostics state true olmalı.");
Require(diagnostics.Issues.Count == 0, "Diagnostics issue count 0 olmalı.");

stateStore.SetLastError("mock gateway error");

var errorState = stateStore.GetCurrent();
var errorDiagnostics = projection.BuildDiagnostics(errorState);

Console.WriteLine("[4] Runtime diagnostics after gateway error");
Console.WriteLine($"OverallHealth    : {errorDiagnostics.OverallHealth}");
Console.WriteLine($"HasCriticalIssue : {errorDiagnostics.HasCriticalIssue}");
Console.WriteLine($"Issue count      : {errorDiagnostics.Issues.Count}");
Console.WriteLine($"First issue      : {errorDiagnostics.Issues.FirstOrDefault()?.Code}");
Console.WriteLine();

Require(errorDiagnostics.OverallHealth == "Critical", "Error diagnostics health Critical olmalı.");
Require(errorDiagnostics.HasCriticalIssue, "Error diagnostics critical issue true olmalı.");
Require(errorDiagnostics.Issues.Any(x => x.Code == "GATEWAY_LAST_ERROR"), "Error diagnostics GATEWAY_LAST_ERROR issue içermeli.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: Gateway runtime ops projection telemetry ve diagnostics DTO'larını doğru üretti.");
Console.ResetColor();

return 0;

static void Require(bool condition, string message)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {message}");
        return;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"FAIL: {message}");
    Console.ResetColor();

    throw new InvalidOperationException(message);
}