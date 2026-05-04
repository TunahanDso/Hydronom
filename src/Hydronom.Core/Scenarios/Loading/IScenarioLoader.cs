using Hydronom.Core.Scenarios.Models;

namespace Hydronom.Core.Scenarios.Loading;

/// <summary>
/// Senaryo dosyalarını Hydronom senaryo modeline çeviren yükleyici kontratı.
/// </summary>
public interface IScenarioLoader
{
    /// <summary>
    /// Verilen dosya yolundan senaryo yükler.
    /// </summary>
    Task<ScenarioDefinition> LoadAsync(string path, CancellationToken cancellationToken = default);
}