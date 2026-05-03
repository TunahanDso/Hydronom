using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Sensors;

/// <summary>
/// SensÃ¶r durum Ã¶zetini taÅŸÄ±r.
/// </summary>
public sealed class SensorStateDto
{
    /// <summary>
    /// Paket zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// SensÃ¶r adÄ±.
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// SensÃ¶r tipi.
    /// Ã–rn: imu, gps, lidar, camera.
    /// </summary>
    public string SensorType { get; set; } = string.Empty;

    /// <summary>
    /// SensÃ¶r kaynaÄŸÄ± / backend bilgisi.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// SensÃ¶r arka uÃ§ sÃ¼rÃ¼cÃ¼sÃ¼.
    /// </summary>
    public string? Backend { get; set; }

    /// <summary>
    /// SimÃ¼lasyon modu mu.
    /// </summary>
    public bool IsSimulated { get; set; }

    /// <summary>
    /// SensÃ¶r aktif mi.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// SensÃ¶rden veri geliyor mu.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// KonfigÃ¼re edilen yayÄ±n / Ã¶rnekleme hÄ±zÄ±.
    /// </summary>
    public double? ConfiguredRateHz { get; set; }

    /// <summary>
    /// Ã–lÃ§Ã¼len efektif hÄ±z.
    /// </summary>
    public double? EffectiveRateHz { get; set; }

    /// <summary>
    /// Son veri zamanÄ±.
    /// </summary>
    public DateTime? LastSampleUtc { get; set; }

    /// <summary>
    /// Son hata mesajÄ±.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// SensÃ¶re ait Ã¶zet metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// SensÃ¶re ait metinsel durum alanlarÄ±.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>
    /// Veri tazelik Ã¶zeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}
