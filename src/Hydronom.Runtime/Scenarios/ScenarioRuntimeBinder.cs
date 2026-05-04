using Hydronom.Core.Scenarios.Models;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.World.Runtime;

namespace Hydronom.Runtime.Scenarios;

/// <summary>
/// ScenarioDefinition içindeki objeleri runtime world model'e aktarır.
/// </summary>
public sealed class ScenarioRuntimeBinder
{
    public IReadOnlyList<HydronomWorldObject> Bind(
        ScenarioDefinition scenario,
        RuntimeWorldModel worldModel,
        bool clearBeforeBind = true)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(worldModel);

        var worldObjects = scenario.Objects
            .Where(x => x.IsActive)
            .Select(ToWorldObject)
            .ToArray();

        if (clearBeforeBind)
        {
            worldModel.Clear();
        }

        worldModel.UpsertMany(worldObjects);

        return worldObjects;
    }

    private static HydronomWorldObject ToWorldObject(ScenarioWorldObjectDefinition source)
    {
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
            Height = source.Height,
            YawDeg = source.YawDeg,
            IsActive = source.IsActive,
            IsBlocking = source.IsBlocking || IsBlockingKind(source.Kind),
            Tags = new Dictionary<string, string>(source.Tags, StringComparer.OrdinalIgnoreCase)
        };
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
            || kind.Equals("no_go_zone", StringComparison.OrdinalIgnoreCase);
    }
}