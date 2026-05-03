using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.State;

/// <summary>
/// Gateway'in tuttuÄŸu birleÅŸik araÃ§ durumuna eriÅŸim saÄŸlar.
/// </summary>
public interface IGatewayStateStore
{
    VehicleAggregateState GetCurrent();

    VehicleTelemetryDto? GetVehicleTelemetry();

    void SetVehicleTelemetry(VehicleTelemetryDto telemetry);
    void SetMissionState(MissionStateDto missionState);
    void SetSensorState(SensorStateDto sensorState);
    void SetActuatorState(ActuatorStateDto actuatorState);
    void SetDiagnosticsState(DiagnosticsStateDto diagnosticsState);

    void AddLog(GatewayLogDto log);

    void TouchRuntimeMessage(string? rawLine = null);

    void SetRuntimeConnected(bool isConnected);
    void SetPythonConnected(bool isConnected);
    void SetWebSocketClientCount(int count);
    void IncrementBroadcastCount();
    void SetLastError(string? error);
}
