namespace HydronomOps.Gateway.Configuration;

/// <summary>
/// Gateway genel davranış ayarları.
/// </summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// Gateway heartbeat yayını açık mı.
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
    /// Gelen veriler loglansın mı.
    /// </summary>
    public bool LogIncomingFrames { get; set; } = false;

    /// <summary>
    /// Giden yayınlar loglansın mı.
    /// </summary>
    public bool LogOutgoingBroadcasts { get; set; } = false;

    /// <summary>
    /// Varsayılan araç kimliği.
    /// </summary>
    public string DefaultVehicleId { get; set; } = "hydronom-main";
}