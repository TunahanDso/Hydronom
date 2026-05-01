namespace Hydronom.GroundStation.Coordination;

/// <summary>
/// MissionAllocator tarafından üretilen görev atama sonucunu temsil eder.
/// 
/// Bu model Ground Station'a şunu söyler:
/// - Görev atanabildi mi?
/// - Hangi araç seçildi?
/// - Neden o araç seçildi?
/// - Uygun adaylar kimlerdi?
/// - Reddedilen/uygun olmayan araçlar neden elendi?
/// 
/// İleride Hydronom Ops tarafında görev atama kararını operatöre açıklamak için kullanılabilir.
/// </summary>
public sealed record MissionAllocationResult
{
    /// <summary>
    /// İlgili görev kimliği.
    /// </summary>
    public string MissionId { get; init; } = string.Empty;

    /// <summary>
    /// Görev başarıyla bir araca atanabildi mi?
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Seçilen araç/node kimliği.
    /// 
    /// Success false ise boş kalabilir.
    /// </summary>
    public string SelectedNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Seçilen aracın insan tarafından okunabilir adı.
    /// </summary>
    public string SelectedDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Atama kararının kısa açıklaması.
    /// 
    /// Örnek:
    /// - "Alpha selected because it satisfies all required capabilities."
    /// - "No online vehicle satisfies required capabilities."
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Seçilen aracın hesaplanan uygunluk skoru.
    /// 
    /// Daha yüksek skor daha uygun araç anlamına gelir.
    /// İlk fazda basit bir puanlama modeli kullanacağız.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Görev için değerlendirilen aday araç kimlikleri.
    /// </summary>
    public IReadOnlyList<string> CandidateNodeIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Uygun bulunmayan araçlar ve kısa ret sebepleri.
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
    /// Atama sonucunun üretildiği UTC zaman.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Başarısız atama sonucu üretir.
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