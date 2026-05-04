using Hydronom.Core.Telemetry.World;
using Hydronom.Core.World.Models;
using Hydronom.Runtime.World.Runtime;

namespace Hydronom.Runtime.World.Projection;

/// <summary>
/// Runtime world model'i telemetry frame'e dönüştürür.
/// Ops/Gateway tarafı ileride bu frame'i okuyarak harita layer'larını gösterebilir.
/// </summary>
public sealed class RuntimeWorldTelemetryProjector
{
    public WorldTelemetryFrame Project(RuntimeWorldModel worldModel)
    {
        ArgumentNullException.ThrowIfNull(worldModel);

        var objects = worldModel.Snapshot();
        var telemetryObjects = objects.Select(ToTelemetry).ToArray();

        var visibleCount = telemetryObjects.Count(x => x.Visible);
        var detectableCount = telemetryObjects.Count(x => x.Detectable);

        return new WorldTelemetryFrame(
            FrameId: Guid.NewGuid().ToString("N"),
            WorldId: "runtime_world",
            DisplayName: "Runtime World",
            TimestampUtc: DateTime.UtcNow,
            Source: "RuntimeWorldModel",
            Objects: telemetryObjects,
            Layers: Array.Empty<WorldLayerTelemetry>(),
            Environment: null,
            MissionObjects: Array.Empty<WorldMissionObjectTelemetry>(),
            ObjectCount: telemetryObjects.Length,
            VisibleObjectCount: visibleCount,
            DetectableObjectCount: detectableCount,
            LayerCount: 0,
            MissionObjectCount: 0,
            Summary: $"RuntimeWorldModel objects={telemetryObjects.Length}, visible={visibleCount}, detectable={detectableCount}"
        ).Sanitized();
    }

    private static WorldObjectTelemetry ToTelemetry(HydronomWorldObject source)
    {
        var safeRadius = SafeNonNegative(source.Radius);
        var safeWidth = SafePositive(source.Width, safeRadius > 0.0 ? safeRadius * 2.0 : 1.0);
        var safeHeight = SafePositive(source.Height, safeRadius > 0.0 ? safeRadius * 2.0 : 1.0);

        var tags = source.Tags
            .Select(x => $"{x.Key}:{x.Value}")
            .Append($"layer:{Normalize(source.Layer, "mission")}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorldObjectTelemetry(
            ObjectId: Normalize(source.Id, "world_object"),
            DisplayName: Normalize(source.Name, source.Id),
            Kind: Normalize(source.Kind, "unknown"),
            State: source.IsActive ? "Active" : "Inactive",
            ShapeKind: ResolveShapeKind(source),
            X: Safe(source.X),
            Y: Safe(source.Y),
            Z: Safe(source.Z),
            Qw: 1.0,
            Qx: 0.0,
            Qy: 0.0,
            Qz: 0.0,
            ScaleX: safeWidth,
            ScaleY: safeHeight,
            ScaleZ: 1.0,
            ColorHex: ResolveColor(source),
            Opacity: source.IsActive ? 1.0 : 0.35,
            Visible: source.IsActive,
            Collidable: source.IsBlocking,
            Detectable: source.IsActive,
            BoundsCenterX: Safe(source.X),
            BoundsCenterY: Safe(source.Y),
            BoundsCenterZ: Safe(source.Z),
            BoundsSizeX: safeWidth,
            BoundsSizeY: safeHeight,
            BoundsSizeZ: 1.0,
            Tags: tags,
            UpdatedUtc: DateTime.UtcNow
        ).Sanitized();
    }

    private static string ResolveShapeKind(HydronomWorldObject source)
    {
        if (source.Radius > 0.0)
        {
            return "Circle";
        }

        if (source.Width > 0.0 || source.Height > 0.0)
        {
            return "Rectangle";
        }

        return "Point";
    }

    private static string ResolveColor(HydronomWorldObject source)
    {
        if (source.Kind.Equals("no_go_zone", StringComparison.OrdinalIgnoreCase))
        {
            return "#EF4444";
        }

        if (source.Kind.Equals("buoy", StringComparison.OrdinalIgnoreCase))
        {
            return "#F59E0B";
        }

        if (source.Kind.Equals("dock", StringComparison.OrdinalIgnoreCase))
        {
            return "#64748B";
        }

        if (source.Kind.Equals("gate", StringComparison.OrdinalIgnoreCase))
        {
            return "#22C55E";
        }

        return source.IsBlocking ? "#EF4444" : "#3B82F6";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static double Safe(double value, double fallback = 0.0)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double SafePositive(double value, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return value <= 0.0 ? fallback : value;
    }

    private static double SafeNonNegative(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        return value < 0.0 ? 0.0 : value;
    }
}