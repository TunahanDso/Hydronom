using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ±ndaki temel nesne modeli.
    ///
    /// Bu model:
    /// - 3D engeller
    /// - gÃ¶rev hedefleri
    /// - ÅŸamandÄ±ralar
    /// - kapÄ±lar
    /// - no-go zone temsilcileri
    /// - dock nesneleri
    /// - replay/ghost marker'larÄ±
    /// - Ops'ta gÃ¶sterilecek world object katmanlarÄ±
    ///
    /// iÃ§in ortak sÃ¶zleÅŸmedir.
    /// </summary>
    public readonly record struct SimWorldObject(
        string ObjectId,
        string DisplayName,
        SimWorldObjectKind Kind,
        SimWorldObjectState State,
        SimWorldTransform Transform,
        SimShapeKind ShapeKind,
        SimShape2D? Shape2D,
        SimShape3D? Shape3D,
        SimWorldMaterial Material,
        IReadOnlyList<SimWorldTag> Tags,
        bool VisibleInOps,
        bool Collidable,
        bool Detectable,
        DateTime CreatedUtc,
        DateTime UpdatedUtc
    )
    {
        public static SimWorldObject Create3D(
            string objectId,
            string displayName,
            SimWorldObjectKind kind,
            SimShape3D shape,
            SimWorldMaterial? material = null
        )
        {
            var now = DateTime.UtcNow;

            return new SimWorldObject(
                ObjectId: Normalize(objectId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(displayName, "World Object"),
                Kind: kind,
                State: SimWorldObjectState.Active,
                Transform: SimWorldTransform.Identity,
                ShapeKind: shape.Kind,
                Shape2D: null,
                Shape3D: shape.Sanitized(),
                Material: (material ?? SimWorldMaterial.Default).Sanitized(),
                Tags: Array.Empty<SimWorldTag>(),
                VisibleInOps: true,
                Collidable: true,
                Detectable: true,
                CreatedUtc: now,
                UpdatedUtc: now
            ).Sanitized();
        }

        public static SimWorldObject Create2D(
            string objectId,
            string displayName,
            SimWorldObjectKind kind,
            SimShape2D shape,
            SimWorldMaterial? material = null
        )
        {
            var now = DateTime.UtcNow;

            return new SimWorldObject(
                ObjectId: Normalize(objectId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(displayName, "World Object"),
                Kind: kind,
                State: SimWorldObjectState.Active,
                Transform: SimWorldTransform.Identity,
                ShapeKind: shape.Kind,
                Shape2D: shape.Sanitized(),
                Shape3D: null,
                Material: (material ?? SimWorldMaterial.Default).Sanitized(),
                Tags: Array.Empty<SimWorldTag>(),
                VisibleInOps: true,
                Collidable: true,
                Detectable: true,
                CreatedUtc: now,
                UpdatedUtc: now
            ).Sanitized();
        }

        public bool Has3DShape => Shape3D is not null;

        public bool Has2DShape => Shape2D is not null;

        public bool IsFinite =>
            Transform.IsFinite &&
            Material.Opacity >= 0.0 &&
            Material.Opacity <= 1.0;

        public SimWorldObject Sanitized()
        {
            var now = DateTime.UtcNow;

            return new SimWorldObject(
                ObjectId: Normalize(ObjectId, Guid.NewGuid().ToString("N")),
                DisplayName: Normalize(DisplayName, "World Object"),
                Kind: Kind,
                State: State,
                Transform: Transform.Sanitized(),
                ShapeKind: ShapeKind,
                Shape2D: Shape2D?.Sanitized(),
                Shape3D: Shape3D?.Sanitized(),
                Material: Material.Sanitized(),
                Tags: NormalizeTags(Tags),
                VisibleInOps: VisibleInOps,
                Collidable: Collidable,
                Detectable: Detectable,
                CreatedUtc: CreatedUtc == default ? now : CreatedUtc,
                UpdatedUtc: UpdatedUtc == default ? now : UpdatedUtc
            );
        }

        public SimWorldObject WithTransform(SimWorldTransform transform)
        {
            return this with
            {
                Transform = transform.Sanitized(),
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimWorldObject WithState(SimWorldObjectState state)
        {
            return this with
            {
                State = state,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public SimWorldObject WithTags(IReadOnlyList<SimWorldTag> tags)
        {
            return this with
            {
                Tags = NormalizeTags(tags),
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public bool HasTag(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || Tags is null)
                return false;

            return Tags.Any(t => string.Equals(t.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public SimBox GetApproxBounds3D()
        {
            if (Shape3D is not null)
                return Shape3D.GetBoundingBox();

            if (Shape2D is not null)
            {
                var rect = Shape2D.GetBoundingRectangle();

                return new SimBox(
                    Center: new SimVector3(rect.Center.X, rect.Center.Y, 0.0),
                    Size: new SimVector3(rect.Width, rect.Height, 0.1),
                    Rotation: SimQuaternion.Identity
                );
            }

            return new SimBox(
                Center: Transform.Pose.Position,
                Size: new SimVector3(0.0, 0.0, 0.0),
                Rotation: Transform.Pose.Rotation
            );
        }

        private static IReadOnlyList<SimWorldTag> NormalizeTags(IReadOnlyList<SimWorldTag>? tags)
        {
            if (tags is null || tags.Count == 0)
                return Array.Empty<SimWorldTag>();

            return tags
                .Select(t => t.Sanitized())
                .Where(t => !string.IsNullOrWhiteSpace(t.Key))
                .ToArray();
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
