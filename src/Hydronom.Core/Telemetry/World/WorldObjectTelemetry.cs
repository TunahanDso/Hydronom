using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.World;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Telemetry.World
{
    /// <summary>
    /// Ops/Gateway/Ground Station tarafÄ±na gÃ¶nderilecek dÃ¼nya nesnesi telemetry modeli.
    ///
    /// Bu model SimWorldObject'in UI ve telemetry iÃ§in sadeleÅŸtirilmiÅŸ halidir.
    /// Ama yine de 2D/3D gÃ¶rÃ¼nÃ¼m, renk, gÃ¶rÃ¼nÃ¼rlÃ¼k, Ã§arpÄ±ÅŸma, algÄ±lanabilirlik ve bounds
    /// gibi kritik bilgileri taÅŸÄ±r.
    /// </summary>
    public readonly record struct WorldObjectTelemetry(
        string ObjectId,
        string DisplayName,
        string Kind,
        string State,
        string ShapeKind,
        double X,
        double Y,
        double Z,
        double Qw,
        double Qx,
        double Qy,
        double Qz,
        double ScaleX,
        double ScaleY,
        double ScaleZ,
        string ColorHex,
        double Opacity,
        bool Visible,
        bool Collidable,
        bool Detectable,
        double BoundsCenterX,
        double BoundsCenterY,
        double BoundsCenterZ,
        double BoundsSizeX,
        double BoundsSizeY,
        double BoundsSizeZ,
        IReadOnlyList<string> Tags,
        DateTime UpdatedUtc
    )
    {
        public static WorldObjectTelemetry FromWorldObject(SimWorldObject obj)
        {
            var safe = obj.Sanitized();
            var transform = safe.Transform.Sanitized();
            var pose = transform.Pose.Sanitized();
            var scale = transform.Scale.Sanitized();
            var material = safe.Material.Sanitized();
            var bounds = safe.GetApproxBounds3D().SanitizedBox();

            return new WorldObjectTelemetry(
                ObjectId: safe.ObjectId,
                DisplayName: safe.DisplayName,
                Kind: safe.Kind.ToString(),
                State: safe.State.ToString(),
                ShapeKind: safe.ShapeKind.ToString(),
                X: pose.Position.X,
                Y: pose.Position.Y,
                Z: pose.Position.Z,
                Qw: pose.Rotation.W,
                Qx: pose.Rotation.X,
                Qy: pose.Rotation.Y,
                Qz: pose.Rotation.Z,
                ScaleX: scale.X,
                ScaleY: scale.Y,
                ScaleZ: scale.Z,
                ColorHex: material.ColorHex,
                Opacity: material.Opacity,
                Visible: safe.VisibleInOps,
                Collidable: safe.Collidable,
                Detectable: safe.Detectable,
                BoundsCenterX: bounds.Center.X,
                BoundsCenterY: bounds.Center.Y,
                BoundsCenterZ: bounds.Center.Z,
                BoundsSizeX: bounds.Size.X,
                BoundsSizeY: bounds.Size.Y,
                BoundsSizeZ: bounds.Size.Z,
                Tags: NormalizeTags(safe.Tags),
                UpdatedUtc: safe.UpdatedUtc
            );
        }

        public WorldObjectTelemetry Sanitized()
        {
            return new WorldObjectTelemetry(
                ObjectId: Normalize(ObjectId, "world_object"),
                DisplayName: Normalize(DisplayName, "World Object"),
                Kind: Normalize(Kind, "Unknown"),
                State: Normalize(State, "Unknown"),
                ShapeKind: Normalize(ShapeKind, "Unknown"),
                X: Safe(X),
                Y: Safe(Y),
                Z: Safe(Z),
                Qw: Safe(Qw, 1.0),
                Qx: Safe(Qx),
                Qy: Safe(Qy),
                Qz: Safe(Qz),
                ScaleX: SafePositive(ScaleX, 1.0),
                ScaleY: SafePositive(ScaleY, 1.0),
                ScaleZ: SafePositive(ScaleZ, 1.0),
                ColorHex: NormalizeColor(ColorHex),
                Opacity: Clamp01(Opacity),
                Visible: Visible,
                Collidable: Collidable,
                Detectable: Detectable,
                BoundsCenterX: Safe(BoundsCenterX),
                BoundsCenterY: Safe(BoundsCenterY),
                BoundsCenterZ: Safe(BoundsCenterZ),
                BoundsSizeX: SafeNonNegative(BoundsSizeX),
                BoundsSizeY: SafeNonNegative(BoundsSizeY),
                BoundsSizeZ: SafeNonNegative(BoundsSizeZ),
                Tags: NormalizeStringList(Tags),
                UpdatedUtc: UpdatedUtc == default ? DateTime.UtcNow : UpdatedUtc
            );
        }

        private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<SimWorldTag>? tags)
        {
            if (tags is null || tags.Count == 0)
                return Array.Empty<string>();

            return tags
                .Select(t => $"{t.Key}:{t.Value}")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
                return Array.Empty<string>();

            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "#9CA3AF";

            var color = value.Trim();

            if (!color.StartsWith("#"))
                color = "#" + color;

            return color.Length == 7 ? color : "#9CA3AF";
        }

        private static double Safe(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static double SafePositive(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return value <= 0.0 ? fallback : value;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }

        private static double Clamp01(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            if (value < 0.0)
                return 0.0;

            if (value > 1.0)
                return 1.0;

            return value;
        }
    }
}
