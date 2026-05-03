using System;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway ve runtime hattÄ±nÄ±n saÄŸlÄ±k/teÅŸhis Ã¶zetini taÅŸÄ±r.
/// </summary>
public sealed class DiagnosticsStateDto
{
    /// <summary>
    /// TanÄ± paketinin Ã¼retim zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gateway prosesinin genel saÄŸlÄ±k durumu.
    /// </summary>
    public string GatewayStatus { get; set; } = "starting";

    /// <summary>
    /// Runtime TCP hattÄ±nÄ±n baÄŸlÄ± olup olmadÄ±ÄŸÄ±.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// WebSocket tarafÄ±nda en az bir istemci baÄŸlÄ± mÄ±.
    /// </summary>
    public bool HasWebSocketClients { get; set; }

    /// <summary>
    /// Aktif WebSocket istemci sayÄ±sÄ±.
    /// </summary>
    public int ConnectedWebSocketClients { get; set; }

    /// <summary>
    /// Runtime tarafÄ±ndan en son veri alÄ±nan zaman.
    /// </summary>
    public DateTime? LastRuntimeMessageUtc { get; set; }

    /// <summary>
    /// Runtime veri tazelik Ã¶zeti.
    /// </summary>
    public FreshnessDto? RuntimeFreshness { get; set; }

    /// <summary>
    /// Son hata mesajÄ±.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Son hata zamanÄ±.
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }

    /// <summary>
    /// Gateway'in toplam aldÄ±ÄŸÄ± frame sayÄ±sÄ±.
    /// </summary>
    public long IngressMessageCount { get; set; }

    /// <summary>
    /// Gateway'in toplam yayÄ±nladÄ±ÄŸÄ± mesaj sayÄ±sÄ±.
    /// </summary>
    public long BroadcastMessageCount { get; set; }
}
