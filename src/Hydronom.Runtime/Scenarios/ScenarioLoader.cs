using System.Text.Json;
using Hydronom.Core.Scenarios.Loading;
using Hydronom.Core.Scenarios.Models;

namespace Hydronom.Runtime.Scenarios;

/// <summary>
/// JSON tabanlı Hydronom senaryo yükleyicisi.
/// 
/// Desteklenen formatlar:
/// 1) Legacy single-file:
///    Samples/teknofest_2026_parkur_1_point_tracking.json
///
/// 2) Package folder:
///    Samples/teknofest_2026_parkur_1_point_tracking/scenario.json
///    Samples/teknofest_2026_parkur_1_point_tracking/objects/*.json
///    Samples/teknofest_2026_parkur_1_point_tracking/objectives.json
///    Samples/teknofest_2026_parkur_1_point_tracking/judge.json
///    Samples/teknofest_2026_parkur_1_point_tracking/fault_injections.json
///    Samples/teknofest_2026_parkur_1_point_tracking/metadata.json
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

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<ScenarioDefinition> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Scenario path boş olamaz.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);

        ScenarioDefinition scenario;

        if (Directory.Exists(fullPath))
        {
            scenario = await LoadPackageAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        else if (File.Exists(fullPath))
        {
            scenario = await LoadSingleFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new FileNotFoundException("Scenario dosyası veya package klasörü bulunamadı.", fullPath);
        }

        Validate(scenario, fullPath);

        return scenario;
    }

    private static async Task<ScenarioDefinition> LoadSingleFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        var scenario = await JsonSerializer.DeserializeAsync<ScenarioDefinition>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (scenario is null)
        {
            throw new InvalidOperationException($"Scenario dosyası okunamadı: {path}");
        }

        return scenario;
    }

    private static async Task<ScenarioDefinition> LoadPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(packagePath, "scenario.json");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "Scenario package klasörü içinde scenario.json bulunamadı.",
                manifestPath);
        }

        var scenario = await LoadSingleFileAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        var packagedObjects = await LoadScenarioObjectsAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var packagedObjectives = await LoadScenarioObjectivesAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var packagedJudge = await LoadScenarioJudgeAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var packagedFaults = await LoadScenarioFaultsAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var packagedTags = await LoadScenarioTagsAsync(packagePath, cancellationToken).ConfigureAwait(false);

        var mergedTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in scenario.Tags)
        {
            mergedTags[pair.Key] = pair.Value;
        }

        foreach (var pair in packagedTags)
        {
            mergedTags[pair.Key] = pair.Value;
        }

        mergedTags["scenario.package"] = "true";
        mergedTags["scenario.packagePath"] = packagePath;

        return scenario with
        {
            Objects = MergeLists(scenario.Objects, packagedObjects),
            Objectives = MergeLists(scenario.Objectives, packagedObjectives),
            Judge = packagedJudge ?? scenario.Judge,
            FaultInjections = MergeLists(scenario.FaultInjections, packagedFaults),
            Tags = mergedTags
        };
    }

    private static async Task<IReadOnlyList<ScenarioWorldObjectDefinition>> LoadScenarioObjectsAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var result = new List<ScenarioWorldObjectDefinition>();

        var objectsDirectory = Path.Combine(packagePath, "objects");

        if (Directory.Exists(objectsDirectory))
        {
            var objectFiles = Directory
                .EnumerateFiles(objectsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var file in objectFiles)
            {
                var items = await ReadJsonArrayOrWrappedArrayAsync<ScenarioWorldObjectDefinition>(
                    file,
                    "objects",
                    cancellationToken).ConfigureAwait(false);

                result.AddRange(items);
            }
        }

        var rootObjectsPath = Path.Combine(packagePath, "objects.json");

        if (File.Exists(rootObjectsPath))
        {
            var items = await ReadJsonArrayOrWrappedArrayAsync<ScenarioWorldObjectDefinition>(
                rootObjectsPath,
                "objects",
                cancellationToken).ConfigureAwait(false);

            result.AddRange(items);
        }

        return result;
    }

    private static async Task<IReadOnlyList<ScenarioMissionObjectiveDefinition>> LoadScenarioObjectivesAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(packagePath, "objectives.json");

        if (!File.Exists(path))
        {
            return Array.Empty<ScenarioMissionObjectiveDefinition>();
        }

        return await ReadJsonArrayOrWrappedArrayAsync<ScenarioMissionObjectiveDefinition>(
            path,
            "objectives",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ScenarioJudgeDefinition?> LoadScenarioJudgeAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(packagePath, "judge.json");

        if (!File.Exists(path))
        {
            return null;
        }

        return await ReadJsonObjectOrWrappedObjectAsync<ScenarioJudgeDefinition>(
            path,
            "judge",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ScenarioFaultInjectionDefinition>> LoadScenarioFaultsAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var snakeCasePath = Path.Combine(packagePath, "fault_injections.json");
        var camelCasePath = Path.Combine(packagePath, "faultInjections.json");

        var path = File.Exists(snakeCasePath)
            ? snakeCasePath
            : camelCasePath;

        if (!File.Exists(path))
        {
            return Array.Empty<ScenarioFaultInjectionDefinition>();
        }

        return await ReadJsonArrayOrWrappedArrayAsync<ScenarioFaultInjectionDefinition>(
            path,
            "faultInjections",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string>> LoadScenarioTagsAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(packagePath, "metadata.json");

        if (!File.Exists(metadataPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var text = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text, DocumentOptions);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"metadata.json object olmalı. File={metadataPath}");
        }

        if (TryGetPropertyIgnoreCase(root, "tags", out var tagsElement))
        {
            return ReadStringDictionary(tagsElement, metadataPath);
        }

        return ReadStringDictionary(root, metadataPath);
    }

    private static async Task<IReadOnlyList<T>> ReadJsonArrayOrWrappedArrayAsync<T>(
        string path,
        string wrapperPropertyName,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text, DocumentOptions);
        var root = document.RootElement;

        JsonElement arrayElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            arrayElement = root;
        }
        else if (root.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(root, wrapperPropertyName, out var wrapped) &&
                wrapped.ValueKind == JsonValueKind.Array)
        {
            arrayElement = wrapped;
        }
        else
        {
            throw new InvalidOperationException(
                $"JSON array veya '{wrapperPropertyName}' array property bekleniyordu. File={path}");
        }

        var items = arrayElement.Deserialize<List<T>>(JsonOptions);

        if (items is null)
        {
            return Array.Empty<T>();
        }

        return items;
    }
    private static async Task<T?> ReadJsonObjectOrWrappedObjectAsync<T>(
        string path,
        string wrapperPropertyName,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text, DocumentOptions);
        var root = document.RootElement;

        JsonElement objectElement;

        if (root.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(root, wrapperPropertyName, out var wrapped) &&
            wrapped.ValueKind == JsonValueKind.Object)
        {
            objectElement = wrapped;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            objectElement = root;
        }
        else
        {
            throw new InvalidOperationException(
                $"JSON object veya '{wrapperPropertyName}' object property bekleniyordu. File={path}");
        }

        return objectElement.Deserialize<T>(JsonOptions);
    }

    private static Dictionary<string, string> ReadStringDictionary(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Metadata/tags JSON object olmalı. File={path}");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText()
            };

            result[property.Name] = value;
        }

        return result;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<T> MergeLists<T>(
        IReadOnlyList<T> primary,
        IReadOnlyList<T> secondary)
    {
        if (primary.Count == 0)
        {
            return secondary;
        }

        if (secondary.Count == 0)
        {
            return primary;
        }

        return primary.Concat(secondary).ToArray();
    }

    private static void Validate(ScenarioDefinition scenario, string path)
    {
        ValidateScenarioIdentity(scenario, path);
        ValidateObjects(scenario);
        ValidateObjectives(scenario);
        ValidateFaultInjections(scenario);
        ValidateJudge(scenario);
    }

    private static void ValidateScenarioIdentity(ScenarioDefinition scenario, string path)
    {
        if (string.IsNullOrWhiteSpace(scenario.Id))
        {
            throw new InvalidOperationException($"Scenario Id boş olamaz. File={path}");
        }

        if (string.IsNullOrWhiteSpace(scenario.CoordinateFrame))
        {
            throw new InvalidOperationException($"Scenario CoordinateFrame boş olamaz. Scenario={scenario.Id}");
        }

        if (string.IsNullOrWhiteSpace(scenario.RunMode))
        {
            throw new InvalidOperationException($"Scenario RunMode boş olamaz. Scenario={scenario.Id}");
        }

        if (string.IsNullOrWhiteSpace(scenario.VehicleId))
        {
            throw new InvalidOperationException($"Scenario VehicleId boş olamaz. Scenario={scenario.Id}");
        }

        if (string.IsNullOrWhiteSpace(scenario.VehiclePlatform))
        {
            throw new InvalidOperationException($"Scenario VehiclePlatform boş olamaz. Scenario={scenario.Id}");
        }

        if (scenario.TimeLimitSeconds < 0.0)
        {
            throw new InvalidOperationException($"Scenario TimeLimitSeconds negatif olamaz. Scenario={scenario.Id}");
        }

        if (scenario.MinimumSuccessScore < 0.0)
        {
            throw new InvalidOperationException($"Scenario MinimumSuccessScore negatif olamaz. Scenario={scenario.Id}");
        }
    }

    private static void ValidateObjects(ScenarioDefinition scenario)
    {
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

        foreach (var obj in scenario.Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.Id))
            {
                throw new InvalidOperationException($"Scenario object Id boş olamaz. Scenario={scenario.Id}");
            }

            if (string.IsNullOrWhiteSpace(obj.Kind))
            {
                throw new InvalidOperationException($"Scenario object Kind boş olamaz. Object={obj.Id}");
            }

            if (obj.Radius < 0.0)
            {
                throw new InvalidOperationException($"Scenario object Radius negatif olamaz. Object={obj.Id}");
            }

            if (obj.Width < 0.0)
            {
                throw new InvalidOperationException($"Scenario object Width negatif olamaz. Object={obj.Id}");
            }

            if (obj.Height < 0.0)
            {
                throw new InvalidOperationException($"Scenario object Height negatif olamaz. Object={obj.Id}");
            }

            if (obj.Length < 0.0)
            {
                throw new InvalidOperationException($"Scenario object Length negatif olamaz. Object={obj.Id}");
            }

            if (obj.ToleranceMeters < 0.0)
            {
                throw new InvalidOperationException($"Scenario object ToleranceMeters negatif olamaz. Object={obj.Id}");
            }

            if (obj.ScoreValue < 0.0)
            {
                throw new InvalidOperationException($"Scenario object ScoreValue negatif olamaz. Object={obj.Id}");
            }

            if (obj.PenaltyValue < 0.0)
            {
                throw new InvalidOperationException($"Scenario object PenaltyValue negatif olamaz. Object={obj.Id}");
            }

            if (obj.IsGate)
            {
                ValidateGateObject(scenario, obj);
            }
        }
    }

    private static void ValidateGateObject(ScenarioDefinition scenario, ScenarioWorldObjectDefinition obj)
    {
        if (!obj.HasGateMarkers)
        {
            // Gate merkez objesi marker olmadan da temsil edilebilir.
            // Bu yüzden sadece Width/Radius yoksa hata veriyoruz.
            if (obj.Width <= 0.0 && obj.Radius <= 0.0)
            {
                throw new InvalidOperationException(
                    $"Gate object marker veya Width/Radius bilgisi olmadan değerlendirilemez. Object={obj.Id}");
            }

            return;
        }

        var hasLeft = scenario.Objects.Any(x =>
            string.Equals(x.Id, obj.LeftObjectId, StringComparison.OrdinalIgnoreCase));

        var hasRight = scenario.Objects.Any(x =>
            string.Equals(x.Id, obj.RightObjectId, StringComparison.OrdinalIgnoreCase));

        if (!hasLeft || !hasRight)
        {
            throw new InvalidOperationException(
                $"Gate marker Id'leri scenario içinde bulunamadı. Gate={obj.Id}, Left={obj.LeftObjectId}, Right={obj.RightObjectId}");
        }

        if (obj.HeadingToleranceDeg < 0.0)
        {
            throw new InvalidOperationException($"Gate HeadingToleranceDeg negatif olamaz. Gate={obj.Id}");
        }
    }

    private static void ValidateObjectives(ScenarioDefinition scenario)
    {
        var duplicateIds = scenario.Objectives
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Scenario içinde duplicate objective Id var: {string.Join(", ", duplicateIds)}");
        }

        foreach (var objective in scenario.Objectives)
        {
            if (string.IsNullOrWhiteSpace(objective.Id))
            {
                throw new InvalidOperationException($"Scenario objective Id boş olamaz. Scenario={scenario.Id}");
            }

            if (string.IsNullOrWhiteSpace(objective.Type))
            {
                throw new InvalidOperationException($"Scenario objective Type boş olamaz. Objective={objective.Id}");
            }

            if (objective.ScoreValue < 0.0)
            {
                throw new InvalidOperationException($"Scenario objective ScoreValue negatif olamaz. Objective={objective.Id}");
            }

            if (objective.ToleranceMeters < 0.0)
            {
                throw new InvalidOperationException($"Scenario objective ToleranceMeters negatif olamaz. Objective={objective.Id}");
            }

            if (objective.TimeLimitSeconds < 0.0)
            {
                throw new InvalidOperationException($"Scenario objective TimeLimitSeconds negatif olamaz. Objective={objective.Id}");
            }

            if (!string.IsNullOrWhiteSpace(objective.TargetObjectId))
            {
                var targetExists = scenario.Objects.Any(x =>
                    string.Equals(x.Id, objective.TargetObjectId, StringComparison.OrdinalIgnoreCase));

                if (!targetExists)
                {
                    throw new InvalidOperationException(
                        $"Scenario objective target object bulunamadı. Objective={objective.Id}, Target={objective.TargetObjectId}");
                }
            }
        }
    }

    private static void ValidateFaultInjections(ScenarioDefinition scenario)
    {
        var duplicateIds = scenario.FaultInjections
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Scenario içinde duplicate fault injection Id var: {string.Join(", ", duplicateIds)}");
        }

        foreach (var fault in scenario.FaultInjections)
        {
            if (string.IsNullOrWhiteSpace(fault.Id))
            {
                throw new InvalidOperationException($"Scenario fault injection Id boş olamaz. Scenario={scenario.Id}");
            }

            if (string.IsNullOrWhiteSpace(fault.Type))
            {
                throw new InvalidOperationException($"Scenario fault injection Type boş olamaz. Fault={fault.Id}");
            }

            if (string.IsNullOrWhiteSpace(fault.Target))
            {
                throw new InvalidOperationException($"Scenario fault injection Target boş olamaz. Fault={fault.Id}");
            }

            if (fault.StartAtSeconds < 0.0)
            {
                throw new InvalidOperationException($"Scenario fault StartAtSeconds negatif olamaz. Fault={fault.Id}");
            }

            if (fault.DurationSeconds < 0.0)
            {
                throw new InvalidOperationException($"Scenario fault DurationSeconds negatif olamaz. Fault={fault.Id}");
            }

            if (fault.Severity < 0.0)
            {
                throw new InvalidOperationException($"Scenario fault Severity negatif olamaz. Fault={fault.Id}");
            }
        }
    }

    private static void ValidateJudge(ScenarioDefinition scenario)
    {
        var judge = scenario.Judge;
        if (judge.CollisionPenalty < 0.0)
        {
            throw new InvalidOperationException($"Judge CollisionPenalty negatif olamaz. Scenario={scenario.Id}");
        }

        if (judge.NoGoZonePenalty < 0.0)
        {
            throw new InvalidOperationException($"Judge NoGoZonePenalty negatif olamaz. Scenario={scenario.Id}");
        }

        if (judge.DegradedOperationPenalty < 0.0)
        {
            throw new InvalidOperationException($"Judge DegradedOperationPenalty negatif olamaz. Scenario={scenario.Id}");
        }
    }
}