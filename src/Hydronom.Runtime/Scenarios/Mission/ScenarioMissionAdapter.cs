using System.Reflection;
using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;
using Hydronom.Core.Scenarios.Models;

namespace Hydronom.Runtime.Scenarios.Mission;

/// <summary>
/// ScenarioDefinition içindeki objective/world-object bilgisini runtime görev sisteminin
/// anlayabileceği TaskDefinition hedeflerine dönüştürür.
///
/// Gerçek runtime entegrasyonunun ilk kapısıdır:
///
/// Scenario JSON
///   → ScenarioMissionPlan
///   → ScenarioMissionTarget
///   → TaskDefinition.GoTo(...)
///   → ITaskManager.SetTask(...)
///   → AdvancedDecision.Decide(...)
///
/// Artık TaskDefinition yapısı net olduğu için reflection ile TaskDefinition üretmiyoruz.
/// Reflection yalnızca scenario/objective tarafındaki opsiyonel alanları toleranslı okumak için kullanılır.
/// </summary>
public sealed class ScenarioMissionAdapter
{
    /// <summary>
    /// ScenarioDefinition içinden sıralı runtime mission plan üretir.
    /// </summary>
    public ScenarioMissionPlan BuildPlan(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var targets = scenario.Objectives
            .Cast<object>()
            .Select(objective => TryBuildTarget(scenario, objective))
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.ObjectiveId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targets.Length == 0)
        {
            throw new InvalidOperationException(
                $"Scenario mission plan üretilemedi. Scenario={scenario.Id}, objective/target eşleşmesi yok.");
        }

        return new ScenarioMissionPlan
        {
            ScenarioId = Normalize(scenario.Id, "unknown_scenario"),
            ScenarioName = Normalize(scenario.Name, scenario.Id),
            VehicleId = Normalize(ReadString(scenario, "VehicleId"), "hydronom-main"),
            VehiclePlatform = Normalize(ReadString(scenario, "VehiclePlatform", "Platform"), "unknown"),
            ScenarioFamily = Normalize(ReadString(scenario, "ScenarioFamily", "Family"), string.Empty),
            CreatedUtc = DateTime.UtcNow,
            Targets = targets,
            TimeLimitSeconds = ReadNullableDouble(scenario, "TimeLimitSeconds", "MaxDurationSeconds"),
            MinimumSuccessScore = ReadDouble(scenario, "MinimumSuccessScore", fallback: 0.0),
            SourceScenario = scenario,
            Tags = new Dictionary<string, string>(scenario.Tags, StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Planın ilk hedefini görev yöneticisine yükler.
    /// </summary>
    public ScenarioMissionTarget ApplyFirstTarget(
        ScenarioMissionPlan plan,
        ITaskManager taskManager)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(taskManager);

        var first = plan.FirstTarget;

        if (first is null)
        {
            throw new InvalidOperationException(
                $"Scenario mission plan hedef içermiyor. Scenario={plan.ScenarioId}");
        }

        ApplyTarget(first, taskManager);
        return first;
    }

    /// <summary>
    /// Belirli objective Id için hedefi görev yöneticisine yükler.
    /// </summary>
    public ScenarioMissionTarget ApplyObjective(
        ScenarioMissionPlan plan,
        string objectiveId,
        ITaskManager taskManager)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(taskManager);

        var target = plan.FindTargetByObjectiveId(objectiveId);

        if (target is null)
        {
            throw new InvalidOperationException(
                $"Objective plan içinde bulunamadı. Scenario={plan.ScenarioId}, Objective={objectiveId}");
        }

        ApplyTarget(target, taskManager);
        return target;
    }

    /// <summary>
    /// Verilen scenario mission target'ı TaskDefinition haline getirip task manager'a yükler.
    /// </summary>
    public TaskDefinition ApplyTarget(
        ScenarioMissionTarget target,
        ITaskManager taskManager)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(taskManager);

        var task = ToTaskDefinition(target);
        taskManager.SetTask(task);

        return task;
    }

    /// <summary>
    /// Verilen hedefi runtime TaskDefinition modeline dönüştürür.
    /// </summary>
    public TaskDefinition ToTaskDefinition(ScenarioMissionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var task = TaskDefinition.GoTo(
            name: target.TaskName,
            target: target.Target,
            holdOnArrive: target.HoldOnArrive);

        task.Waypoints.Clear();
        task.Waypoints.Add(target.Target);

        task.WaitSecondsPerPoint = target.HoldOnArrive ? 1.0 : 0.0;
        task.Loop = false;

        return task;
    }

    /// <summary>
    /// Plan hedeflerinin tamamını tek Route task olarak üretir.
    /// Bu daha sonra gerçek runtime'ın tüm parkuru tek görev olarak alması için kullanılacak.
    /// İlk entegrasyonda tek tek GoTo hedefleri kullanılacak.
    /// </summary>
    public TaskDefinition ToRouteTaskDefinition(
        ScenarioMissionPlan plan,
        bool loop = false,
        bool holdOnArrive = true,
        double waitSecondsPerPoint = 0.0)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.HasTargets)
        {
            throw new InvalidOperationException(
                $"Route task üretilemedi; plan hedef içermiyor. Scenario={plan.ScenarioId}");
        }

        var waypoints = plan.Targets
            .OrderBy(x => x.Order)
            .ThenBy(x => x.ObjectiveId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Target)
            .ToArray();

        return TaskDefinition.Route(
            name: $"ScenarioRoute:{plan.ScenarioId}",
            waypoints: waypoints,
            loop: loop,
            holdOnArrive: holdOnArrive,
            waitSecondsPerPoint: waitSecondsPerPoint);
    }

    /// <summary>
    /// Scenario objective + hedef object üzerinden mission target üretir.
    /// </summary>
    private static ScenarioMissionTarget? TryBuildTarget(
        ScenarioDefinition scenario,
        object objective)
    {
        var targetObjectId = ReadString(
            objective,
            "TargetObjectId",
            "TargetId",
            "ObjectId",
            "WorldObjectId");

        if (string.IsNullOrWhiteSpace(targetObjectId))
        {
            return null;
        }

        var worldObject = scenario.Objects.FirstOrDefault(x =>
            string.Equals(x.Id, targetObjectId, StringComparison.OrdinalIgnoreCase));

        if (worldObject is null)
        {
            return null;
        }

        var order = ReadInt(objective, "Order", "Index", "Sequence", fallback: 0);
        var objectiveId = Normalize(
            ReadString(objective, "Id", "ObjectiveId", "Name"),
            $"objective_{order}");

        var objectiveTitle = Normalize(
            ReadString(objective, "Title", "Name", "Label"),
            objectiveId);

        var objectiveType = Normalize(
            ReadString(objective, "Type", "Kind", "ObjectiveType"),
            "reach_object");

        var tolerance = ResolveTolerance(objective, worldObject);

        return new ScenarioMissionTarget
        {
            ScenarioId = Normalize(scenario.Id, "unknown_scenario"),
            ObjectiveId = objectiveId,
            Order = order,
            Title = objectiveTitle,
            TargetObjectId = Normalize(targetObjectId, worldObject.Id),
            Target = new Vec3(
                Sanitize(worldObject.X),
                Sanitize(worldObject.Y),
                Sanitize(worldObject.Z)),
            ToleranceMeters = tolerance,
            Kind = Normalize(worldObject.Kind, "unknown"),
            Layer = Normalize(worldObject.Layer, "mission"),
            IsRequired = ReadBool(objective, "IsRequired", "Required", fallback: true),
            HoldOnArrive = ShouldHoldOnArrive(objectiveType, worldObject.Kind),
            Tags = MergeTags(objective, worldObject, objectiveType)
        };
    }

    private static bool ShouldHoldOnArrive(string objectiveType, string? worldObjectKind)
    {
        if (string.Equals(objectiveType, "hold_position", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(objectiveType, "inspect_object", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(worldObjectKind, "dock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static double ResolveTolerance(
        object objective,
        ScenarioWorldObjectDefinition worldObject)
    {
        var objectiveTolerance = ReadNullableDouble(
            objective,
            "ToleranceMeters",
            "ToleranceM",
            "RadiusMeters",
            "SuccessRadiusMeters");

        if (objectiveTolerance is > 0.0 && double.IsFinite(objectiveTolerance.Value))
        {
            return objectiveTolerance.Value;
        }

        if (worldObject.Radius > 0.0 && double.IsFinite(worldObject.Radius))
        {
            return worldObject.Radius;
        }

        return 1.0;
    }

    private static IReadOnlyDictionary<string, string> MergeTags(
        object objective,
        ScenarioWorldObjectDefinition worldObject,
        string objectiveType)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in worldObject.Tags)
        {
            tags[pair.Key] = pair.Value;
        }

        foreach (var pair in ReadTags(objective))
        {
            tags[pair.Key] = pair.Value;
        }

        tags["objective.type"] = objectiveType;
        tags["target.kind"] = worldObject.Kind ?? string.Empty;
        tags["target.layer"] = worldObject.Layer ?? string.Empty;

        return tags;
    }

    private static string? ReadString(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = source
                .GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(source);
            var text = value?.ToString()?.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static int ReadInt(object source, string propertyName, string alternateName, string thirdName, int fallback)
    {
        return ReadInt(source, new[] { propertyName, alternateName, thirdName }, fallback);
    }

    private static int ReadInt(object source, string propertyName, int fallback)
    {
        return ReadInt(source, new[] { propertyName }, fallback);
    }

    private static int ReadInt(object source, string[] propertyNames, int fallback)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = source
                .GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(source);

            if (value is int i)
            {
                return i;
            }

            if (value is IConvertible convertible)
            {
                try
                {
                    return convertible.ToInt32(System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    // Okuma hatası mission adapter'ı düşürmemeli; sıradaki alan denenir.
                }
            }

            if (int.TryParse(value?.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static double ReadDouble(object source, string propertyName, double fallback)
    {
        return ReadNullableDouble(source, propertyName) ?? fallback;
    }

    private static double? ReadNullableDouble(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = source
                .GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(source);

            if (value is double d && double.IsFinite(d))
            {
                return d;
            }

            if (value is float f && float.IsFinite(f))
            {
                return f;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return l;
            }

            if (double.TryParse(
                    value?.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed) &&
                double.IsFinite(parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool ReadBool(object source, string propertyName, string alternateName, bool fallback)
    {
        foreach (var property in new[] { propertyName, alternateName })
        {
            var value = ReadBoolNullable(source, property);

            if (value.HasValue)
            {
                return value.Value;
            }
        }

        return fallback;
    }

    private static bool? ReadBoolNullable(object source, string propertyName)
    {
        var property = source
            .GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(source);

        if (value is bool b)
        {
            return b;
        }

        if (bool.TryParse(value?.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ReadTags(object source)
    {
        var property = source
            .GetType()
            .GetProperty("Tags", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property?.GetValue(source) is IReadOnlyDictionary<string, string> readOnly)
        {
            return readOnly;
        }

        if (property?.GetValue(source) is IDictionary<string, string> dictionary)
        {
            return new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static double Sanitize(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}