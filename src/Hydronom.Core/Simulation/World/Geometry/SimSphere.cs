namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 3D kÃ¼re hacmi.
    ///
    /// YaklaÅŸma alanÄ±, hedef kapsama alanÄ±, gÃ¼venlik balonu veya radar/sonar menzil hacmi
    /// gibi kullanÄ±mlar iÃ§in uygundur.
    /// </summary>
    public readonly record struct SimSphere(
        SimVector3 Center,
        double Radius
    ) : SimShape3D
    {
        public SimShapeKind Kind => SimShapeKind.Sphere;

        public bool IsFinite =>
            Center.IsFinite &&
            double.IsFinite(Radius);

        public SimShape3D Sanitized()
        {
            return new SimSphere(
                Center.Sanitized(),
                SafeNonNegative(Radius)
            );
        }

        public SimSphere SanitizedSphere()
        {
            return (SimSphere)Sanitized();
        }

        public bool Contains(SimVector3 point)
        {
            var safe = SanitizedSphere();
            return safe.Center.DistanceTo(point.Sanitized()) <= safe.Radius;
        }

        public SimBox GetBoundingBox()
        {
            var safe = SanitizedSphere();

            return new SimBox(
                Center: safe.Center,
                Size: new SimVector3(
                    safe.Radius * 2.0,
                    safe.Radius * 2.0,
                    safe.Radius * 2.0
                ),
                Rotation: SimQuaternion.Identity
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
