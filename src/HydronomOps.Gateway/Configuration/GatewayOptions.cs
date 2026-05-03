namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// Gateway genel davranÄ±ÅŸ ayarlarÄ±.
/// </summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// Gateway heartbeat yayÄ±nÄ± aÃ§Ä±k mÄ±.
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// Snapshot endpoint'i aktif mi.
    /// </summary>
    public bool EnableSnapshotEndpoint { get; set; } = true;

    /// <summary>
    /// Status endpoint'i aktif mi.
    /// </summary>
    public bool EnableStatusEndpoint { get; set; } = true;

    /// <summary>
    /// Gelen veriler loglansÄ±n mÄ±.
    /// </summary>
    public bool LogIncomingFrames { get; set; } = false;

    /// <summary>
    /// Giden yayÄ±nlar loglansÄ±n mÄ±.
    /// </summary>
    public bool LogOutgoingBroadcasts { get; set; } = false;

    /// <summary>
    /// VarsayÄ±lan araÃ§ kimliÄŸi.
    /// </summary>
    public string DefaultVehicleId { get; set; } = "hydronom-main";
}
