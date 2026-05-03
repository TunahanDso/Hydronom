using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Domain;

/// <summary>
/// Gateway tarafÄ±nda tek araÃ§ iÃ§in tutulan birleÅŸik durum modeli.
/// </summary>
public sealed class VehicleAggregateState
{
    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Gateway baÅŸlangÄ±Ã§ zamanÄ±.
    /// </summary>
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son genel gÃ¼ncelleme zamanÄ±.
    /// </summary>
    public DateTime? LastUpdatedUtc { get; set; }

    /// <summary>
    /// Runtimeâ€™dan son veri geliÅŸ zamanÄ±.
    /// </summary>
    public DateTime? LastRuntimeIngressUtc { get; set; }

    /// <summary>
    /// Son araÃ§ telemetri zamanÄ±.
    /// </summary>
    public DateTime? LastVehicleTelemetryUtc { get; set; }

    /// <summary>
    /// Son gÃ¶rev durumu zamanÄ±.
    /// </summary>
    public DateTime? LastMissionStateUtc { get; set; }

    /// <summary>
    /// Son sensÃ¶r durumu zamanÄ±.
    /// </summary>
    public DateTime? LastSensorStateUtc { get; set; }

    /// <summary>
    /// Son aktÃ¼atÃ¶r durumu zamanÄ±.
    /// </summary>
    public DateTime? LastActuatorStateUtc { get; set; }

    /// <summary>
    /// Son tanÄ± durumu zamanÄ±.
    /// </summary>
    public DateTime? LastDiagnosticsStateUtc { get; set; }

    /// <summary>
    /// Son gateway broadcast zamanÄ±.
    /// </summary>
    public DateTime? LastGatewayBroadcastUtc { get; set; }

    /// <summary>
    /// Runtimeâ€™dan gelen son ham satÄ±r.
    /// </summary>
    public string? LastRawRuntimeLine { get; set; }

    /// <summary>
    /// Son hata metni.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Runtime baÄŸlÄ± kabul ediliyor mu.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// Python baÄŸlÄ± kabul ediliyor mu.
    /// </summary>
    public bool PythonConnected { get; set; }

    /// <summary>
    /// Aktif websocket istemci sayÄ±sÄ±.
    /// </summary>
    public int WebSocketClientCount { get; set; }

    /// <summary>
    /// Runtimeâ€™dan alÄ±nan toplam mesaj sayÄ±sÄ±.
    /// </summary>
    public long TotalMessagesReceived { get; set; }

    /// <summary>
    /// Gatewayâ€™den yayÄ±nlanan toplam mesaj sayÄ±sÄ±.
    /// </summary>
    public long TotalMessagesBroadcast { get; set; }

    /// <summary>
    /// Son telemetri verisi.
    /// </summary>
    public VehicleTelemetryDto? VehicleTelemetry { get; set; }

    /// <summary>
    /// Son gÃ¶rev durumu.
    /// </summary>
    public MissionStateDto? MissionState { get; set; }

    /// <summary>
    /// Son sensÃ¶r durumu.
    /// </summary>
    public SensorStateDto? SensorState { get; set; }

    /// <summary>
    /// Son aktÃ¼atÃ¶r durumu.
    /// </summary>
    public ActuatorStateDto? ActuatorState { get; set; }

    /// <summary>
    /// Son tanÄ± durumu.
    /// </summary>
    public DiagnosticsStateDto? DiagnosticsState { get; set; }

    /// <summary>
    /// Son log kayÄ±tlarÄ±.
    /// </summary>
    public List<GatewayLogDto> Logs { get; set; } = new();
}
