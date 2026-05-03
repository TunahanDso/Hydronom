namespace Hydronom.GroundStation.Commanding;

using Hydronom.Core.Fleet;

/// <summary>
/// Yer istasyonu tarafÄ±ndan Ã¼retilen veya takip edilen bir komutun kayÄ±t modelidir.
/// 
/// CommandRecord, FleetCommand ile FleetCommandResult arasÄ±ndaki iliÅŸkiyi tutar.
/// BÃ¶ylece Ground Station ÅŸunu takip edebilir:
/// - Hangi komut gÃ¶nderildi?
/// - Hangi araca gÃ¶nderildi?
/// - Ne zaman gÃ¶nderildi?
/// - AraÃ§ cevap verdi mi?
/// - Komut kabul edildi mi?
/// - SafetyGate tarafÄ±ndan reddedildi mi?
/// - Komut hangi aÅŸamaya kadar ilerledi?
/// 
/// Bu model ileride Hydronom Ops tarafÄ±ndaki:
/// - Command History
/// - Operator Timeline
/// - Safety Rejection Log
/// - Mission Command Audit
/// ekranlarÄ±nÄ±n temel veri modeli olabilir.
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
    /// gibi bilgileri taÅŸÄ±r.
    /// </summary>
    public FleetCommand Command { get; init; } = new();

    /// <summary>
    /// Komuta karÅŸÄ±lÄ±k araÃ§tan/node'dan gelen en son sonuÃ§.
    /// 
    /// null ise:
    /// - Komuta henÃ¼z cevap gelmemiÅŸtir.
    /// - Komut yolda olabilir.
    /// - Hedef node offline olabilir.
    /// - Result gerektirmeyen bir komut olabilir.
    /// </summary>
    public FleetCommandResult? LastResult { get; init; }

    /// <summary>
    /// Komutun Ground Station tarafÄ±ndan kayÄ±t altÄ±na alÄ±ndÄ±ÄŸÄ± UTC zamanÄ±dÄ±r.
    /// 
    /// Genelde komut gÃ¶nderilmeden hemen Ã¶nce oluÅŸturulur.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Komuta ilk sonucun geldiÄŸi UTC zamanÄ±dÄ±r.
    /// 
    /// null ise henÃ¼z sonuÃ§ alÄ±nmamÄ±ÅŸtÄ±r.
    /// </summary>
    public DateTimeOffset? FirstResultUtc { get; init; }

    /// <summary>
    /// Komuta gelen en son sonucun UTC zamanÄ±dÄ±r.
    /// 
    /// BazÄ± komutlar birden fazla aÅŸamalÄ± sonuÃ§ dÃ¶ndÃ¼rebilir:
    /// - Received
    /// - Accepted
    /// - SafetyChecked
    /// - Applied
    /// 
    /// Bu alan son gÃ¼ncellemeyi gÃ¶sterir.
    /// </summary>
    public DateTimeOffset? LastResultUtc { get; init; }

    /// <summary>
    /// Komutun ÅŸu anda tamamlanmÄ±ÅŸ sayÄ±lÄ±p sayÄ±lmadÄ±ÄŸÄ±nÄ± belirtir.
    /// 
    /// true:
    /// - Komut uygulanmÄ±ÅŸ olabilir.
    /// - Komut reddedilmiÅŸ olabilir.
    /// - Komut baÅŸarÄ±sÄ±z olmuÅŸ olabilir.
    /// 
    /// Yani completed her zaman successful anlamÄ±na gelmez.
    /// BaÅŸarÄ± bilgisi LastResult.Success Ã¼zerinden okunmalÄ±dÄ±r.
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Komutun cevap bekleyip beklemediÄŸini pratik olarak dÃ¶ndÃ¼rÃ¼r.
    /// 
    /// FleetCommand.RequiresResult alanÄ±nÄ± temel alÄ±r.
    /// </summary>
    public bool RequiresResult =>
        Command.RequiresResult;

    /// <summary>
    /// Komuta cevap gelip gelmediÄŸini dÃ¶ndÃ¼rÃ¼r.
    /// </summary>
    public bool HasResult =>
        LastResult is not null;

    /// <summary>
    /// Komutun sonucu baÅŸarÄ±lÄ± mÄ±?
    /// 
    /// HenÃ¼z sonuÃ§ yoksa false dÃ¶ner.
    /// </summary>
    public bool IsSuccessful =>
        LastResult?.Success == true;

    /// <summary>
    /// Komutun sonuÃ§ beklediÄŸi halde henÃ¼z cevap almadÄ±ÄŸÄ±nÄ± belirtir.
    /// </summary>
    public bool IsPending =>
        RequiresResult && LastResult is null && !IsCompleted;

    /// <summary>
    /// Komuta sonuÃ§ eklenmiÅŸ yeni bir CommandRecord Ã¼retir.
    /// 
    /// Record immutable kaldÄ±ÄŸÄ± iÃ§in mevcut nesne deÄŸiÅŸtirilmez;
    /// gÃ¼ncellenmiÅŸ kopya dÃ¶ner.
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
    /// Komutun zaman aÅŸÄ±mÄ±na uÄŸramÄ±ÅŸ ÅŸekilde tamamlandÄ±ÄŸÄ±nÄ± belirten yeni kayÄ±t Ã¼retir.
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
    /// Bir result status deÄŸerinin terminal/final durum sayÄ±lÄ±p sayÄ±lmadÄ±ÄŸÄ±nÄ± belirler.
    /// 
    /// Terminal durumlar:
    /// - Applied
    /// - Rejected
    /// - SafetyBlocked
    /// - Unauthorized
    /// - Expired
    /// - Failed
    /// 
    /// Accepted her zaman final deÄŸildir; araÃ§ komutu kabul edip daha sonra uygulayabilir.
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
