namespace Hydronom.Core.Fleet;

/// <summary>
/// Bir FleetCommand komutuna araÃ§/node tarafÄ±ndan verilen sonucu temsil eder.
/// 
/// Bu model HydronomEnvelope.Payload iÃ§inde taÅŸÄ±nÄ±r.
/// MessageType Ã¶rneÄŸi:
/// - "FleetCommandResult"
/// - "CommandResult"
/// 
/// AmaÃ§:
/// Yer istasyonu veya komutu gÃ¶nderen node ÅŸunu anlayabilsin:
/// - Komut alÄ±ndÄ± mÄ±?
/// - Kabul edildi mi?
/// - Reddedildi mi?
/// - SafetyGate tarafÄ±ndan engellendi mi?
/// - UygulandÄ± mÄ±?
/// - Hata mÄ± oluÅŸtu?
/// 
/// Bu sonuÃ§ modeli, Ã¶zellikle yer istasyonu kontrolÃ¼nde Ã§ok Ã¶nemlidir.
/// Ã‡Ã¼nkÃ¼ operatÃ¶r sadece komutu gÃ¶ndermemeli; aracÄ±n bu komuta ne cevap verdiÄŸini de gÃ¶rmelidir.
/// </summary>
public sealed record FleetCommandResult
{
    /// <summary>
    /// SonuÃ§ mesajÄ±nÄ±n benzersiz kimliÄŸi.
    /// 
    /// Loglama, replay ve debugging iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public string ResultId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Bu sonucun hangi komuta ait olduÄŸunu belirtir.
    /// 
    /// FleetCommand.CommandId ile eÅŸleÅŸmelidir.
    /// BÃ¶ylece yer istasyonu gÃ¶nderdiÄŸi komutla gelen cevabÄ± baÄŸlayabilir.
    /// </summary>
    public string CommandId { get; init; } = string.Empty;

    /// <summary>
    /// Sonucu Ã¼reten node kimliÄŸi.
    /// 
    /// Genellikle komutu alan araÃ§tÄ±r.
    /// 
    /// Ã–rnek:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Sonucun gÃ¶nderileceÄŸi node kimliÄŸi.
    /// 
    /// Genellikle komutu gÃ¶nderen yer istasyonu veya gateway'dir.
    /// 
    /// Ã–rnek:
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komut sonucunun genel durumu.
    /// 
    /// Ã–rnekler:
    /// - "Received"
    /// - "Accepted"
    /// - "Rejected"
    /// - "SafetyBlocked"
    /// - "Unauthorized"
    /// - "Expired"
    /// - "Applied"
    /// - "Failed"
    /// 
    /// Åimdilik string bÄ±rakÄ±yoruz.
    /// Ä°leride CommandResultStatus enum'una Ã§evrilebilir.
    /// </summary>
    public string Status { get; init; } = "Received";

    /// <summary>
    /// Sonucun baÅŸarÄ±lÄ± kabul edilip edilmediÄŸini belirtir.
    /// 
    /// true:
    /// - Komut kabul edilmiÅŸ veya uygulanmÄ±ÅŸtÄ±r.
    /// 
    /// false:
    /// - Komut reddedilmiÅŸ, safety tarafÄ±ndan engellenmiÅŸ,
    ///   yetkisiz bulunmuÅŸ veya hata oluÅŸmuÅŸtur.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// SonuÃ§la ilgili insan tarafÄ±ndan okunabilir kÄ±sa aÃ§Ä±klama.
    /// 
    /// Hydronom Ops tarafÄ±nda operatÃ¶re gÃ¶sterilebilir.
    /// 
    /// Ã–rnek:
    /// - "Mission command accepted."
    /// - "Command rejected by SafetyGate: obstacle too close."
    /// - "Operator is not authorized for manual control."
    /// - "Command expired before execution."
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Sonucun Ã¼retildiÄŸi UTC zaman damgasÄ±.
    /// 
    /// Komut-cevap gecikmesi ve olay zaman Ã§izelgesi iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komutun araÃ§ tarafÄ±nda hangi katmana kadar ilerlediÄŸini belirtir.
    /// 
    /// Ã–rnekler:
    /// - "Received"
    /// - "Validated"
    /// - "AuthorityChecked"
    /// - "SafetyChecked"
    /// - "DecisionAccepted"
    /// - "ActuationApplied"
    /// 
    /// Bu alan debugging iÃ§in Ã§ok deÄŸerlidir.
    /// Yer istasyonu sadece "baÅŸarÄ±sÄ±z" gÃ¶rmek yerine nerede takÄ±ldÄ±ÄŸÄ±nÄ± anlayabilir.
    /// </summary>
    public string ProcessingStage { get; init; } = "Received";

    /// <summary>
    /// Komutun reddedilme veya baÅŸarÄ±sÄ±z olma sebebi.
    /// 
    /// Ã–rnekler:
    /// - "InvalidCommand"
    /// - "UnauthorizedSource"
    /// - "SafetyRisk"
    /// - "ObstacleTooClose"
    /// - "StaleCommand"
    /// - "UnsupportedCommandType"
    /// - "RuntimeFault"
    /// 
    /// BaÅŸarÄ±lÄ± sonuÃ§larda boÅŸ kalabilir.
    /// </summary>
    public string FailureReason { get; init; } = string.Empty;

    /// <summary>
    /// SonuÃ§la ilgili ek metadata bilgileri.
    /// 
    /// Ã–rnek:
    /// - "latencyMs": "32"
    /// - "safetyGate": "passed"
    /// - "runtimeMode": "Autonomous"
    /// - "activeMissionId": "MISSION-2026-001"
    /// 
    /// Ä°lk fazda esneklik saÄŸlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Sonucun temel olarak geÃ§erli olup olmadÄ±ÄŸÄ±nÄ± dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// En azÄ±ndan:
    /// - ResultId
    /// - CommandId
    /// - SourceNodeId
    /// - TargetNodeId
    /// dolu olmalÄ±dÄ±r.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResultId) &&
        !string.IsNullOrWhiteSpace(CommandId) &&
        !string.IsNullOrWhiteSpace(SourceNodeId) &&
        !string.IsNullOrWhiteSpace(TargetNodeId);
}
