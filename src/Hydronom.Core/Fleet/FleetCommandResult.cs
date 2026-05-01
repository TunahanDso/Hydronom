namespace Hydronom.Core.Fleet;

/// <summary>
/// Bir FleetCommand komutuna araç/node tarafından verilen sonucu temsil eder.
/// 
/// Bu model HydronomEnvelope.Payload içinde taşınır.
/// MessageType örneği:
/// - "FleetCommandResult"
/// - "CommandResult"
/// 
/// Amaç:
/// Yer istasyonu veya komutu gönderen node şunu anlayabilsin:
/// - Komut alındı mı?
/// - Kabul edildi mi?
/// - Reddedildi mi?
/// - SafetyGate tarafından engellendi mi?
/// - Uygulandı mı?
/// - Hata mı oluştu?
/// 
/// Bu sonuç modeli, özellikle yer istasyonu kontrolünde çok önemlidir.
/// Çünkü operatör sadece komutu göndermemeli; aracın bu komuta ne cevap verdiğini de görmelidir.
/// </summary>
public sealed record FleetCommandResult
{
    /// <summary>
    /// Sonuç mesajının benzersiz kimliği.
    /// 
    /// Loglama, replay ve debugging için kullanılır.
    /// </summary>
    public string ResultId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Bu sonucun hangi komuta ait olduğunu belirtir.
    /// 
    /// FleetCommand.CommandId ile eşleşmelidir.
    /// Böylece yer istasyonu gönderdiği komutla gelen cevabı bağlayabilir.
    /// </summary>
    public string CommandId { get; init; } = string.Empty;

    /// <summary>
    /// Sonucu üreten node kimliği.
    /// 
    /// Genellikle komutu alan araçtır.
    /// 
    /// Örnek:
    /// - "VEHICLE-ALPHA-001"
    /// - "VEHICLE-BETA-001"
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Sonucun gönderileceği node kimliği.
    /// 
    /// Genellikle komutu gönderen yer istasyonu veya gateway'dir.
    /// 
    /// Örnek:
    /// - "GROUND-001"
    /// - "OPS-GATEWAY-001"
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Komut sonucunun genel durumu.
    /// 
    /// Örnekler:
    /// - "Received"
    /// - "Accepted"
    /// - "Rejected"
    /// - "SafetyBlocked"
    /// - "Unauthorized"
    /// - "Expired"
    /// - "Applied"
    /// - "Failed"
    /// 
    /// Şimdilik string bırakıyoruz.
    /// İleride CommandResultStatus enum'una çevrilebilir.
    /// </summary>
    public string Status { get; init; } = "Received";

    /// <summary>
    /// Sonucun başarılı kabul edilip edilmediğini belirtir.
    /// 
    /// true:
    /// - Komut kabul edilmiş veya uygulanmıştır.
    /// 
    /// false:
    /// - Komut reddedilmiş, safety tarafından engellenmiş,
    ///   yetkisiz bulunmuş veya hata oluşmuştur.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Sonuçla ilgili insan tarafından okunabilir kısa açıklama.
    /// 
    /// Hydronom Ops tarafında operatöre gösterilebilir.
    /// 
    /// Örnek:
    /// - "Mission command accepted."
    /// - "Command rejected by SafetyGate: obstacle too close."
    /// - "Operator is not authorized for manual control."
    /// - "Command expired before execution."
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Sonucun üretildiği UTC zaman damgası.
    /// 
    /// Komut-cevap gecikmesi ve olay zaman çizelgesi için kullanılır.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komutun araç tarafında hangi katmana kadar ilerlediğini belirtir.
    /// 
    /// Örnekler:
    /// - "Received"
    /// - "Validated"
    /// - "AuthorityChecked"
    /// - "SafetyChecked"
    /// - "DecisionAccepted"
    /// - "ActuationApplied"
    /// 
    /// Bu alan debugging için çok değerlidir.
    /// Yer istasyonu sadece "başarısız" görmek yerine nerede takıldığını anlayabilir.
    /// </summary>
    public string ProcessingStage { get; init; } = "Received";

    /// <summary>
    /// Komutun reddedilme veya başarısız olma sebebi.
    /// 
    /// Örnekler:
    /// - "InvalidCommand"
    /// - "UnauthorizedSource"
    /// - "SafetyRisk"
    /// - "ObstacleTooClose"
    /// - "StaleCommand"
    /// - "UnsupportedCommandType"
    /// - "RuntimeFault"
    /// 
    /// Başarılı sonuçlarda boş kalabilir.
    /// </summary>
    public string FailureReason { get; init; } = string.Empty;

    /// <summary>
    /// Sonuçla ilgili ek metadata bilgileri.
    /// 
    /// Örnek:
    /// - "latencyMs": "32"
    /// - "safetyGate": "passed"
    /// - "runtimeMode": "Autonomous"
    /// - "activeMissionId": "MISSION-2026-001"
    /// 
    /// İlk fazda esneklik sağlar.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Sonucun temel olarak geçerli olup olmadığını döndürür.
    /// 
    /// En azından:
    /// - ResultId
    /// - CommandId
    /// - SourceNodeId
    /// - TargetNodeId
    /// dolu olmalıdır.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResultId) &&
        !string.IsNullOrWhiteSpace(CommandId) &&
        !string.IsNullOrWhiteSpace(SourceNodeId) &&
        !string.IsNullOrWhiteSpace(TargetNodeId);
}