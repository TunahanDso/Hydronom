using System;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway iç loglarının arayüze taşınması için kullanılan kayıt modeli.
/// </summary>
public sealed class GatewayLogDto
{
    /// <summary>
    /// Log zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Log seviyesi.
    /// Örn: Trace, Debug, Info, Warn, Error, Critical
    /// </summary>
    public string Level { get; set; } = "Info";

    /// <summary>
    /// Logun geldiği bileşen.
    /// Örn: tcp-ingress, mapper, broadcast, health
    /// </summary>
    public string Category { get; set; } = "gateway";

    /// <summary>
    /// İnsan okunur log mesajı.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// İsteğe bağlı hata / teknik detay.
    /// </summary>
    public string? Detail { get; set; }
}