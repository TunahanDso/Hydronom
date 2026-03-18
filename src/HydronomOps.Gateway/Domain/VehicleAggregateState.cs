using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Actuators;
using HydronomOps.Gateway.Contracts.Diagnostics;
using HydronomOps.Gateway.Contracts.Mission;
using HydronomOps.Gateway.Contracts.Sensors;
using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Domain;

/// <summary>
/// Gateway tarafında tek araç için tutulan birleşik durum modeli.
/// </summary>
public sealed class VehicleAggregateState
{
    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Gateway başlangıç zamanı.
    /// </summary>
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son genel güncelleme zamanı.
    /// </summary>
    public DateTime? LastUpdatedUtc { get; set; }

    /// <summary>
    /// Runtime’dan son veri geliş zamanı.
    /// </summary>
    public DateTime? LastRuntimeIngressUtc { get; set; }

    /// <summary>
    /// Son araç telemetri zamanı.
    /// </summary>
    public DateTime? LastVehicleTelemetryUtc { get; set; }

    /// <summary>
    /// Son görev durumu zamanı.
    /// </summary>
    public DateTime? LastMissionStateUtc { get; set; }

    /// <summary>
    /// Son sensör durumu zamanı.
    /// </summary>
    public DateTime? LastSensorStateUtc { get; set; }

    /// <summary>
    /// Son aktüatör durumu zamanı.
    /// </summary>
    public DateTime? LastActuatorStateUtc { get; set; }

    /// <summary>
    /// Son tanı durumu zamanı.
    /// </summary>
    public DateTime? LastDiagnosticsStateUtc { get; set; }

    /// <summary>
    /// Son gateway broadcast zamanı.
    /// </summary>
    public DateTime? LastGatewayBroadcastUtc { get; set; }

    /// <summary>
    /// Runtime’dan gelen son ham satır.
    /// </summary>
    public string? LastRawRuntimeLine { get; set; }

    /// <summary>
    /// Son hata metni.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Runtime bağlı kabul ediliyor mu.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// Python bağlı kabul ediliyor mu.
    /// </summary>
    public bool PythonConnected { get; set; }

    /// <summary>
    /// Aktif websocket istemci sayısı.
    /// </summary>
    public int WebSocketClientCount { get; set; }

    /// <summary>
    /// Runtime’dan alınan toplam mesaj sayısı.
    /// </summary>
    public long TotalMessagesReceived { get; set; }

    /// <summary>
    /// Gateway’den yayınlanan toplam mesaj sayısı.
    /// </summary>
    public long TotalMessagesBroadcast { get; set; }

    /// <summary>
    /// Son telemetri verisi.
    /// </summary>
    public VehicleTelemetryDto? VehicleTelemetry { get; set; }

    /// <summary>
    /// Son görev durumu.
    /// </summary>
    public MissionStateDto? MissionState { get; set; }

    /// <summary>
    /// Son sensör durumu.
    /// </summary>
    public SensorStateDto? SensorState { get; set; }

    /// <summary>
    /// Son aktüatör durumu.
    /// </summary>
    public ActuatorStateDto? ActuatorState { get; set; }

    /// <summary>
    /// Son tanı durumu.
    /// </summary>
    public DiagnosticsStateDto? DiagnosticsState { get; set; }

    /// <summary>
    /// Son log kayıtları.
    /// </summary>
    public List<GatewayLogDto> Logs { get; set; } = new();
}