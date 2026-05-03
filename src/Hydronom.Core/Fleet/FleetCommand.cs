namespace Hydronom.Core.Fleet;

using Hydronom.Core.Communication;

/// <summary>
/// Yer istasyonu, gateway, operatÃ¶r paneli veya baÅŸka bir yetkili node tarafÄ±ndan
/// bir Hydronom aracÄ±na gÃ¶nderilen filo/gÃ¶rev/operatÃ¶r komutunu temsil eder.
/// 
/// Bu model HydronomEnvelope.Payload iÃ§inde taÅŸÄ±nÄ±r.
/// MessageType Ã¶rneÄŸi:
/// - "FleetCommand"
/// - "MissionCommand"
/// - "ControlCommand"
/// - "EmergencyCommand"
/// 
/// Ã–nemli mimari kural:
/// FleetCommand asla doÄŸrudan motora gitmemelidir.
/// AraÃ§ tarafÄ±nda ÅŸu zincirden geÃ§melidir:
/// 
/// CommandValidator
/// -> AuthorityManager
/// -> SafetyGate
/// -> Decision/Task/Actuation
/// 
/// Yani yer istasyonu gÃ¼Ã§lÃ¼dÃ¼r ama araÃ§ Ã¼stÃ¼ Safety katmanÄ±nÄ± ezemez.
/// </summary>
public sealed record FleetCommand
{
    /// <summary>
    /// Komutun benzersiz kimliÄŸi.
    /// 
    /// KullanÄ±m alanlarÄ±:
    /// - Komut takibi
    /// - ACK / result eÅŸleÅŸtirme
    /// - Replay kayÄ±tlarÄ±
    /// - OperatÃ¶r geÃ§miÅŸi
    /// - Debugging
    /// 
    /// VarsayÄ±lan olarak GUID tabanlÄ± Ã¼retilir.
    /// </summary>
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Komutu gÃ¶nderen node kimliÄŸi.
    /// 
    /// Ã–rnekler:
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// - "VEHICLE-ALPHA-001"
    /// 
    /// AraÃ§ tarafÄ±ndaki AuthorityManager bu alanÄ± kullanarak:
    /// - Bu komutu kim gÃ¶nderdi?
    /// - Bu kaynaÄŸÄ±n yetkisi var mÄ±?
    /// sorularÄ±nÄ± cevaplayabilir.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komutun hedef node kimliÄŸi.
    /// 
    /// Ã–rnekler:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// - "BROADCAST"
    /// 
    /// Broadcast komutlar Ã¶zellikle EmergencyStop gibi durumlarda kullanÄ±labilir.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komutun mantÄ±ksal tÃ¼rÃ¼.
    /// 
    /// Ã–rnekler:
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
    /// AlÄ±cÄ± taraf bu tipe gÃ¶re Args alanÄ±nÄ± yorumlar.
    /// </summary>
    public string CommandType { get; init; } = string.Empty;

    /// <summary>
    /// Komutun yetki/gÃ¼venlik seviyesi.
    /// 
    /// Ã–rnekler:
    /// - "Info"
    /// - "Suggestion"
    /// - "MissionCommand"
    /// - "ControlCommand"
    /// - "CriticalCommand"
    /// - "EmergencyCommand"
    /// 
    /// Bu alan araÃ§ tarafÄ±ndaki AuthorityManager ve SafetyGate iÃ§in Ã¶nemlidir.
    /// Ã–rneÄŸin EmergencyCommand daha yÃ¼ksek doÄŸrulama veya Ã¶zel iÅŸleme gerektirebilir.
    /// </summary>
    public string AuthorityLevel { get; init; } = "MissionCommand";

    /// <summary>
    /// Komutun Ã¶ncelik seviyesi.
    /// 
    /// CommunicationRouter ve araÃ§ tarafÄ±ndaki command queue bu alanÄ± kullanabilir.
    /// EmergencyStop gibi komutlar Emergency seviyesinde olmalÄ±dÄ±r.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Komutun oluÅŸturulduÄŸu UTC zaman damgasÄ±.
    /// 
    /// AraÃ§ tarafÄ±nda stale/eski komutlarÄ± reddetmek iÃ§in kullanÄ±labilir.
    /// Ã–rneÄŸin Ã§ok eski bir manuel kontrol komutu uygulanmamalÄ±dÄ±r.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komutun maksimum geÃ§erlilik sÃ¼resi.
    /// 
    /// null ise sistem varsayÄ±lan komut geÃ§erlilik politikasÄ±nÄ± kullanabilir.
    /// 
    /// Ã–rnek:
    /// - ManualControl iÃ§in Ã§ok kÄ±sa olabilir.
    /// - AssignMission iÃ§in daha uzun olabilir.
    /// - EmergencyStop iÃ§in Ã¶zel politika uygulanabilir.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Komutun parametreleri.
    /// 
    /// Ã–rnekler:
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
    /// Åimdilik string/string dictionary kullanÄ±yoruz.
    /// BÃ¶ylece ilk fazda esneklik saÄŸlanÄ±r.
    /// Ä°leride belirli komut tipleri iÃ§in gÃ¼Ã§lÃ¼ typed payload modelleri oluÅŸturulabilir.
    /// </summary>
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Komutun operatÃ¶r tarafÄ±ndan mÄ± Ã¼retildiÄŸini belirtir.
    /// 
    /// true:
    /// - Komut doÄŸrudan insan operatÃ¶r etkileÅŸiminden gelmiÅŸtir.
    /// 
    /// false:
    /// - Komut GroundStation Engine, AI Orchestrator, MissionAllocator
    ///   veya baÅŸka bir otomatik sistem tarafÄ±ndan Ã¼retilebilir.
    /// </summary>
    public bool IsOperatorIssued { get; init; }

    /// <summary>
    /// Komut iÃ§in ACK / sonuÃ§ cevabÄ± beklenip beklenmediÄŸini belirtir.
    /// 
    /// true ise araÃ§ tarafÄ± FleetCommandResult Ã¼retmelidir.
    /// 
    /// Ã–zellikle:
    /// - MissionCommand
    /// - ControlCommand
    /// - CriticalCommand
    /// - EmergencyCommand
    /// iÃ§in genellikle true olmalÄ±dÄ±r.
    /// </summary>
    public bool RequiresResult { get; init; } = true;

    /// <summary>
    /// Komutla ilgili ek metadata bilgileri.
    /// 
    /// Ã–rnek:
    /// - "uiAction": "mission_panel_assign"
    /// - "operatorName": "Tunahan"
    /// - "sourceScreen": "FleetDashboard"
    /// - "reason": "manual_test"
    /// 
    /// Ä°lk fazda esneklik saÄŸlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Komutun temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan:
    /// - CommandId
    /// - SourceNodeId
    /// - TargetNodeId
    /// - CommandType
    /// dolu olmalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CommandId) &&
        !string.IsNullOrWhiteSpace(SourceNodeId) &&
        !string.IsNullOrWhiteSpace(TargetNodeId) &&
        !string.IsNullOrWhiteSpace(CommandType);

    /// <summary>
    /// Komutun zaman aÅŸÄ±mÄ±na uÄŸrayÄ±p uÄŸramadÄ±ÄŸÄ±nÄ± kontrol eder.
    /// 
    /// nowUtc verilmezse DateTimeOffset.UtcNow kullanÄ±lÄ±r.
    /// 
    /// TimeToLive null ise komut bu metoda gÃ¶re expired kabul edilmez.
    /// Daha geliÅŸmiÅŸ sistemlerde komut tipine gÃ¶re varsayÄ±lan TTL politikasÄ± ayrÄ±ca eklenebilir.
    /// </summary>
    public bool IsExpired(DateTimeOffset? nowUtc = null)
    {
        if (TimeToLive is null)
            return false;

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        return TimestampUtc + TimeToLive < now;
    }
}
