namespace HydronomOps.Gateway.Contracts.Common;

/// <summary>
/// Gateway Ã¼zerinden yayÄ±nlanacak mesaj tipleri.
/// </summary>
public static class GatewayMessageType
{
    public const string VehicleTelemetry = "vehicle.telemetry";
    public const string MissionState = "mission.state";
    public const string SensorState = "sensor.state";
    public const string ActuatorState = "actuator.state";
    public const string DiagnosticsState = "diagnostics.state";
    public const string Heartbeat = "gateway.heartbeat";
    public const string GatewayLog = "gateway.log";
    public const string Snapshot = "gateway.snapshot";
}
