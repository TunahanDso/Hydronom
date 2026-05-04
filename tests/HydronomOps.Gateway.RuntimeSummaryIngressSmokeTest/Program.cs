using System.Text.Json;
using HydronomOps.Gateway.Infrastructure.TcpIngress;
using HydronomOps.Gateway.Infrastructure.Time;
using HydronomOps.Gateway.Services.Mapping;
using HydronomOps.Gateway.Services.RuntimeOps;
using HydronomOps.Gateway.Services.State;

Console.WriteLine("=== HydronomOps Gateway Runtime Summary Ingress Smoke Test ===");
Console.WriteLine();

var stateStore = new GatewayStateStore();
var clock = new SystemClock();
var mapper = new RuntimeToGatewayMapper(clock);
var parser = new RuntimeFrameParser(stateStore, mapper);
var projection = new GatewayRuntimeOpsProjectionService();

var timestampUtc = DateTime.UtcNow;

var runtimeSummary = new
{
    type = "RuntimeTelemetrySummary",
    runtimeId = "runtime-summary-ingress-test",
    timestampUtc,
    overallHealth = "Healthy",
    hasCriticalIssue = false,
    hasWarnings = false,

    sensorCount = 2,
    healthySensorCount = 2,

    fusionEngineName = "gps_imu_state_estimator",
    fusionProducedCandidate = true,
    fusionConfidence = 0.95,

    vehicleId = "RUNTIME-SUMMARY-INGRESS-001",
    hasState = true,
    stateX = 18.75,
    stateY = -6.50,
    stateZ = 0.35,
    stateYawDeg = 64.0,
    stateConfidence = 0.92,

    lastStateDecision = "Accepted",
    lastStateAccepted = true,
    acceptedStateUpdateCount = 3,
    rejectedStateUpdateCount = 0,

    summary = "Healthy: runtime summary ingress smoke test."
};

var json = JsonSerializer.Serialize(runtimeSummary);

Console.WriteLine("[1] RuntimeTelemetrySummary JSON");
Console.WriteLine(json);
Console.WriteLine();

parser.ProcessLine(json);

var state = stateStore.GetCurrent();

Console.WriteLine("[2] Gateway state after parser");
Console.WriteLine($"VehicleId              : {state.VehicleTelemetry?.VehicleId}");
Console.WriteLine($"Runtime connected      : {state.RuntimeConnected}");
Console.WriteLine($"Python connected       : {state.PythonConnected}");
Console.WriteLine($"Total ingress messages : {state.TotalMessagesReceived}");
Console.WriteLine($"Has vehicle telemetry  : {state.VehicleTelemetry is not null}");
Console.WriteLine($"Has sensor state       : {state.SensorState is not null}");
Console.WriteLine($"Has diagnostics state  : {state.DiagnosticsState is not null}");
Console.WriteLine();

Require(state.RuntimeConnected, "RuntimeConnected true olmalı.");
Require(!state.PythonConnected, "RuntimeTelemetrySummary PythonConnected true yapmamalı.");
Require(state.TotalMessagesReceived == 1, "Ingress message count 1 olmalı.");
Require(state.VehicleTelemetry is not null, "Vehicle telemetry oluşmalı.");
Require(state.SensorState is not null, "Sensor state oluşmalı.");
Require(state.DiagnosticsState is not null, "Diagnostics state oluşmalı.");

var vehicle = state.VehicleTelemetry!;

Console.WriteLine("[3] Vehicle telemetry from runtime summary");
Console.WriteLine($"VehicleId : {vehicle.VehicleId}");
Console.WriteLine($"Pose      : X={vehicle.X:F3}, Y={vehicle.Y:F3}, Z={vehicle.Z:F3}, Yaw={vehicle.YawDeg:F3}");
Console.WriteLine($"Heading   : {vehicle.HeadingDeg:F3}");
Console.WriteLine($"Origin    : {vehicle.Fields?["origin"]}");
Console.WriteLine();

Require(vehicle.VehicleId == "RUNTIME-SUMMARY-INGRESS-001", "Vehicle id runtime summary'den gelmeli.");
Require(Math.Abs(vehicle.X - 18.75) < 0.001, "Vehicle X doğru map edilmeli.");
Require(Math.Abs(vehicle.Y - -6.50) < 0.001, "Vehicle Y doğru map edilmeli.");
Require(Math.Abs(vehicle.Z - 0.35) < 0.001, "Vehicle Z doğru map edilmeli.");
Require(Math.Abs(vehicle.YawDeg - 64.0) < 0.001, "Vehicle yaw doğru map edilmeli.");
Require(Math.Abs(vehicle.HeadingDeg - 64.0) < 0.001, "Heading yaw ile eşlenmeli.");
Require(vehicle.Fields is not null && vehicle.Fields["origin"] == "runtime-telemetry-summary", "Vehicle telemetry origin runtime-telemetry-summary olmalı.");
Require(vehicle.Fields is not null && vehicle.Fields["runtime.overallHealth"] == "Healthy", "Vehicle fields overall health taşımalı.");
Require(vehicle.Fields is not null && vehicle.Fields["runtime.lastStateDecision"] == "Accepted", "Vehicle fields last decision taşımalı.");
Require(vehicle.Metrics is not null && Math.Abs(vehicle.Metrics["runtime.fusionConfidence"] - 0.95) < 0.001, "Vehicle metrics fusion confidence taşımalı.");
Require(vehicle.Metrics is not null && Math.Abs(vehicle.Metrics["runtime.acceptedStateUpdateCount"] - 3.0) < 0.001, "Vehicle metrics accepted count taşımalı.");

var sensor = state.SensorState!;

Console.WriteLine("[4] Sensor state from runtime summary");
Console.WriteLine($"SensorName   : {sensor.SensorName}");
Console.WriteLine($"SensorType   : {sensor.SensorType}");
Console.WriteLine($"Source       : {sensor.Source}");
Console.WriteLine($"Backend      : {sensor.Backend}");
Console.WriteLine($"IsEnabled    : {sensor.IsEnabled}");
Console.WriteLine($"IsHealthy    : {sensor.IsHealthy}");
Console.WriteLine($"LastError    : {sensor.LastError}");
Console.WriteLine();

Require(sensor.VehicleId == "RUNTIME-SUMMARY-INGRESS-001", "Sensor state vehicle id doğru olmalı.");
Require(sensor.SensorName == "RuntimeSensorSummary", "Sensor name RuntimeSensorSummary olmalı.");
Require(sensor.SensorType == "runtime-summary", "Sensor type runtime-summary olmalı.");
Require(sensor.Source == "csharp-primary-runtime", "Sensor source csharp-primary-runtime olmalı.");
Require(sensor.Backend == "runtime-diagnostics", "Sensor backend runtime-diagnostics olmalı.");
Require(sensor.IsEnabled, "Sensor summary enabled olmalı.");
Require(sensor.IsHealthy, "Sensor summary healthy olmalı.");
Require(sensor.LastError is null, "Healthy runtime summary sensor last error üretmemeli.");

var diagnostics = state.DiagnosticsState!;

Console.WriteLine("[5] Diagnostics state from runtime summary");
Console.WriteLine($"GatewayStatus         : {diagnostics.GatewayStatus}");
Console.WriteLine($"RuntimeConnected      : {diagnostics.RuntimeConnected}");
Console.WriteLine($"LastRuntimeMessageUtc : {diagnostics.LastRuntimeMessageUtc:O}");
Console.WriteLine($"LastError             : {diagnostics.LastError}");
Console.WriteLine();

Require(diagnostics.GatewayStatus == "Healthy", "Diagnostics GatewayStatus runtime overall health olmalı.");
Require(diagnostics.RuntimeConnected, "Diagnostics RuntimeConnected true olmalı.");
Require(diagnostics.LastError is null, "Healthy runtime summary diagnostics last error üretmemeli.");

var telemetrySummary = projection.BuildTelemetrySummary(state);

Console.WriteLine("[6] Runtime ops telemetry projection");
Console.WriteLine($"RuntimeId           : {telemetrySummary.RuntimeId}");
Console.WriteLine($"OverallHealth       : {telemetrySummary.OverallHealth}");
Console.WriteLine($"RuntimeConnected    : {telemetrySummary.RuntimeConnected}");
Console.WriteLine($"PythonConnected     : {telemetrySummary.PythonConnected}");
Console.WriteLine($"VehicleId           : {telemetrySummary.VehicleId}");
Console.WriteLine($"HasVehicleTelemetry : {telemetrySummary.HasVehicleTelemetry}");
Console.WriteLine($"Pose                : X={telemetrySummary.X:F3}, Y={telemetrySummary.Y:F3}, Z={telemetrySummary.Z:F3}, Yaw={telemetrySummary.YawDeg:F3}");
Console.WriteLine($"SensorHealthy       : {telemetrySummary.SensorHealthy}");
Console.WriteLine($"Summary             : {telemetrySummary.Summary}");
Console.WriteLine();

Require(telemetrySummary.OverallHealth == "Healthy", "Projection overall health Healthy olmalı.");
Require(telemetrySummary.RuntimeConnected, "Projection runtime connected true olmalı.");
Require(!telemetrySummary.PythonConnected, "Projection python connected false kalmalı.");
Require(telemetrySummary.VehicleId == "RUNTIME-SUMMARY-INGRESS-001", "Projection vehicle id doğru olmalı.");
Require(telemetrySummary.HasVehicleTelemetry, "Projection has vehicle telemetry true olmalı.");
Require(Math.Abs(telemetrySummary.X - 18.75) < 0.001, "Projection X doğru olmalı.");
Require(Math.Abs(telemetrySummary.Y - -6.50) < 0.001, "Projection Y doğru olmalı.");
Require(Math.Abs(telemetrySummary.YawDeg - 64.0) < 0.001, "Projection yaw doğru olmalı.");
Require(telemetrySummary.SensorHealthy, "Projection sensor healthy true olmalı.");

var runtimeDiagnostics = projection.BuildDiagnostics(state);

Console.WriteLine("[7] Runtime ops diagnostics projection");
Console.WriteLine($"OverallHealth       : {runtimeDiagnostics.OverallHealth}");
Console.WriteLine($"HasCriticalIssue    : {runtimeDiagnostics.HasCriticalIssue}");
Console.WriteLine($"HasWarnings         : {runtimeDiagnostics.HasWarnings}");
Console.WriteLine($"RuntimeConnected    : {runtimeDiagnostics.RuntimeConnected}");
Console.WriteLine($"HasVehicleTelemetry : {runtimeDiagnostics.HasVehicleTelemetry}");
Console.WriteLine($"HasSensorState      : {runtimeDiagnostics.HasSensorState}");
Console.WriteLine($"HasDiagnosticsState : {runtimeDiagnostics.HasDiagnosticsState}");
Console.WriteLine($"Issue count         : {runtimeDiagnostics.Issues.Count}");
Console.WriteLine($"Summary             : {runtimeDiagnostics.Summary}");
Console.WriteLine();

Require(runtimeDiagnostics.OverallHealth == "Healthy", "Diagnostics projection health Healthy olmalı.");
Require(!runtimeDiagnostics.HasCriticalIssue, "Diagnostics projection critical issue olmamalı.");
Require(!runtimeDiagnostics.HasWarnings, "Diagnostics projection warning olmamalı.");
Require(runtimeDiagnostics.RuntimeConnected, "Diagnostics projection runtime connected true olmalı.");
Require(runtimeDiagnostics.HasVehicleTelemetry, "Diagnostics projection vehicle telemetry true olmalı.");
Require(runtimeDiagnostics.HasSensorState, "Diagnostics projection sensor state true olmalı.");
Require(runtimeDiagnostics.HasDiagnosticsState, "Diagnostics projection diagnostics state true olmalı.");
Require(runtimeDiagnostics.Issues.Count == 0, "Diagnostics projection issue count 0 olmalı.");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("PASS: RuntimeTelemetrySummary ingress parser, gateway state ve runtime ops projection doğru çalıştı.");
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