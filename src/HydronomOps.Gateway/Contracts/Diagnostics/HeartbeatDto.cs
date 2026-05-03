using System;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway'in dÃ¼zenli saÄŸlÄ±k yayÄ±nÄ± iÃ§in heartbeat verisi.
/// </summary>
public sealed class HeartbeatDto
{
    /// <summary>
    /// Gateway zaman damgasÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gateway ayakta mÄ±.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Aktif websocket istemci sayÄ±sÄ±.
    /// </summary>
    public int ConnectedClientCount { get; set; }

    /// <summary>
    /// Runtime baÄŸlantÄ±sÄ± aktif mi.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// Son runtime verisinin tazelik bilgisi.
    /// </summary>
    public FreshnessDto? RuntimeFreshness { get; set; }

    /// <summary>
    /// Gateway Ã§alÄ±ÅŸma sÃ¼resi.
    /// </summary>
    public double UptimeMs { get; set; }
}
