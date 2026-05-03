using System;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 2D dikdÃ¶rtgen ÅŸekli.
    ///
    /// Åimdilik Contains hesabÄ± axis-aligned bounding yaklaÅŸÄ±mÄ±yla yapÄ±lÄ±r.
    /// YawDeg Ops Ã§izimi ve ileride dÃ¶ndÃ¼rÃ¼lmÃ¼ÅŸ rectangle desteÄŸi iÃ§in tutulur.
    /// </summary>
    public readonly record struct SimRectangle(
        SimVector2 Center,
        double Width,
        double Height,
        double YawDeg
    ) : SimShape2D
    {
        public SimShapeKind Kind => SimShapeKind.Rectangle;

        public bool IsFinite =>
            Center.IsFinite &&
            double.IsFinite(Width) &&
            double.IsFinite(Height) &&
            double.IsFinite(YawDeg);

        public SimShape2D Sanitized()
        {
            return new SimRectangle(
                Center.Sanitized(),
                SafeNonNegative(Width),
                SafeNonNegative(Height),
                NormalizeDeg(YawDeg)
            );
        }

        public SimRectangle SanitizedRectangle()
        {
            return (SimRectangle)Sanitized();
        }

        public bool Contains(SimVector2 point)
        {
            var safe = SanitizedRectangle();
            var p = point.Sanitized();

            double halfW = safe.Width * 0.5;
            double halfH = safe.Height * 0.5;

            return Math.Abs(p.X - safe.Center.X) <= halfW &&
                   Math.Abs(p.Y - safe.Center.Y) <= halfH;
        }

        public SimRectangle GetBoundingRectangle()
        {
            return SanitizedRectangle();
        }

        public SimVector2 Min
        {
            get
            {
                var safe = SanitizedRectangle();
                return new SimVector2(
                    safe.Center.X - safe.Width * 0.5,
                    safe.Center.Y - safe.Height * 0.5
                );
            }
        }

        public SimVector2 Max
        {
            get
            {
                var safe = SanitizedRectangle();
                return new SimVector2(
                    safe.Center.X + safe.Width * 0.5,
                    safe.Center.Y + safe.Height * 0.5
                );
            }
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return value < 0.0 ? 0.0 : value;
        }

        private static double NormalizeDeg(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }
    }
}
