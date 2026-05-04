namespace Hydronom.Core.Scenarios.Models;

/// <summary>
/// Hydronom senaryo tanımı.
/// JSON parkur dosyasının bellekteki karşılığıdır.
/// </summary>
public sealed record ScenarioDefinition
{
    /// <summary>
    /// Senaryo kimliği.
    /// Örnek: hydrocontest_basic_course
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// İnsan tarafından okunabilir senaryo adı.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo açıklaması.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Senaryo sürümü.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Koordinat sistemi açıklaması.
    /// Şimdilik local_metric varsayılır.
    /// </summary>
    public string CoordinateFrame { get; init; } = "local_metric";

    /// <summary>
    /// Senaryodaki dünya/parkur objeleri.
    /// </summary>
    public IReadOnlyList<ScenarioWorldObjectDefinition> Objects { get; init; }
        = Array.Empty<ScenarioWorldObjectDefinition>();

    /// <summary>
    /// Genel senaryo metadata alanı.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasObjects => Objects.Count > 0;
}