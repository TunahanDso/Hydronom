namespace Hydronom.GroundStation.Commanding;

using Hydronom.Core.Fleet;

/// <summary>
/// Yer istasyonu tarafından üretilen veya takip edilen bir komutun kayıt modelidir.
/// 
/// CommandRecord, FleetCommand ile FleetCommandResult arasındaki ilişkiyi tutar.
/// Böylece Ground Station şunu takip edebilir:
/// - Hangi komut gönderildi?
/// - Hangi araca gönderildi?
/// - Ne zaman gönderildi?
/// - Araç cevap verdi mi?
/// - Komut kabul edildi mi?
/// - SafetyGate tarafından reddedildi mi?
/// - Komut hangi aşamaya kadar ilerledi?
/// 
/// Bu model ileride Hydronom Ops tarafındaki:
/// - Command History
/// - Operator Timeline
/// - Safety Rejection Log
/// - Mission Command Audit
/// ekranlarının temel veri modeli olabilir.
/// </summary>
public sealed record CommandRecord
{
    /// <summary>
    /// Takip edilen komutun kendisi.
    /// 
    /// FleetCommand:
    /// - CommandId
    /// - SourceNodeId
    /// - TargetNodeId
    /// - CommandType
    /// - AuthorityLevel
    /// - Priority
    /// - Args
    /// gibi bilgileri taşır.
    /// </summary>
    public FleetCommand Command { get; init; } = new();

    /// <summary>
    /// Komuta karşılık araçtan/node'dan gelen en son sonuç.
    /// 
    /// null ise:
    /// - Komuta henüz cevap gelmemiştir.
    /// - Komut yolda olabilir.
    /// - Hedef node offline olabilir.
    /// - Result gerektirmeyen bir komut olabilir.
    /// </summary>
    public FleetCommandResult? LastResult { get; init; }

    /// <summary>
    /// Komutun Ground Station tarafından kayıt altına alındığı UTC zamanıdır.
    /// 
    /// Genelde komut gönderilmeden hemen önce oluşturulur.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komuta ilk sonucun geldiği UTC zamanıdır.
    /// 
    /// null ise henüz sonuç alınmamıştır.
    /// </summary>
    public DateTimeOffset? FirstResultUtc { get; init; }

    /// <summary>
    /// Komuta gelen en son sonucun UTC zamanıdır.
    /// 
    /// Bazı komutlar birden fazla aşamalı sonuç döndürebilir:
    /// - Received
    /// - Accepted
    /// - SafetyChecked
    /// - Applied
    /// 
    /// Bu alan son güncellemeyi gösterir.
    /// </summary>
    public DateTimeOffset? LastResultUtc { get; init; }

    /// <summary>
    /// Komutun şu anda tamamlanmış sayılıp sayılmadığını belirtir.
    /// 
    /// true:
    /// - Komut uygulanmış olabilir.
    /// - Komut reddedilmiş olabilir.
    /// - Komut başarısız olmuş olabilir.
    /// 
    /// Yani completed her zaman successful anlamına gelmez.
    /// Başarı bilgisi LastResult.Success üzerinden okunmalıdır.
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Komutun cevap bekleyip beklemediğini pratik olarak döndürür.
    /// 
    /// FleetCommand.RequiresResult alanını temel alır.
    /// </summary>
    public bool RequiresResult =>
        Command.RequiresResult;

    /// <summary>
    /// Komuta cevap gelip gelmediğini döndürür.
    /// </summary>
    public bool HasResult =>
        LastResult is not null;

    /// <summary>
    /// Komutun sonucu başarılı mı?
    /// 
    /// Henüz sonuç yoksa false döner.
    /// </summary>
    public bool IsSuccessful =>
        LastResult?.Success == true;

    /// <summary>
    /// Komutun sonuç beklediği halde henüz cevap almadığını belirtir.
    /// </summary>
    public bool IsPending =>
        RequiresResult && LastResult is null && !IsCompleted;

    /// <summary>
    /// Komuta sonuç eklenmiş yeni bir CommandRecord üretir.
    /// 
    /// Record immutable kaldığı için mevcut nesne değiştirilmez;
    /// güncellenmiş kopya döner.
    /// </summary>
    public CommandRecord WithResult(FleetCommandResult result)
    {
        var now = DateTimeOffset.UtcNow;

        return this with
        {
            LastResult = result,
            FirstResultUtc = FirstResultUtc ?? now,
            LastResultUtc = now,
            IsCompleted = IsTerminalStatus(result.Status)
        };
    }

    /// <summary>
    /// Komutun zaman aşımına uğramış şekilde tamamlandığını belirten yeni kayıt üretir.
    /// </summary>
    public CommandRecord MarkExpired()
    {
        var result = new FleetCommandResult
        {
            CommandId = Command.CommandId,
            SourceNodeId = Command.TargetNodeId,
            TargetNodeId = Command.SourceNodeId,
            Status = "Expired",
            Success = false,
            Message = "Command expired before a valid result was received.",
            ProcessingStage = "CommandTracker",
            FailureReason = "CommandTimeout"
        };

        return this with
        {
            LastResult = result,
            FirstResultUtc = FirstResultUtc ?? DateTimeOffset.UtcNow,
            LastResultUtc = DateTimeOffset.UtcNow,
            IsCompleted = true
        };
    }

    /// <summary>
    /// Bir result status değerinin terminal/final durum sayılıp sayılmadığını belirler.
    /// 
    /// Terminal durumlar:
    /// - Applied
    /// - Rejected
    /// - SafetyBlocked
    /// - Unauthorized
    /// - Expired
    /// - Failed
    /// 
    /// Accepted her zaman final değildir; araç komutu kabul edip daha sonra uygulayabilir.
    /// </summary>
    private static bool IsTerminalStatus(string status)
    {
        return status.Equals("Applied", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("SafetyBlocked", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Expired", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Failed", StringComparison.OrdinalIgnoreCase);
    }
}