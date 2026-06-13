namespace HydronomOps.Gateway.Contracts.Common;

/// <summary>
/// Veri tazelik bilgisini taÅŸÄ±r.
/// </summary>
public sealed class FreshnessDto
{
    /// <summary>
    /// Verinin Ã¼retim zamanÄ± (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Åu ana gÃ¶re veri yaÅŸÄ±.
    /// </summary>
    public double AgeMs { get; set; }

    /// <summary>
    /// Veri taze kabul ediliyor mu.
    /// </summary>
    public bool IsFresh { get; set; }

    /// <summary>
    /// Tazelik eÅŸiÄŸi.
    /// </summary>
    public double ThresholdMs { get; set; }

    /// <summary>
    /// KaynaÄŸÄ±n etiketi.
    /// </summary>
    public string Source { get; set; } = "runtime";
}
