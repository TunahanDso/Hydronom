using System;
using System.Collections.Generic;
using System.Linq;

namespace Hydronom.Core.Simulation.World.Geometry
{
    /// <summary>
    /// 2D poligon ÅŸekli.
    ///
    /// No-go zone, inspection zone, gÃ¶rev alanÄ±, gÃ¼venli koridor ve operasyon sÄ±nÄ±rÄ±
    /// gibi alanlar iÃ§in kullanÄ±lÄ±r.
    /// </summary>
    public readonly record struct SimPolygon(
        IReadOnlyList<SimVector2> Points
    ) : SimShape2D
    {
        public SimShapeKind Kind => SimShapeKind.Polygon;

        public SimVector2 Center
        {
            get
            {
                var points = SafePoints();
                if (points.Count == 0)
                    return SimVector2.Zero;

                double x = 0.0;
                double y = 0.0;

                foreach (var p in points)
                {
                    x += p.X;
                    y += p.Y;
                }

                return new SimVector2(x / points.Count, y / points.Count).Sanitized();
            }
        }

        public bool IsFinite
        {
            get
            {
                foreach (var p in SafePoints())
                {
                    if (!p.IsFinite)
                        return false;
                }

                return true;
            }
        }

        public SimShape2D Sanitized()
        {
            return new SimPolygon(SafePoints());
        }

        public SimPolygon SanitizedPolygon()
        {
            return (SimPolygon)Sanitized();
        }

        public bool Contains(SimVector2 point)
        {
            var points = SafePoints();
            var p = point.Sanitized();

            if (points.Count < 3)
                return false;

            bool inside = false;
            int j = points.Count - 1;

            for (int i = 0; i < points.Count; i++)
            {
                var pi = points[i];
                var pj = points[j];

                bool intersect =
                    ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                    (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / SafeDenominator(pj.Y - pi.Y) + pi.X);

                if (intersect)
                    inside = !inside;

                j = i;
            }

            return inside;
        }

        public SimRectangle GetBoundingRectangle()
        {
            var points = SafePoints();

            if (points.Count == 0)
                return new SimRectangle(SimVector2.Zero, 0.0, 0.0, 0.0);

            var min = points[0];
            var max = points[0];

            foreach (var p in points)
            {
                min = SimVector2.Min(min, p);
                max = SimVector2.Max(max, p);
            }

            var center = new SimVector2(
                (min.X + max.X) * 0.5,
                (min.Y + max.Y) * 0.5
            );

            return new SimRectangle(
                Center: center,
                Width: Math.Max(0.0, max.X - min.X),
                Height: Math.Max(0.0, max.Y - min.Y),
                YawDeg: 0.0
            );
        }

        private IReadOnlyList<SimVector2> SafePoints()
        {
            if (Points is null || Points.Count == 0)
                return Array.Empty<SimVector2>();

            return Points
                .Select(p => p.Sanitized())
                .Where(p => p.IsFinite)
                .ToArray();
        }

        private static double SafeDenominator(double value)
        {
            if (!double.IsFinite(value))
                return 1e-12;

            return Math.Abs(value) < 1e-12 ? 1e-12 : value;
        }
    }
}
