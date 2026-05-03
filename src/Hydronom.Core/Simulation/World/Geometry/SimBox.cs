using System;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 3D kutu hacmi.
    ///
    /// 3D engeller, yapÄ± bloklarÄ±, gÃ¶rev hacimleri, sualtÄ± tarama bÃ¶lgeleri ve
    /// Ops 3D tactical gÃ¶rÃ¼nÃ¼mÃ¼ndeki temel hacimler iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimBox(
        SimVector3 Center,
        SimVector3 Size,
        SimQuaternion Rotation
    ) : SimShape3D
    {
        public SimShapeKind Kind => SimShapeKind.Box;

        public bool IsFinite =>
            Center.IsFinite &&
            Size.IsFinite &&
            Rotation.IsFinite;

        public SimShape3D Sanitized()
        {
            return new SimBox(
                Center.Sanitized(),
                new SimVector3(
                    SafeNonNegative(Size.X),
                    SafeNonNegative(Size.Y),
                    SafeNonNegative(Size.Z)
                ),
                Rotation.Sanitized()
            );
        }

        public SimBox SanitizedBox()
        {
            return (SimBox)Sanitized();
        }

        public bool Contains(SimVector3 point)
        {
            var safe = SanitizedBox();
            var p = point.Sanitized();

            // Ä°lk sÃ¼rÃ¼mde axis-aligned kontrol yapÄ±lÄ±r.
            // Rotation Ops Ã§izimi ve ileride geliÅŸmiÅŸ collision iÃ§in korunur.
            var delta = p - safe.Center;

            return Math.Abs(delta.X) <= safe.Size.X * 0.5 &&
                   Math.Abs(delta.Y) <= safe.Size.Y * 0.5 &&
                   Math.Abs(delta.Z) <= safe.Size.Z * 0.5;
        }

        public SimBox GetBoundingBox()
        {
            return SanitizedBox();
        }

        public SimVector3 Min
        {
            get
            {
                var safe = SanitizedBox();

                return new SimVector3(
                    safe.Center.X - safe.Size.X * 0.5,
                    safe.Center.Y - safe.Size.Y * 0.5,
                    safe.Center.Z - safe.Size.Z * 0.5
                );
            }
        }

        public SimVector3 Max
        {
            get
            {
                var safe = SanitizedBox();

                return new SimVector3(
                    safe.Center.X + safe.Size.X * 0.5,
                    safe.Center.Y + safe.Size.Y * 0.5,
                    safe.Center.Z + safe.Size.Z * 0.5
                );
            }
        }

        public static SimBox FromMinMax(SimVector3 min, SimVector3 max)
        {
            var safeMin = min.Sanitized();
            var safeMax = max.Sanitized();

            var center = new SimVector3(
                (safeMin.X + safeMax.X) * 0.5,
                (safeMin.Y + safeMax.Y) * 0.5,
                (safeMin.Z + safeMax.Z) * 0.5
            );

            var size = new SimVector3(
                Math.Abs(safeMax.X - safeMin.X),
                Math.Abs(safeMax.Y - safeMin.Y),
                Math.Abs(safeMax.Z - safeMin.Z)
            );

            return new SimBox(center, size, SimQuaternion.Identity);
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
