namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway/Ops runtime diagnostics cevabı.
/// Sensor/fusion/state authority bilgisi geldikçe bu DTO genişletilebilir.
/// </summary>
public sealed class GatewayRuntimeDiagnosticsDto
{
    public string RuntimeId { get; set; } = "hydronom_runtime";

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string OverallHealth { get; set; } = "Unknown";

    public bool HasCriticalIssue { get; set; }

    public bool HasWarnings { get; set; }

    public bool RuntimeConnected { get; set; }

    public bool PythonConnected { get; set; }

    public int WebSocketClientCount { get; set; }

    public long IngressMessageCount { get; set; }

    public long BroadcastMessageCount { get; set; }

    public DateTime StartedUtc { get; set; }

    public DateTime? LastUpdatedUtc { get; set; }

    public DateTime? LastRuntimeIngressUtc { get; set; }

    public DateTime? LastVehicleTelemetryUtc { get; set; }

    public DateTime? LastSensorStateUtc { get; set; }

    public DateTime? LastDiagnosticsStateUtc { get; set; }

    public DateTime? LastGatewayBroadcastUtc { get; set; }

    public string VehicleId { get; set; } = "hydronom-main";

    public bool HasVehicleTelemetry { get; set; }

    public bool HasSensorState { get; set; }

    public bool HasDiagnosticsState { get; set; }

    public string? GatewayStatus { get; set; }

    public string? LastError { get; set; }

    public string? LastRawRuntimeLine { get; set; }

    public IReadOnlyList<GatewayRuntimeDiagnosticIssueDto> Issues { get; set; }
        = Array.Empty<GatewayRuntimeDiagnosticIssueDto>();

    public string Summary { get; set; } = "Runtime diagnostics.";
}

/// <summary>
/// Gateway runtime diagnostics içindeki tekil issue.
/// </summary>
public sealed class GatewayRuntimeDiagnosticIssueDto
{
    public string Severity { get; set; } = "Info";

    public string Code { get; set; } = "INFO";

    public string Message { get; set; } = string.Empty;
}