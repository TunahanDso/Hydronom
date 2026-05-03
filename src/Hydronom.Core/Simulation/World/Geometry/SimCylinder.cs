using System;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 3D silindir hacmi.
    ///
    /// ÅamandÄ±ra, direk, kolon, sualtÄ± boru, silindirik hedef, sonar/radar hacmi
    /// gibi nesneleri temsil etmek iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimCylinder(
        SimVector3 Center,
        double Radius,
        double Height,
        SimQuaternion Rotation
    ) : SimShape3D
    {
        public SimShapeKind Kind => SimShapeKind.Cylinder;

        public bool IsFinite =>
            Center.IsFinite &&
            double.IsFinite(Radius) &&
            double.IsFinite(Height) &&
            Rotation.IsFinite;

        public SimShape3D Sanitized()
        {
            return new SimCylinder(
                Center.Sanitized(),
                SafeNonNegative(Radius),
                SafeNonNegative(Height),
                Rotation.Sanitized()
            );
        }

        public SimCylinder SanitizedCylinder()
        {
            return (SimCylinder)Sanitized();
        }

        public bool Contains(SimVector3 point)
        {
            var safe = SanitizedCylinder();
            var p = point.Sanitized();

            // Ä°lk sÃ¼rÃ¼mde Z ekseni boyunca axis-aligned silindir kabul edilir.
            // Rotation ileride geliÅŸmiÅŸ collision ve Ops 3D Ã§izimi iÃ§in korunur.
            double dx = p.X - safe.Center.X;
            double dy = p.Y - safe.Center.Y;
            double radial = Math.Sqrt(dx * dx + dy * dy);

            return radial <= safe.Radius &&
                   Math.Abs(p.Z - safe.Center.Z) <= safe.Height * 0.5;
        }

        public SimBox GetBoundingBox()
        {
            var safe = SanitizedCylinder();

            return new SimBox(
                Center: safe.Center,
                Size: new SimVector3(
                    safe.Radius * 2.0,
                    safe.Radius * 2.0,
                    safe.Height
                ),
                Rotation: safe.Rotation
            );
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }
    }
}
