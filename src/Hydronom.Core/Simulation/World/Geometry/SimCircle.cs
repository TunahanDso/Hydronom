using System;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 2D daire ÅŸekli.
    ///
    /// GÃ¶rev hedef yarÄ±Ã§apÄ±, gÃ¼venli yaklaÅŸma bÃ¶lgesi, no-go circle veya sensÃ¶r etki alanÄ±
    /// gibi kullanÄ±mlar iÃ§in uygundur.
    /// </summary>
    public readonly record struct SimCircle(
        SimVector2 Center,
        double Radius
    ) : SimShape2D
    {
        public SimShapeKind Kind => SimShapeKind.Circle;

        public bool IsFinite =>
            Center.IsFinite &&
            double.IsFinite(Radius);

        public SimShape2D Sanitized()
        {
            return new SimCircle(
                Center.Sanitized(),
                SafeNonNegative(Radius)
            );
        }

        public SimCircle SanitizedCircle()
        {
            return (SimCircle)Sanitized();
        }

        public bool Contains(SimVector2 point)
        {
            var safe = SanitizedCircle();
            return safe.Center.DistanceTo(point.Sanitized()) <= safe.Radius;
        }

        public SimRectangle GetBoundingRectangle()
        {
            var safe = SanitizedCircle();

            return new SimRectangle(
                Center: safe.Center,
                Width: safe.Radius * 2.0,
                Height: safe.Radius * 2.0,
                YawDeg: 0.0
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
