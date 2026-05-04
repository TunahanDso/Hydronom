using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;
using HydronomOps.Gateway.Domain;

namespace HydronomOps.Gateway.Services.State;

/// <summary>
/// Gateway'in tuttuğu birleşik araç durumuna erişim sağlar.
/// </summary>
public interface IGatewayStateStore
{
    VehicleAggregateState GetCurrent();

    VehicleTelemetryDto? GetVehicleTelemetry();

    void SetVehicleTelemetry(VehicleTelemetryDto telemetry);
    void SetMissionState(MissionStateDto missionState);

    /// <summary>
    /// Geriye dönük uyumluluk için genel sensör durumunu yazar.
    /// Yeni C# Primary akışta ana runtime sensör özeti için SetRuntimeSensorState,
    /// twin/debug sensörler için SetDebugSensorState tercih edilmelidir.
    /// </summary>
    void SetSensorState(SensorStateDto sensorState);

    /// <summary>
    /// C# Primary RuntimeTelemetrySummary üzerinden gelen ana sensör sağlık özetini yazar.
    /// TwinImu, TwinGps veya raw/debug sensörler bu alanı ezmemelidir.
    /// </summary>
    void SetRuntimeSensorState(SensorStateDto sensorState);

    /// <summary>
    /// Twin, sim, raw veya debug amaçlı sensör durumunu yazar.
    /// Bu veri ana operasyonel sensör sağlığını ve ana vehicle telemetry kaynağını ezmemelidir.
    /// </summary>
    void SetDebugSensorState(SensorStateDto sensorState);

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