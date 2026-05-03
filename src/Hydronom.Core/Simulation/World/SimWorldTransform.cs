using Hydronom.Core.Simulation.World.Geometry;

namespace Hydronom.Core.Simulation.World
{
    /// <summary>
    /// SimÃ¼lasyon dÃ¼nyasÄ± iÃ§in transform modeli.
    ///
    /// Pose pozisyon/yÃ¶nelim bilgisini, Scale ise Ops 3D ve mesh Ã§izimlerinde
    /// kullanÄ±lacak yerel Ã¶lÃ§ek bilgisini temsil eder.
    /// </summary>
    public readonly record struct SimWorldTransform(
        SimWorldPose Pose,
        SimVector3 Scale
    )
    {
        public static SimWorldTransform Identity => new(
            Pose: SimWorldPose.Zero,
            Scale: new SimVector3(1.0, 1.0, 1.0)
        );

        public bool IsFinite =>
            Pose.IsFinite &&
            Scale.IsFinite;

        public SimWorldTransform Sanitized()
        {
            var scale = Scale.Sanitized();

            return new SimWorldTransform(
                Pose: Pose.Sanitized(),
                Scale: new SimVector3(
                    SafePositive(scale.X),
                    SafePositive(scale.Y),
                    SafePositive(scale.Z)
                )
            );
        }

        public SimVector3 TransformPoint(SimVector3 localPoint)
        {
            var safe = Sanitized();
            var p = localPoint.Sanitized();

            var scaled = new SimVector3(
                p.X * safe.Scale.X,
                p.Y * safe.Scale.Y,
                p.Z * safe.Scale.Z
            );

            return safe.Pose.Position + safe.Pose.Rotation.Rotate(scaled);
        }

        private static double SafePositive(double value)
        {
            if (!double.IsFinite(value))
                return 1.0;

            return value <= 0.0 ? 1.0 : value;
        }
    }
}
