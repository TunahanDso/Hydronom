using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ± veya bir world layer iÃ§in genel sÄ±nÄ±r modeli.
    ///
    /// Bounds, Ops haritasÄ±nda/3D gÃ¶rÃ¼nÃ¼mde viewport ve culling iÃ§in;
    /// sim sensÃ¶rlerde ise kaba Ã§arpÄ±ÅŸma ve algÄ± hÄ±zlandÄ±rma iÃ§in kullanÄ±labilir.
    /// </summary>
    public readonly record struct SimWorldBounds(
        SimBox Box,
        bool IsUnbounded
    )
    {
        public static SimWorldBounds Unbounded => new(
            Box: new SimBox(
                Center: SimVector3.Zero,
                Size: new SimVector3(0.0, 0.0, 0.0),
                Rotation: SimQuaternion.Identity
            ),
            IsUnbounded: true
        );

        public static SimWorldBounds FromBox(SimBox box)
        {
            return new SimWorldBounds(
                Box: box.SanitizedBox(),
                IsUnbounded: false
            );
        }

        public bool IsFinite =>
            IsUnbounded || Box.IsFinite;

        public SimWorldBounds Sanitized()
        {
            if (IsUnbounded)
                return Unbounded;

            return new SimWorldBounds(
                Box: Box.SanitizedBox(),
                IsUnbounded: false
            );
        }

        public bool Contains(SimVector3 point)
        {
            if (IsUnbounded)
                return true;

            return Box.Contains(point);
        }
    }
}
