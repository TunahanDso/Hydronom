using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.Scenarios.Mission;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed partial class RuntimeScenarioController
{
    private IReadOnlyList<RuntimeScenarioWorldObject> BuildWorldObjectsUnsafe()
    {
        if (_scenario is not null && _scenario.Objects.Count > 0)
            return BuildScenarioDefinitionWorldObjectsUnsafe(_scenario);

        return BuildMissionDerivedWorldObjectsUnsafe();
    }

    private IReadOnlyList<RuntimeScenarioWorldObject> BuildScenarioDefinitionWorldObjectsUnsafe(
        ScenarioDefinition scenario)
    {
        var objects = scenario.Objects
            .Where(x => x.IsActive)
            .Select(ToRuntimeScenarioWorldObject)
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        foreach (var obj in objects)
        {
            if (!string.IsNullOrWhiteSpace(obj.ObjectiveId) &&
                _session is not null &&
                _session.CompletedObjectiveIds.Contains(obj.ObjectiveId))
            {
                obj.IsCompleted = true;
            }
        }

        return objects;
    }

    private IReadOnlyList<RuntimeScenarioWorldObject> BuildMissionDerivedWorldObjectsUnsafe()
    {
        var objects = new List<RuntimeScenarioWorldObject>();

        if (_plan is not null)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = "start",
                Type = "start",
                Kind = "start_zone",
                Role = "start",
                Layer = "mission",
                Label = "START",
                Name = "START",
                X = 0.0,
                Y = 0.0,
                Z = ResolveDefaultStartZUnsafe(),
                Radius = 0.8,
                Width = 1.6,
                Height = 1.6,
                Color = "#38bdf8",
                IsActive = true
            });

            foreach (var target in _plan.Targets.Select((value, index) => new { value, index }))
            {
                var isLast = target.index == _plan.Targets.Count - 1;
                var objectiveId = target.value.ObjectiveId;

                objects.Add(new RuntimeScenarioWorldObject
                {
                    Id = objectiveId,
                    Type = isLast ? "finish" : "checkpoint",
                    Kind = isLast ? "finish" : "waypoint",
                    Role = isLast ? "finish" : "waypoint",
                    Layer = "mission",
                    Label = isLast ? "FINISH" : BuildObjectiveLabel(objectiveId, target.index),
                    Name = isLast ? "FINISH" : BuildObjectiveLabel(objectiveId, target.index),
                    ObjectiveId = objectiveId,
                    X = target.value.Target.X,
                    Y = target.value.Target.Y,
                    Z = target.value.Target.Z,
                    Radius = target.value.ToleranceMeters,
                    Width = target.value.ToleranceMeters * 2.0,
                    Height = target.value.ToleranceMeters * 2.0,
                    Color = isLast ? "#f97316" : "#facc15",
                    IsActive = true,
                    IsCompleted = _session is not null &&
                                  _session.CompletedObjectiveIds.Contains(objectiveId),
                    IsBlocking = false,
                    IsDetectable = false
                });
            }
        }

        if (IsSurfaceTeknofestParkur1Unsafe())
            AddTeknofestParkur1Buoys(objects);

        if (IsSurfaceTeknofestParkur2Unsafe())
            AddTeknofestParkur2Objects(objects);

        return objects;
    }

    private static RuntimeScenarioWorldObject ToRuntimeScenarioWorldObject(
        ScenarioWorldObjectDefinition definition)
    {
        var type = ResolveRuntimeObjectType(definition);
        var radius = ResolveRadius(definition.Radius, definition.Width, definition.Height);
        var side = ResolveSide(definition);
        var tags = CloneTags(definition.Tags);

        return new RuntimeScenarioWorldObject
        {
            Id = NormalizeTagValue(definition.Id, "scenario-object"),
            Type = type,
            Kind = NormalizeOptional(definition.Kind),
            Name = NormalizeOptional(definition.Name),
            Layer = NormalizeOptional(definition.Layer),
            Role = NormalizeOptional(definition.Role),
            Label = NormalizeOptional(definition.Label),
            ObjectiveId = NormalizeOptional(definition.ObjectiveId),
            Side = side,

            X = definition.X,
            Y = definition.Y,
            Z = definition.Z,

            RollDeg = definition.RollDeg,
            PitchDeg = definition.PitchDeg,
            YawDeg = definition.YawDeg,

            Radius = radius,
            Width = definition.Width > 0.0 ? definition.Width : radius * 2.0,
            Height = definition.Height > 0.0 ? definition.Height : radius * 2.0,
            Length = definition.Length,

            Color = NormalizeOptional(definition.Color),

            IsActive = definition.IsActive,
            IsBlocking = definition.IsBlocking,
            IsDetectable = definition.IsDetectable,
            IsJudgeTracked = definition.IsJudgeTracked,
            IsNoGoZone = definition.IsNoGoZone,
            IsTargetZone = definition.IsTargetZone,
            IsGate = definition.IsGate,

            LeftObjectId = NormalizeOptional(definition.LeftObjectId),
            RightObjectId = NormalizeOptional(definition.RightObjectId),

            ToleranceMeters = definition.ToleranceMeters,
            RequiresDirectionCheck = definition.RequiresDirectionCheck,
            RequiredHeadingDeg = definition.RequiredHeadingDeg,
            HeadingToleranceDeg = definition.HeadingToleranceDeg,

            ScoreValue = definition.ScoreValue,
            PenaltyValue = definition.PenaltyValue,

            Tags = tags
        };
    }

    private static string ResolveRuntimeObjectType(ScenarioWorldObjectDefinition definition)
    {
        var kind = NormalizeOptional(definition.Kind);
        var role = NormalizeOptional(definition.Role);
        var layer = NormalizeOptional(definition.Layer);

        if (definition.IsNoGoZone ||
            TextEquals(kind, "no_go_zone") ||
            TextEquals(role, "no_go_zone"))
            return "no_go_zone";

        if (TextEquals(role, "start") || TextEquals(kind, "start_zone"))
            return "start_zone";

        if (TextEquals(role, "finish"))
            return "finish";

        if (TextEquals(role, "waypoint") || TextEquals(kind, "waypoint") || definition.IsTargetZone)
            return "waypoint";

        if (TextEquals(role, "gate_left"))
            return "gate_left";

        if (TextEquals(role, "gate_right"))
            return "gate_right";

        if (role is not null && role.Contains("boundary", StringComparison.OrdinalIgnoreCase))
            return "boundary";

        if (TextEquals(role, "obstacle") || TextEquals(layer, "obstacle"))
            return "obstacle";

        if (!string.IsNullOrWhiteSpace(kind))
            return kind;

        if (!string.IsNullOrWhiteSpace(role))
            return role;

        if (!string.IsNullOrWhiteSpace(layer))
            return layer;

        return "object";
    }

    private static string? ResolveSide(ScenarioWorldObjectDefinition definition)
    {
        if (definition.Tags.TryGetValue("gate.side", out var gateSide) &&
            !string.IsNullOrWhiteSpace(gateSide))
            return gateSide.Trim();

        var role = definition.Role ?? string.Empty;
        var id = definition.Id ?? string.Empty;

        if (role.Contains("left", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("_left", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("-left", StringComparison.OrdinalIgnoreCase))
            return "left";

        if (role.Contains("right", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("_right", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("-right", StringComparison.OrdinalIgnoreCase))
            return "right";

        return null;
    }

    private static Dictionary<string, string> CloneTags(
        IReadOnlyDictionary<string, string> source)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in source)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) &&
                !string.IsNullOrWhiteSpace(pair.Value))
            {
                tags[pair.Key.Trim()] = pair.Value.Trim();
            }
        }

        return tags;
    }

    private static double ResolveRadius(double radius, double width, double height)
    {
        if (double.IsFinite(radius) && radius > 0.0)
            return radius;

        var maxDimension = Math.Max(width, height);

        return double.IsFinite(maxDimension) && maxDimension > 0.0
            ? maxDimension / 2.0
            : 0.5;
    }

    private double ResolveDefaultStartZUnsafe()
    {
        if (_plan is null || _plan.Targets.Count == 0)
            return 0.0;

        var firstTargetZ = _plan.Targets[0].Target.Z;

        return double.IsFinite(firstTargetZ)
            ? firstTargetZ
            : 0.0;
    }

    private bool IsUnderwaterScenarioUnsafe()
    {
        var scenarioId = _plan?.ScenarioId ?? string.Empty;
        var scenarioName = _plan?.ScenarioName ?? string.Empty;
        var vehicleId = _plan?.VehicleId ?? string.Empty;

        return
            scenarioId.Contains("uuv", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("underwater", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("sualti", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("su_alti", StringComparison.OrdinalIgnoreCase) ||
            scenarioId.Contains("submarine", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("sualtı", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("su altı", StringComparison.OrdinalIgnoreCase) ||
            scenarioName.Contains("underwater", StringComparison.OrdinalIgnoreCase) ||
            vehicleId.Contains("uuv", StringComparison.OrdinalIgnoreCase) ||
            vehicleId.Contains("underwater", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSurfaceTeknofestParkur1Unsafe()
    {
        if (IsUnderwaterScenarioUnsafe())
            return false;

        var scenarioId = _plan?.ScenarioId ?? string.Empty;

        return scenarioId.Contains("teknofest_2026_parkur_1", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("parkur_1_point_tracking", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSurfaceTeknofestParkur2Unsafe()
    {
        if (IsUnderwaterScenarioUnsafe())
            return false;

        var scenarioId = _plan?.ScenarioId ?? string.Empty;

        return scenarioId.Contains("teknofest_2026_parkur_2", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("parkur_2", StringComparison.OrdinalIgnoreCase) ||
               scenarioId.Contains("obstacle_point_tracking", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddTeknofestParkur1Buoys(List<RuntimeScenarioWorldObject> objects)
    {
        var leftXs = new[] { 8.0, 20.0, 32.0, 44.0 };
        var rightXs = new[] { 8.0, 20.0, 32.0, 44.0 };

        for (var i = 0; i < leftXs.Length; i++)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = $"parkur1-left-buoy-{i + 1}",
                Type = "gate_left",
                Kind = "buoy",
                Role = "gate_left",
                Layer = "navigation",
                Label = $"L-{i + 1}",
                Name = $"L-{i + 1}",
                X = leftXs[i],
                Y = 8.0,
                Z = 0.0,
                Radius = 0.45,
                Width = 0.9,
                Height = 0.9,
                Color = "#22c55e",
                Side = "left",
                IsActive = true,
                IsBlocking = true,
                IsDetectable = true
            });
        }

        for (var i = 0; i < rightXs.Length; i++)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = $"parkur1-right-buoy-{i + 1}",
                Type = "gate_right",
                Kind = "buoy",
                Role = "gate_right",
                Layer = "navigation",
                Label = $"R-{i + 1}",
                Name = $"R-{i + 1}",
                X = rightXs[i],
                Y = -8.0,
                Z = 0.0,
                Radius = 0.45,
                Width = 0.9,
                Height = 0.9,
                Color = "#ef4444",
                Side = "right",
                IsActive = true,
                IsBlocking = true,
                IsDetectable = true
            });
        }
    }

    private static void AddTeknofestParkur2Objects(List<RuntimeScenarioWorldObject> objects)
    {
        AddGate(objects, 1, 10.0, 0.0);
        AddGate(objects, 2, 22.0, 0.0);
        AddGate(objects, 3, 34.0, 0.0);

        AddObstacle(objects, "parkur2-obstacle-1", "OBS-1", 15.0, 1.2, 0.8);
        AddObstacle(objects, "parkur2-obstacle-2", "OBS-2", 28.0, -1.4, 0.9);
        AddObstacle(objects, "parkur2-obstacle-3", "OBS-3", 40.0, 1.8, 0.9);
    }

    private static void AddGate(
        List<RuntimeScenarioWorldObject> objects,
        int index,
        double x,
        double centerY)
    {
        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = $"parkur2-gate-{index}-left",
            Type = "gate_left",
            Kind = "buoy",
            Role = "gate_left",
            Layer = "navigation",
            Label = $"G{index}-L",
            Name = $"G{index}-L",
            X = x,
            Y = centerY + 3.0,
            Z = 0.0,
            Radius = 0.45,
            Width = 0.9,
            Height = 0.9,
            Color = "#22c55e",
            Side = "left",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });

        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = $"parkur2-gate-{index}-right",
            Type = "gate_right",
            Kind = "buoy",
            Role = "gate_right",
            Layer = "navigation",
            Label = $"G{index}-R",
            Name = $"G{index}-R",
            X = x,
            Y = centerY - 3.0,
            Z = 0.0,
            Radius = 0.45,
            Width = 0.9,
            Height = 0.9,
            Color = "#ef4444",
            Side = "right",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });
    }

    private static void AddObstacle(
        List<RuntimeScenarioWorldObject> objects,
        string id,
        string label,
        double x,
        double y,
        double radius)
    {
        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = id,
            Type = "obstacle",
            Kind = "obstacle",
            Role = "obstacle",
            Layer = "obstacle",
            Label = label,
            Name = label,
            X = x,
            Y = y,
            Z = 0.0,
            Radius = radius,
            Width = radius * 2.0,
            Height = radius * 2.0,
            Color = "#f43f5e",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });
    }

    private void UpdateRuntimeWorldModelUnsafe()
    {
        if (_runtimeWorld is null)
            return;

        var scenarioObjects = BuildWorldObjectsUnsafe();

        var worldObjects = scenarioObjects
            .Where(x => x.IsActive)
            .Select(obj => ToHydronomWorldObject(obj, _plan, _session, ResolveRuntimeVehicleId()))
            .ToArray();

        _runtimeWorld.UpsertMany(worldObjects);
    }

    private void ClearRuntimeWorldUnsafe()
    {
        _runtimeWorld?.Clear();
    }

    private static HydronomWorldObject ToHydronomWorldObject(
        RuntimeScenarioWorldObject obj,
        ScenarioMissionPlan? plan,
        RuntimeScenarioSession? session,
        string runtimeVehicleId)
    {
        var kind = NormalizeWorldObjectKind(obj);
        var layer = ResolveWorldLayer(obj, kind);

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "scenario",
            ["scenarioObject"] = "true",
            ["type"] = NormalizeTagValue(obj.Type, "object"),
            ["kind"] = kind,
            ["layer"] = layer,
            ["runtimeVehicleId"] = NormalizeTagValue(runtimeVehicleId, DefaultRuntimeVehicleId),
            ["isBlocking"] = obj.IsBlocking ? "true" : "false",
            ["isDetectable"] = obj.IsDetectable ? "true" : "false",
            ["isCompleted"] = obj.IsCompleted ? "true" : "false",
            ["isActive"] = obj.IsActive ? "true" : "false",
            ["isJudgeTracked"] = obj.IsJudgeTracked ? "true" : "false",
            ["isNoGoZone"] = obj.IsNoGoZone ? "true" : "false",
            ["isTargetZone"] = obj.IsTargetZone ? "true" : "false",
            ["isGate"] = obj.IsGate ? "true" : "false"
        };

        MergeObjectTags(tags, obj.Tags);

        AddTagIfPresent(tags, "label", obj.Label);
        AddTagIfPresent(tags, "name", obj.Name);
        AddTagIfPresent(tags, "objectiveId", obj.ObjectiveId);
        AddTagIfPresent(tags, "side", obj.Side);
        AddTagIfPresent(tags, "color", obj.Color);
        AddTagIfPresent(tags, "role", obj.Role);
        AddTagIfPresent(tags, "objectLayer", obj.Layer);
        AddTagIfPresent(tags, "physicalKind", obj.Kind);
        AddTagIfPresent(tags, "leftObjectId", obj.LeftObjectId);
        AddTagIfPresent(tags, "rightObjectId", obj.RightObjectId);

        if (obj.ToleranceMeters is not null)
            tags["toleranceMeters"] = obj.ToleranceMeters.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (obj.RequiredHeadingDeg is not null)
            tags["requiredHeadingDeg"] = obj.RequiredHeadingDeg.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (obj.HeadingToleranceDeg is not null)
            tags["headingToleranceDeg"] = obj.HeadingToleranceDeg.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (obj.ScoreValue is not null)
            tags["scoreValue"] = obj.ScoreValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (obj.PenaltyValue is not null)
            tags["penaltyValue"] = obj.PenaltyValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (obj.RequiresDirectionCheck)
            tags["requiresDirectionCheck"] = "true";

        if (plan is not null)
        {
            AddTagIfPresent(tags, "scenarioId", plan.ScenarioId);
            AddTagIfPresent(tags, "scenarioName", plan.ScenarioName);
            AddTagIfPresent(tags, "scenarioVehicleId", plan.VehicleId);
        }

        if (session is not null)
        {
            AddTagIfPresent(tags, "currentObjectiveId", session.CurrentObjectiveId);
            tags["scenarioState"] = session.State.ToString();
        }

        if (obj.Type.Equals("checkpoint", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("waypoint", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("finish", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("start", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("start_zone", StringComparison.OrdinalIgnoreCase))
        {
            tags["missionMarker"] = "true";
        }

        return new HydronomWorldObject
        {
            Id = obj.Id,
            Kind = kind,
            Name = NormalizeTagValue(obj.Label, NormalizeTagValue(obj.Name, obj.Id)),
            Layer = layer,
            X = obj.X,
            Y = obj.Y,
            Z = obj.Z,
            Radius = obj.Radius,
            Width = ResolveDimension(obj.Width, obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0),
            Height = ResolveDimension(obj.Height, obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0),
            YawDeg = obj.YawDeg ?? 0.0,
            IsActive = obj.IsActive,
            IsBlocking = obj.IsBlocking,
            Tags = tags
        };
    }

    private static string ResolveWorldLayer(RuntimeScenarioWorldObject obj, string kind)
    {
        var explicitLayer = NormalizeOptional(obj.Layer);

        if (!string.IsNullOrWhiteSpace(explicitLayer))
        {
            if (explicitLayer.Equals("mission", StringComparison.OrdinalIgnoreCase))
                return "scenario_mission";

            if (explicitLayer.Equals("navigation", StringComparison.OrdinalIgnoreCase))
                return "scenario_navigation";

            if (explicitLayer.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return "scenario_obstacles";

            if (explicitLayer.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                return "scenario_boundary";

            if (explicitLayer.Equals("safety", StringComparison.OrdinalIgnoreCase))
                return "scenario_safety";

            return $"scenario_{explicitLayer}";
        }

        if (kind.Equals("no_go_zone", StringComparison.OrdinalIgnoreCase))
            return "scenario_safety";

        if (obj.IsBlocking || kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
            return "scenario_obstacles";

        if (kind.Equals("gate_left", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("gate_right", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
            return "scenario_navigation";

        return "scenario_mission";
    }

    private static string NormalizeWorldObjectKind(RuntimeScenarioWorldObject obj)
    {
        var type = NormalizeOptional(obj.Type);
        var role = NormalizeOptional(obj.Role);
        var kind = NormalizeOptional(obj.Kind);
        var layer = NormalizeOptional(obj.Layer);

        if (obj.IsNoGoZone ||
            TextEquals(type, "no_go_zone") ||
            TextEquals(role, "no_go_zone") ||
            TextEquals(kind, "no_go_zone"))
            return "no_go_zone";

        if (TextEquals(type, "start_zone") ||
            TextEquals(role, "start") ||
            TextEquals(kind, "start_zone"))
            return "start_zone";

        if (TextEquals(type, "finish") || TextEquals(role, "finish"))
            return "finish";

        if (TextEquals(type, "waypoint") ||
            TextEquals(type, "checkpoint") ||
            TextEquals(role, "waypoint") ||
            TextEquals(kind, "waypoint") ||
            obj.IsTargetZone)
            return "waypoint";

        if (TextEquals(type, "gate_left") || TextEquals(role, "gate_left"))
            return "gate_left";

        if (TextEquals(type, "gate_right") || TextEquals(role, "gate_right"))
            return "gate_right";

        if ((role is not null && role.Contains("boundary", StringComparison.OrdinalIgnoreCase)) ||
            TextEquals(type, "boundary") ||
            TextEquals(layer, "boundary"))
            return "boundary";

        if (TextEquals(role, "obstacle") ||
            TextEquals(type, "obstacle") ||
            TextEquals(layer, "obstacle"))
            return "obstacle";

        if (!string.IsNullOrWhiteSpace(kind))
            return kind;

        if (!string.IsNullOrWhiteSpace(type))
            return type;

        return "object";
    }

    private static void MergeObjectTags(
        Dictionary<string, string> tags,
        IReadOnlyDictionary<string, string> objectTags)
    {
        foreach (var pair in objectTags)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value))
                continue;

            var key = pair.Key.Trim();
            var value = pair.Value.Trim();

            if (tags.ContainsKey(key))
                tags[$"object.{key}"] = value;
            else
                tags[key] = value;
        }
    }

    private static void AddTagIfPresent(
        Dictionary<string, string> tags,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            tags[key] = value.Trim();
    }

    private static string NormalizeTagValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static double ResolveDimension(double? value, double fallback)
    {
        return value is > 0.0 && double.IsFinite(value.Value)
            ? value.Value
            : fallback;
    }

    private static string BuildObjectiveLabel(string objectiveId, int index)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
            return $"WP-{index + 1}";

        if (objectiveId.Contains("finish", StringComparison.OrdinalIgnoreCase))
            return "FINISH";

        if (objectiveId.Contains("wp_", StringComparison.OrdinalIgnoreCase) ||
            objectiveId.Contains("wp-", StringComparison.OrdinalIgnoreCase) ||
            objectiveId.Contains("reach_wp", StringComparison.OrdinalIgnoreCase))
            return $"WP-{index + 1}";

        return objectiveId;
    }
}