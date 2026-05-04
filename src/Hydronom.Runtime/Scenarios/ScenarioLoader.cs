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