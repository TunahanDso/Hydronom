using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Sensors;

/// <summary>
/// Sensör durum özetini taşır.
/// </summary>
public sealed class SensorStateDto
{
    /// <summary>
    /// Paket zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Sensör adı.
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Sensör tipi.
    /// Örn: imu, gps, lidar, camera.
    /// </summary>
    public string SensorType { get; set; } = string.Empty;

    /// <summary>
    /// Sensör kaynağı / backend bilgisi.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Sensör arka uç sürücüsü.
    /// </summary>
    public string? Backend { get; set; }

    /// <summary>
    /// Simülasyon modu mu.
    /// </summary>
    public bool IsSimulated { get; set; }

    /// <summary>
    /// Sensör aktif mi.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Sensörden veri geliyor mu.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Konfigüre edilen yayın / örnekleme hızı.
    /// </summary>
    public double? ConfiguredRateHz { get; set; }

    /// <summary>
    /// Ölçülen efektif hız.
    /// </summary>
    public double? EffectiveRateHz { get; set; }

    /// <summary>
    /// Son veri zamanı.
    /// </summary>
    public DateTime? LastSampleUtc { get; set; }

    /// <summary>
    /// Son hata mesajı.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Sensöre ait özet metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// Sensöre ait metinsel durum alanları.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>
    /// Veri tazelik özeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}