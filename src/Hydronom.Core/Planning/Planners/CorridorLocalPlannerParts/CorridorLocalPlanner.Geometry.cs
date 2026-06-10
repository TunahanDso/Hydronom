using System;
using Hydronom.Core.Domain;
using Hydronom.Core.World.Models;

namespace Hydronom.Core.Planning.Planners
{
    public sealed partial class CorridorLocalPlanner
    {
        private static double DistancePointToSegment2D(Vec3 point, Vec3 a, Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var c1 = vx * wx + vy * wy;
            if (c1 <= 0.0)
                return Distance2D(point, a);

            var c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return Distance2D(point, b);

            var t = c1 / c2;

            var projection = new Vec3(
                a.X + t * vx,
                a.Y + t * vy,
                a.Z);

            return Distance2D(point, projection);
        }

        private static double ProjectionAlongSegment(Vec3 point, Vec3 a, Vec3 b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = point.X - a.X;
            var wy = point.Y - a.Y;

            var c2 = vx * vx + vy * vy;
            if (c2 <= 1e-9)
                return 0.0;

            return (vx * wx + vy * wy) / c2;
        }

        private static Vec3 ToVec3(HydronomWorldObject obj)
        {
            return new Vec3(obj.X, obj.Y, obj.Z);
        }

        private static double Distance2D(Vec3 a, Vec3 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double PathLength2D(System.Collections.Generic.IReadOnlyList<Vec3> points)
        {
            if (points.Count < 2)
                return 0.0;

            var total = 0.0;

            for (var i = 1; i < points.Count; i++)
            {
                total += Distance2D(points[i - 1], points[i]);
            }

            return total;
        }

        private static double HeadingDeg(Vec3 from, Vec3 to)
        {
            return NormalizeDeg(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI);
        }

        private static double DeltaAngleDeg(double from, double to)
        {
            return NormalizeDeg(to - from);
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

        private static string FormatClearance(double clearance)
        {
            return double.IsFinite(clearance) ? $"{clearance:F2}m" : "inf";
        }
    }
}