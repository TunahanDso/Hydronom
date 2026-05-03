using System;

namespace HydronomOps.Gateway.Contracts.Diagnostics;

/// <summary>
/// Gateway iÃ§ loglarÄ±nÄ±n arayÃ¼ze taÅŸÄ±nmasÄ± iÃ§in kullanÄ±lan kayÄ±t modeli.
/// </summary>
public sealed class GatewayLogDto
{
    /// <summary>
    /// Log zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Log seviyesi.
    /// Ã–rn: Trace, Debug, Info, Warn, Error, Critical
    /// </summary>
    public string Level { get; set; } = "Info";

    /// <summary>
    /// Logun geldiÄŸi bileÅŸen.
    /// Ã–rn: tcp-ingress, mapper, broadcast, health
    /// </summary>
    public string Category { get; set; } = "gateway";

    /// <summary>
    /// Ä°nsan okunur log mesajÄ±.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Ä°steÄŸe baÄŸlÄ± hata / teknik detay.
    /// </summary>
    public string? Detail { get; set; }
}
