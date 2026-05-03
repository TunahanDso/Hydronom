namespace Hydronom.GroundStation.Coordination;

/// <summary>
/// MissionAllocator tarafÄ±ndan Ã¼retilen gÃ¶rev atama sonucunu temsil eder.
/// 
/// Bu model Ground Station'a ÅŸunu sÃ¶yler:
/// - GÃ¶rev atanabildi mi?
/// - Hangi araÃ§ seÃ§ildi?
/// - Neden o araÃ§ seÃ§ildi?
/// - Uygun adaylar kimlerdi?
/// - Reddedilen/uygun olmayan araÃ§lar neden elendi?
/// 
/// Ä°leride Hydronom Ops tarafÄ±nda gÃ¶rev atama kararÄ±nÄ± operatÃ¶re aÃ§Ä±klamak iÃ§in kullanÄ±labilir.
/// </summary>
public sealed record MissionAllocationResult
{
    /// <summary>
    /// Ä°lgili gÃ¶rev kimliÄŸi.
    /// </summary>
    public string MissionId { get; init; } = string.Empty;

    /// <summary>
    /// GÃ¶rev baÅŸarÄ±yla bir araca atanabildi mi?
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// SeÃ§ilen araÃ§/node kimliÄŸi.
    /// 
    /// Success false ise boÅŸ kalabilir.
    /// </summary>
    public string SelectedNodeId { get; init; } = string.Empty;

    /// <summary>
    /// SeÃ§ilen aracÄ±n insan tarafÄ±ndan okunabilir adÄ±.
    /// </summary>
    public string SelectedDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Atama kararÄ±nÄ±n kÄ±sa aÃ§Ä±klamasÄ±.
    /// 
    /// Ã–rnek:
    /// - "Alpha selected because it satisfies all required capabilities."
    /// - "No online vehicle satisfies required capabilities."
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// SeÃ§ilen aracÄ±n hesaplanan uygunluk skoru.
    /// 
    /// Daha yÃ¼ksek skor daha uygun araÃ§ anlamÄ±na gelir.
    /// Ä°lk fazda basit bir puanlama modeli kullanacaÄŸÄ±z.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// GÃ¶rev iÃ§in deÄŸerlendirilen aday araÃ§ kimlikleri.
    /// </summary>
    public IReadOnlyList<string> CandidateNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Uygun bulunmayan araÃ§lar ve kÄ±sa ret sebepleri.
    /// 
    /// Key:
    /// - NodeId
    /// 
    /// Value:
    /// - Ret sebebi
    /// </summary>
    public IReadOnlyDictionary<string, string> RejectedNodeReasons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Atama sonucunun Ã¼retildiÄŸi UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// BaÅŸarÄ±sÄ±z atama sonucu Ã¼retir.
    /// </summary>
    public static MissionAllocationResult Failed(
        MissionRequest request,
        string reason,
        IReadOnlyDictionary<string, string>? rejectedNodeReasons = null)
    {
        return new MissionAllocationResult
        {
            MissionId = request?.MissionId ?? string.Empty,
            Success = false,
            Reason = reason,
            RejectedNodeReasons = rejectedNodeReasons ?? new Dictionary<string, string>()
        };
    }
}
