using System;

namespace HydronomOps.Gateway.Contracts.Common;

/// <summary>
/// Gateway üzerinden çıkan tüm mesajlar için ortak zarf yapısı.
/// </summary>
public sealed class GatewayEnvelope
{
    /// <summary>
    /// Mesaj tipi.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Mesajın UTC zaman damgası.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Veri kaynağı.
    /// Örn: runtime, python, gateway.
    /// </summary>
    public string Source { get; set; } = "gateway";

    /// <summary>
    /// Sıralama / takip için artan sıra numarası.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// İçerik verisi.
    /// </summary>
    public object? Payload { get; set; }
}