namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Yer istasyonu, gateway, operatör paneli veya başka bir yetkili node tarafından
/// bir Hydronom aracına gönderilen filo/görev/operatör komutunu temsil eder.
/// 
/// Bu model HydronomEnvelope.Payload içinde taşınır.
/// MessageType örneği:
/// - "FleetCommand"
/// - "MissionCommand"
/// - "ControlCommand"
/// - "EmergencyCommand"
/// 
/// Önemli mimari kural:
/// FleetCommand asla doğrudan motora gitmemelidir.
/// Araç tarafında şu zincirden geçmelidir:
/// 
/// CommandValidator
/// -> AuthorityManager
/// -> SafetyGate
/// -> Decision/Task/Actuation
/// 
/// Yani yer istasyonu güçlüdür ama araç üstü Safety katmanını ezemez.
/// </summary>
public sealed record FleetCommand
{
    /// <summary>
    /// Komutun benzersiz kimliği.
    /// 
    /// Kullanım alanları:
    /// - Komut takibi
    /// - ACK / result eşleştirme
    /// - Replay kayıtları
    /// - Operatör geçmişi
    /// - Debugging
    /// 
    /// Varsayılan olarak GUID tabanlı üretilir.
    /// </summary>
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Komutu gönderen node kimliği.
    /// 
    /// Örnekler:
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// - "VEHICLE-ALPHA-001"
    /// 
    /// Araç tarafındaki AuthorityManager bu alanı kullanarak:
    /// - Bu komutu kim gönderdi?
    /// - Bu kaynağın yetkisi var mı?
    /// sorularını cevaplayabilir.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komutun hedef node kimliği.
    /// 
    /// Örnekler:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "BROADCAST"
    /// 
    /// Broadcast komutlar özellikle EmergencyStop gibi durumlarda kullanılabilir.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komutun mantıksal türü.
    /// 
    /// Örnekler:
    /// - "AssignMission"
    /// - "CancelMission"
    /// - "PauseMission"
    /// - "ResumeMission"
    /// - "ReturnHome"
    /// - "SetTarget"
    /// - "SetMode"
    /// - "ManualControl"
    /// - "EmergencyStop"
    /// 
    /// Alıcı taraf bu tipe göre Args alanını yorumlar.
    /// </summary>
    public string CommandType { get; init; } = string.Empty;

    /// <summary>
    /// Komutun yetki/güvenlik seviyesi.
    /// 
    /// Örnekler:
    /// - "Info"
    /// - "Suggestion"
    /// - "MissionCommand"
    /// - "ControlCommand"
    /// - "CriticalCommand"
    /// - "EmergencyCommand"
    /// 
    /// Bu alan araç tarafındaki AuthorityManager ve SafetyGate için önemlidir.
    /// Örneğin EmergencyCommand daha yüksek doğrulama veya özel işleme gerektirebilir.
    /// </summary>
    public string AuthorityLevel { get; init; } = "MissionCommand";

    /// <summary>
    /// Komutun öncelik seviyesi.
    /// 
    /// CommunicationRouter ve araç tarafındaki command queue bu alanı kullanabilir.
    /// EmergencyStop gibi komutlar Emergency seviyesinde olmalıdır.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Komutun oluşturulduğu UTC zaman damgası.
    /// 
    /// Araç tarafında stale/eski komutları reddetmek için kullanılabilir.
    /// Örneğin çok eski bir manuel kontrol komutu uygulanmamalıdır.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komutun maksimum geçerlilik süresi.
    /// 
    /// null ise sistem varsayılan komut geçerlilik politikasını kullanabilir.
    /// 
    /// Örnek:
    /// - ManualControl için çok kısa olabilir.
    /// - AssignMission için daha uzun olabilir.
    /// - EmergencyStop için özel politika uygulanabilir.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Komutun parametreleri.
    /// 
    /// Örnekler:
    /// AssignMission:
    /// - "missionId": "MISSION-2026-001"
    /// - "areaId": "AREA-A"
    /// 
    /// SetTarget:
    /// - "lat": "41.123"
    /// - "lon": "29.456"
    /// 
    /// ManualControl:
    /// - "throttle": "0.20"
    /// - "rudder": "-0.10"
    /// 
    /// Şimdilik string/string dictionary kullanıyoruz.
    /// Böylece ilk fazda esneklik sağlanır.
    /// İleride belirli komut tipleri için güçlü typed payload modelleri oluşturulabilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Komutun operatör tarafından mı üretildiğini belirtir.
    /// 
    /// true:
    /// - Komut doğrudan insan operatör etkileşiminden gelmiştir.
    /// 
    /// false:
    /// - Komut GroundStation Engine, AI Orchestrator, MissionAllocator
    ///   veya başka bir otomatik sistem tarafından üretilebilir.
    /// </summary>
    public bool IsOperatorIssued { get; init; }

    /// <summary>
    /// Komut için ACK / sonuç cevabı beklenip beklenmediğini belirtir.
    /// 
    /// true ise araç tarafı FleetCommandResult üretmelidir.
    /// 
    /// Özellikle:
    /// - MissionCommand
    /// - ControlCommand
    /// - CriticalCommand
    /// - EmergencyCommand
    /// için genellikle true olmalıdır.
    /// </summary>
    public bool RequiresResult { get; init; } = true;

    /// <summary>
    /// Komutla ilgili ek metadata bilgileri.
    /// 
    /// Örnek:
    /// - "uiAction": "mission_panel_assign"
    /// - "operatorName": "Tunahan"
    /// - "sourceScreen": "FleetDashboard"
    /// - "reason": "manual_test"
    /// 
    /// İlk fazda esneklik sağlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Komutun temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından:
    /// - CommandId
    /// - SourceNodeId
    /// - TargetNodeId
    /// - CommandType
    /// dolu olmalıdır.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CommandId) &&
        !string.IsNullOrWhiteSpace(SourceNodeId) &&
        !string.IsNullOrWhiteSpace(TargetNodeId) &&
        !string.IsNullOrWhiteSpace(CommandType);

    /// <summary>
    /// Komutun zaman aşımına uğrayıp uğramadığını kontrol eder.
    /// 
    /// nowUtc verilmezse DateTimeOffset.UtcNow kullanılır.
    /// 
    /// TimeToLive null ise komut bu metoda göre expired kabul edilmez.
    /// Daha gelişmiş sistemlerde komut tipine göre varsayılan TTL politikası ayrıca eklenebilir.
    /// </summary>
    public bool IsExpired(DateTimeOffset? nowUtc = null)
    {
        if (TimeToLive is null)
            return false;

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        return TimestampUtc + TimeToLive < now;
    }
}