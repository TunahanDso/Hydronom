using System.Text.Json;
using Hydronom.Core.Scenarios.Loading;
using Hydronom.Core.Scenarios.Models;

namespace Hydronom.Runtime.Scenarios;

/// <summary>
/// JSON tabanlı Hydronom senaryo yükleyicisi.
/// </summary>
public sealed class ScenarioLoader : IScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public async Task<ScenarioDefinition> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Scenario path boş olamaz.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Scenario dosyası bulunamadı.", path);
        }

        await using var stream = File.OpenRead(path);

        var scenario = await JsonSerializer.DeserializeAsync<ScenarioDefinition>(
            stream,
            JsonOptions,
            cancellationToken);

        if (scenario is null)
        {
            throw new InvalidOperationException($"Scenario dosyası okunamadı: {path}");
        }

        Validate(scenario, path);

        return scenario;
    }

    private static void Validate(ScenarioDefinition scenario, string path)
    {
        if (string.IsNullOrWhiteSpace(scenario.Id))
        {
            throw new InvalidOperationException($"Scenario Id boş olamaz. File={path}");
        }

        var duplicateIds = scenario.Objects
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Scenario içinde duplicate object Id var: {string.Join(", ", duplicateIds)}");
        }
    }
}