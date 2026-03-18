namespace HydronomOps.Gateway.Contracts.Common;

/// <summary>
/// Veri tazelik bilgisini taşır.
/// </summary>
public sealed class FreshnessDto
{
    /// <summary>
    /// Verinin üretim zamanı (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Şu ana göre veri yaşı.
    /// </summary>
    public double AgeMs { get; set; }

    /// <summary>
    /// Veri taze kabul ediliyor mu.
    /// </summary>
    public bool IsFresh { get; set; }

    /// <summary>
    /// Tazelik eşiği.
    /// </summary>
    public double ThresholdMs { get; set; }

    /// <summary>
    /// Kaynağın etiketi.
    /// </summary>
    public string Source { get; set; } = "runtime";
}