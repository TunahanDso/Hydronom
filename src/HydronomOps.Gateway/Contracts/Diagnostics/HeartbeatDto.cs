using System;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway'in düzenli sağlık yayını için heartbeat verisi.
/// </summary>
public sealed class HeartbeatDto
{
    /// <summary>
    /// Gateway zaman damgası.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gateway ayakta mı.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Aktif websocket istemci sayısı.
    /// </summary>
    public int ConnectedClientCount { get; set; }

    /// <summary>
    /// Runtime bağlantısı aktif mi.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// Son runtime verisinin tazelik bilgisi.
    /// </summary>
    public FreshnessDto? RuntimeFreshness { get; set; }

    /// <summary>
    /// Gateway çalışma süresi.
    /// </summary>
    public double UptimeMs { get; set; }
}