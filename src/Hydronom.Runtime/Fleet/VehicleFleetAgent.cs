namespace Hydronom.Runtime.Fleet;

using Hydronom.Core.Communication;
using Hydronom.Core.Fleet;

/// <summary>
/// Araç üzerindeki Hydronom Runtime'ın Fleet & Ground Station mimarisine
/// kendini tanıtmasını sağlayan temel ajan sınıfıdır.
/// 
/// VehicleFleetAgent şu işlerden sorumludur:
/// - Aracın kimliğini tutmak,
/// - Aracın mevcut durumundan FleetHeartbeat üretmek,
/// - Bu heartbeat'i HydronomEnvelope içine sarmak,
/// - Yer istasyonuna gönderilmeye hazır standart mesaj üretmek.
/// 
/// Bu sınıf henüz transport ile doğrudan mesaj göndermiyor.
/// Şimdilik sadece mesaj üretir.
/// Gönderme işini ileride TransportManager / CommunicationRouter yapacak.
/// </summary>
public sealed class VehicleFleetAgent
{
    /// <summary>
    /// Bu aracın Fleet mimarisindeki kimliği.
    /// 
    /// Örnek:
    /// - VEHICLE-ALPHA-001
    /// - VEHICLE-BETA-001
    /// - SIM-VEHICLE-001
    /// 
    /// Runtime, yer istasyonuna bu kimlikle görünür.
    /// </summary>
    public NodeIdentity Identity { get; }

    /// <summary>
    /// Varsayılan hedef yer istasyonu node kimliği.
    /// 
    /// İlk fazda GROUND-001 kullanıyoruz.
    /// İleride config üzerinden değiştirilebilir.
    /// </summary>
    public string GroundNodeId { get; }

    /// <summary>
    /// VehicleFleetAgent oluşturur.
    /// </summary>
    public VehicleFleetAgent(NodeIdentity identity, string groundNodeId = "GROUND-001")
    {
        if (identity is null || !identity.IsValid)
            throw new ArgumentException("VehicleFleetAgent için geçerli bir NodeIdentity gerekir.", nameof(identity));

        Identity = identity;
        GroundNodeId = string.IsNullOrWhiteSpace(groundNodeId)
            ? "GROUND-001"
            : groundNodeId;
    }

    /// <summary>
    /// Araç durumundan FleetHeartbeat payload'ı üretir.
    /// 
    /// Bu metot Runtime içindeki mevcut state, görev, health, batarya ve transport
    /// bilgileriyle çağrılabilir.
    /// 
    /// İlk sürümde parametreler basit tutuldu.
    /// İleride VehicleState, HealthReport, PowerReport, MissionState gibi güçlü
    /// modellerden otomatik üretime geçilebilir.
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
    /// Araç durumundan doğrudan HydronomEnvelope içine sarılmış heartbeat mesajı üretir.
    /// 
    /// Bu metot ileride transport katmanına verilecek hazır mesajı üretir.
    /// Yani Runtime şunu diyebilir:
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