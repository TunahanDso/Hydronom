namespace Hydronom.Runtime.Fleet;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// AraÃ§ Ã¼zerindeki Hydronom Runtime'Ä±n Fleet & Ground Station mimarisine
/// kendini tanÄ±tmasÄ±nÄ± saÄŸlayan temel ajan sÄ±nÄ±fÄ±dÄ±r.
/// 
/// VehicleFleetAgent ÅŸu iÅŸlerden sorumludur:
/// - AracÄ±n kimliÄŸini tutmak,
/// - AracÄ±n mevcut durumundan FleetHeartbeat Ã¼retmek,
/// - Bu heartbeat'i HydronomEnvelope iÃ§ine sarmak,
/// - Yer istasyonuna gÃ¶nderilmeye hazÄ±r standart mesaj Ã¼retmek.
/// 
/// Bu sÄ±nÄ±f henÃ¼z transport ile doÄŸrudan mesaj gÃ¶ndermiyor.
/// Åimdilik sadece mesaj Ã¼retir.
/// GÃ¶nderme iÅŸini ileride TransportManager / CommunicationRouter yapacak.
/// </summary>
public sealed class VehicleFleetAgent
{
    /// <summary>
    /// Bu aracÄ±n Fleet mimarisindeki kimliÄŸi.
    /// 
    /// Ã–rnek:
    /// - VEHICLE-ALPHA-001
    /// - VEHICLE-BETA-001
    /// - SIM-VEHICLE-001
    /// 
    /// Runtime, yer istasyonuna bu kimlikle gÃ¶rÃ¼nÃ¼r.
    /// </summary>
    public NodeIdentity Identity { get; }

    /// <summary>
    /// VarsayÄ±lan hedef yer istasyonu node kimliÄŸi.
    /// 
    /// Ä°lk fazda GROUND-001 kullanÄ±yoruz.
    /// Ä°leride config Ã¼zerinden deÄŸiÅŸtirilebilir.
    /// </summary>
    public string GroundNodeId { get; }

    /// <summary>
    /// VehicleFleetAgent oluÅŸturur.
    /// </summary>
    public VehicleFleetAgent(NodeIdentity identity, string groundNodeId = "GROUND-001")
    {
        if (identity is null || !identity.IsValid)
            throw new ArgumentException("VehicleFleetAgent iÃ§in geÃ§erli bir NodeIdentity gerekir.", nameof(identity));

        Identity = identity;
        GroundNodeId = string.IsNullOrWhiteSpace(groundNodeId)
            ? "GROUND-001"
            : groundNodeId;
    }

    /// <summary>
    /// AraÃ§ durumundan FleetHeartbeat payload'Ä± Ã¼retir.
    /// 
    /// Bu metot Runtime iÃ§indeki mevcut state, gÃ¶rev, health, batarya ve transport
    /// bilgileriyle Ã§aÄŸrÄ±labilir.
    /// 
    /// Ä°lk sÃ¼rÃ¼mde parametreler basit tutuldu.
    /// Ä°leride VehicleState, HealthReport, PowerReport, MissionState gibi gÃ¼Ã§lÃ¼
    /// modellerden otomatik Ã¼retime geÃ§ilebilir.
    /// </summary>
    public FleetHeartbeat CreateHeartbeat(
        string mode = "Unknown",
        string health = "Unknown",
        double? batteryPercent = null,
        string activeMissionId = "",
        string missionState = "Idle",
        double? latitude = null,
        double? longitude = null,
        double? headingDeg = null,
        double? speedMps = null,
        IReadOnlyList<TransportKind>? availableTransports = null,
        IReadOnlyList<VehicleCapability>? capabilities = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new FleetHeartbeat
        {
            Identity = Identity,
            TimestampUtc = DateTimeOffset.UtcNow,
            Mode = mode,
            Health = health,
            BatteryPercent = batteryPercent,
            ActiveMissionId = activeMissionId,
            MissionState = missionState,
            Latitude = latitude,
            Longitude = longitude,
            HeadingDeg = headingDeg,
            SpeedMps = speedMps,
            AvailableTransports = availableTransports ?? Array.Empty<TransportKind>(),
            Capabilities = capabilities ?? Array.Empty<VehicleCapability>(),
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// AraÃ§ durumundan doÄŸrudan HydronomEnvelope iÃ§ine sarÄ±lmÄ±ÅŸ heartbeat mesajÄ± Ã¼retir.
    /// 
    /// Bu metot ileride transport katmanÄ±na verilecek hazÄ±r mesajÄ± Ã¼retir.
    /// Yani Runtime ÅŸunu diyebilir:
    /// 
    /// var envelope = fleetAgent.CreateHeartbeatEnvelope(...);
    /// await transport.SendAsync(envelope, ct);
    /// </summary>
    public HydronomEnvelope CreateHeartbeatEnvelope(
        string mode = "Unknown",
        string health = "Unknown",
        double? batteryPercent = null,
        string activeMissionId = "",
        string missionState = "Idle",
        double? latitude = null,
        double? longitude = null,
        double? headingDeg = null,
        double? speedMps = null,
        IReadOnlyList<TransportKind>? availableTransports = null,
        IReadOnlyList<VehicleCapability>? capabilities = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var heartbeat = CreateHeartbeat(
            mode,
            health,
            batteryPercent,
            activeMissionId,
            missionState,
            latitude,
            longitude,
            headingDeg,
            speedMps,
            availableTransports,
            capabilities,
            metadata);

        return HydronomEnvelopeFactory.CreateHeartbeat(heartbeat, GroundNodeId);
    }
}
