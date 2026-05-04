using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.World.Runtime;

namespace Hydronom.Runtime.Scenarios;

/// <summary>
/// ScenarioDefinition içindeki objeleri runtime world model'e aktarır.
/// </summary>
public sealed class ScenarioRuntimeBinder
{
    /// <summary>
    /// ScenarioDefinition içindeki aktif objeleri RuntimeWorldModel içine aktarır.
    /// </summary>
    public IReadOnlyList<HydronomWorldObject> Bind(
        ScenarioDefinition scenario,
        RuntimeWorldModel worldModel,
        bool clearBeforeBind = true)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(worldModel);

        var worldObjects = scenario.Objects
            .Where(x => x.IsActive)
            .Select(x => ToWorldObject(scenario, x))
            .ToArray();

        if (clearBeforeBind)
        {
            worldModel.Clear();
        }

        worldModel.UpsertMany(worldObjects);

        return worldObjects;
    }

    private static HydronomWorldObject ToWorldObject(
        ScenarioDefinition scenario,
        ScenarioWorldObjectDefinition source)
    {
        var tags = BuildTags(scenario, source);

        return new HydronomWorldObject
        {
            Id = source.Id,
            Kind = Normalize(source.Kind, "unknown"),
            Name = string.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name,
            Layer = Normalize(source.Layer, "mission"),

            X = source.X,
            Y = source.Y,
            Z = source.Z,

            Radius = source.Radius,
            Width = source.Width,
            Height = ResolveHeight(source),

            YawDeg = source.YawDeg,

            IsActive = source.IsActive,
            IsBlocking = ResolveBlocking(source),

            Tags = tags
        };
    }

    private static Dictionary<string, string> BuildTags(
        ScenarioDefinition scenario,
        ScenarioWorldObjectDefinition source)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in source.Tags)
        {
            tags[pair.Key] = pair.Value;
        }

        tags["scenario.id"] = scenario.Id;
        tags["scenario.name"] = scenario.Name;
        tags["scenario.family"] = scenario.ScenarioFamily;
        tags["scenario.version"] = scenario.Version;
        tags["scenario.coordinateFrame"] = scenario.CoordinateFrame;
        tags["scenario.runMode"] = scenario.RunMode;

        tags["vehicle.id"] = scenario.VehicleId;
        tags["vehicle.platform"] = scenario.VehiclePlatform;

        tags["object.id"] = source.Id;
        tags["object.kind"] = Normalize(source.Kind, "unknown");
        tags["object.layer"] = Normalize(source.Layer, "mission");
        tags["object.role"] = source.Role ?? string.Empty;

        tags["object.x"] = source.X.ToString("G17");
        tags["object.y"] = source.Y.ToString("G17");
        tags["object.z"] = source.Z.ToString("G17");

        tags["object.rollDeg"] = source.RollDeg.ToString("G17");
        tags["object.pitchDeg"] = source.PitchDeg.ToString("G17");
        tags["object.yawDeg"] = source.YawDeg.ToString("G17");

        tags["object.radius"] = source.Radius.ToString("G17");
        tags["object.width"] = source.Width.ToString("G17");
        tags["object.height"] = source.Height.ToString("G17");
        tags["object.length"] = source.Length.ToString("G17");

        tags["object.isActive"] = source.IsActive.ToString();
        tags["object.isBlocking"] = ResolveBlocking(source).ToString();
        tags["object.isDetectable"] = source.IsDetectable.ToString();
        tags["object.isJudgeTracked"] = source.IsJudgeTracked.ToString();
        tags["object.isNoGoZone"] = source.IsNoGoZone.ToString();
        tags["object.isTargetZone"] = source.IsTargetZone.ToString();
        tags["object.isGate"] = source.IsGate.ToString();

        tags["object.toleranceMeters"] = source.ToleranceMeters.ToString("G17");
        tags["object.scoreValue"] = source.ScoreValue.ToString("G17");
        tags["object.penaltyValue"] = source.PenaltyValue.ToString("G17");

        tags["object.requiresDirectionCheck"] = source.RequiresDirectionCheck.ToString();
        tags["object.requiredHeadingDeg"] = source.RequiredHeadingDeg.ToString("G17");
        tags["object.headingToleranceDeg"] = source.HeadingToleranceDeg.ToString("G17");

        if (!string.IsNullOrWhiteSpace(source.ObjectiveId))
        {
            tags["objective.id"] = source.ObjectiveId;
        }

        if (!string.IsNullOrWhiteSpace(source.LeftObjectId))
        {
            tags["gate.leftObjectId"] = source.LeftObjectId;
        }

        if (!string.IsNullOrWhiteSpace(source.RightObjectId))
        {
            tags["gate.rightObjectId"] = source.RightObjectId;
        }

        if (!string.IsNullOrWhiteSpace(source.Color))
        {
            tags["visual.color"] = source.Color;
        }

        if (!string.IsNullOrWhiteSpace(source.Label))
        {
            tags["visual.label"] = source.Label;
        }

        if (source.HasPoints)
        {
            tags["geometry.pointCount"] = source.Points.Count.ToString();

            for (var i = 0; i < source.Points.Count; i++)
            {
                var point = source.Points[i];
                tags[$"geometry.point.{i}.x"] = point.X.ToString("G17");
                tags[$"geometry.point.{i}.y"] = point.Y.ToString("G17");

                if (!string.IsNullOrWhiteSpace(point.Label))
                {
                    tags[$"geometry.point.{i}.label"] = point.Label;
                }
            }
        }

        return tags;
    }

    private static double ResolveHeight(ScenarioWorldObjectDefinition source)
    {
        if (source.Height > 0.0)
        {
            return source.Height;
        }

        if (source.Length > 0.0)
        {
            return source.Length;
        }

        return 0.0;
    }

    private static bool ResolveBlocking(ScenarioWorldObjectDefinition source)
    {
        return source.IsBlocking ||
               source.IsNoGoZone ||
               IsBlockingKind(source.Kind);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }

    private static bool IsBlockingKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return kind.Equals("obstacle", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("buoy", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("no_go_zone", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("nogozone", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("boundary", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("wall", StringComparison.OrdinalIgnoreCase);
    }
}