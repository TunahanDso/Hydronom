using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.Scenarios.Mission;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed partial class RuntimeScenarioController
{
    private IReadOnlyList<RuntimeScenarioWorldObject> BuildWorldObjectsUnsafe()
    {
        var objects = new List<RuntimeScenarioWorldObject>();

        if (_plan is not null)
        {
            objects.Add(new RuntimeScenarioWorldObject
            {
                Id = "start",
                Type = "start",
                Label = "START",
                X = 0.0,
                Y = 0.0,
                Z = ResolveDefaultStartZUnsafe(),
                Radius = 0.8,
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
                    Label = isLast ? "FINISH" : BuildObjectiveLabel(objectiveId, target.index),
                    ObjectiveId = objectiveId,
                    X = target.value.Target.X,
                    Y = target.value.Target.Y,
                    Z = target.value.Target.Z,
                    Radius = target.value.ToleranceMeters,
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
                Type = "buoy",
                Label = $"L-{i + 1}",
                X = leftXs[i],
                Y = 8.0,
                Z = 0.0,
                Radius = 0.45,
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
                Type = "buoy",
                Label = $"R-{i + 1}",
                X = rightXs[i],
                Y = -8.0,
                Z = 0.0,
                Radius = 0.45,
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
            Type = "buoy",
            Label = $"G{index}-L",
            X = x,
            Y = centerY + 3.0,
            Z = 0.0,
            Radius = 0.45,
            Color = "#22c55e",
            Side = "left",
            IsActive = true,
            IsBlocking = true,
            IsDetectable = true
        });

        objects.Add(new RuntimeScenarioWorldObject
        {
            Id = $"parkur2-gate-{index}-right",
            Type = "buoy",
            Label = $"G{index}-R",
            X = x,
            Y = centerY - 3.0,
            Z = 0.0,
            Radius = 0.45,
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
            Label = label,
            X = x,
            Y = y,
            Z = 0.0,
            Radius = radius,
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
            ["isActive"] = obj.IsActive ? "true" : "false"
        };

        AddTagIfPresent(tags, "label", obj.Label);
        AddTagIfPresent(tags, "objectiveId", obj.ObjectiveId);
        AddTagIfPresent(tags, "side", obj.Side);
        AddTagIfPresent(tags, "color", obj.Color);

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

        var gateIndex = TryExtractGateIndex(obj);
        if (gateIndex is not null)
        {
            tags["gateIndex"] = gateIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tags["gateSide"] = NormalizeTagValue(obj.Side, "unknown");
            tags["corridorMarker"] = "true";
        }

        var parkur1Index = TryExtractParkur1BuoyIndex(obj);
        if (parkur1Index is not null)
        {
            tags["gateIndex"] = parkur1Index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tags["gateSide"] = NormalizeTagValue(obj.Side, "unknown");
            tags["corridorMarker"] = "true";
            tags["parkur"] = "1";
        }

        if (obj.Type.Equals("checkpoint", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("finish", StringComparison.OrdinalIgnoreCase) ||
            obj.Type.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            tags["missionMarker"] = "true";
        }

        return new HydronomWorldObject
        {
            Id = obj.Id,
            Kind = kind,
            Name = NormalizeTagValue(obj.Label, obj.Id),
            Layer = layer,
            X = obj.X,
            Y = obj.Y,
            Z = obj.Z,
            Radius = obj.Radius,
            Width = obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0,
            Height = obj.Radius > 0.0 ? obj.Radius * 2.0 : 1.0,
            YawDeg = 0.0,
            IsActive = obj.IsActive,
            IsBlocking = obj.IsBlocking,
            Tags = tags
        };
    }

    private static string ResolveWorldLayer(RuntimeScenarioWorldObject obj, string kind)
    {
        if (obj.IsBlocking || kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
            return "scenario_obstacles";

        if (kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
            return "scenario_corridor";

        return "scenario_mission";
    }

    private static string NormalizeWorldObjectKind(RuntimeScenarioWorldObject obj)
    {
        if (obj.IsBlocking || obj.IsDetectable)
        {
            if (obj.Type.Equals("obstacle", StringComparison.OrdinalIgnoreCase))
                return "obstacle";

            if (obj.Type.Equals("buoy", StringComparison.OrdinalIgnoreCase))
                return "buoy";

            return "obstacle";
        }

        if (string.IsNullOrWhiteSpace(obj.Type))
            return "object";

        return obj.Type.Trim();
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

    private static int? TryExtractGateIndex(RuntimeScenarioWorldObject obj)
    {
        if (!obj.Id.StartsWith("parkur2-gate-", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = obj.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        return int.TryParse(parts[2], out var index)
            ? index
            : null;
    }

    private static int? TryExtractParkur1BuoyIndex(RuntimeScenarioWorldObject obj)
    {
        if (!obj.Id.StartsWith("parkur1-", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = obj.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return null;

        return int.TryParse(parts[^1], out var index)
            ? index
            : null;
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