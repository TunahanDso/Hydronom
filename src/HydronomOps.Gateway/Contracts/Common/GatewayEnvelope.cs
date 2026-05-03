using System;

namespace HydronomOps.Gateway.Contracts.Common;

/// <summary>
/// Gateway Ã¼zerinden Ã§Ä±kan tÃ¼m mesajlar iÃ§in ortak zarf yapÄ±sÄ±.
/// </summary>
public sealed class GatewayEnvelope
{
    /// <summary>
    /// Mesaj tipi.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// MesajÄ±n UTC zaman damgasÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Veri kaynaÄŸÄ±.
    /// Ã–rn: runtime, python, gateway.
    /// </summary>
    public string Source { get; set; } = "gateway";

    /// <summary>
    /// SÄ±ralama / takip iÃ§in artan sÄ±ra numarasÄ±.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Ä°Ã§erik verisi.
    /// </summary>
    public object? Payload { get; set; }
}
