namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Hydronom Ops için Gateway seviyesinde runtime telemetry özeti.
/// Runtime iç modeline doğrudan bağımlı değildir.
/// </summary>
public sealed class GatewayRuntimeTelemetrySummaryDto
{
    public string RuntimeId { get; set; } = "hydronom_runtime";

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string OverallHealth { get; set; } = "Unknown";

    public bool HasCriticalIssue { get; set; }

    public bool HasWarnings { get; set; }

    public bool RuntimeConnected { get; set; }

    public bool PythonConnected { get; set; }

    public int WebSocketClientCount { get; set; }

    public long TotalMessagesReceived { get; set; }

    public long TotalMessagesBroadcast { get; set; }

    public string VehicleId { get; set; } = "hydronom-main";

    public bool HasVehicleTelemetry { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public double YawDeg { get; set; }

    public double HeadingDeg { get; set; }

    public double Vx { get; set; }

    public double Vy { get; set; }

    public double Vz { get; set; }

    public int ObstacleCount { get; set; }

    public bool ObstacleAhead { get; set; }

    public bool HasSensorState { get; set; }

    public bool SensorHealthy { get; set; }

    public string? SensorName { get; set; }

    public string? SensorType { get; set; }

    public bool HasDiagnosticsState { get; set; }

    public string? GatewayStatus { get; set; }

    public DateTime? LastRuntimeIngressUtc { get; set; }

    public DateTime? LastGatewayBroadcastUtc { get; set; }

    public string? LastError { get; set; }

    public string Summary { get; set; } = "Runtime telemetry summary.";
}