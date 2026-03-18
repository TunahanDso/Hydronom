using System;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway ve runtime hattının sağlık/teşhis özetini taşır.
/// </summary>
public sealed class DiagnosticsStateDto
{
    /// <summary>
    /// Tanı paketinin üretim zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gateway prosesinin genel sağlık durumu.
    /// </summary>
    public string GatewayStatus { get; set; } = "starting";

    /// <summary>
    /// Runtime TCP hattının bağlı olup olmadığı.
    /// </summary>
    public bool RuntimeConnected { get; set; }

    /// <summary>
    /// WebSocket tarafında en az bir istemci bağlı mı.
    /// </summary>
    public bool HasWebSocketClients { get; set; }

    /// <summary>
    /// Aktif WebSocket istemci sayısı.
    /// </summary>
    public int ConnectedWebSocketClients { get; set; }

    /// <summary>
    /// Runtime tarafından en son veri alınan zaman.
    /// </summary>
    public DateTime? LastRuntimeMessageUtc { get; set; }

    /// <summary>
    /// Runtime veri tazelik özeti.
    /// </summary>
    public FreshnessDto? RuntimeFreshness { get; set; }

    /// <summary>
    /// Son hata mesajı.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Son hata zamanı.
    /// </summary>
    public DateTime? LastErrorUtc { get; set; }

    /// <summary>
    /// Gateway'in toplam aldığı frame sayısı.
    /// </summary>
    public long IngressMessageCount { get; set; }

    /// <summary>
    /// Gateway'in toplam yayınladığı mesaj sayısı.
    /// </summary>
    public long BroadcastMessageCount { get; set; }
}