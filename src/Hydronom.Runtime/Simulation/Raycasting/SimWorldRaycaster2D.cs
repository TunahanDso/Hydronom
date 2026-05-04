using Hydronom.Core.World.Models;
using Hydronom.Runtime.World.Runtime;

namespace Hydronom.Runtime.Simulation.Raycasting;

/// <summary>
/// RuntimeWorldModel üzerinde basit 2D raycast yapar.
/// Şimdilik dairesel ve dikdörtgen yaklaşık bounds desteklenir.
/// Bölüm 3 için hedef: LiDAR'ın world içindeki obstacle/no-go/buoy objelerini görmesi.
/// </summary>
public sealed class SimWorldRaycaster2D
{
    private readonly RuntimeWorldModel _worldModel;

    public SimWorldRaycaster2D(RuntimeWorldModel worldModel)
    {
        _worldModel = worldModel ?? throw new ArgumentNullException(nameof(worldModel));
    }

    public SimRaycastHit2D Cast(SimRay2D ray)
    {
        var safeRay = ray.Sanitized();

        var candidates = _worldModel.ActiveObjects()
            .Where(x => x.IsObstacleLike || x.IsBlocking)
            .ToArray();

        SimRaycastHit2D? nearest = null;

        foreach (var candidate in candidates)
        {
            var hit = IntersectObject(safeRay, candidate);

            if (!hit.Hit)
            {
                continue;
            }

            if (hit.DistanceMeters < 0.0 || hit.DistanceMeters > safeRay.MaxDistanceMeters)
            {
                continue;
            }

            if (nearest is null || hit.DistanceMeters < nearest.Value.DistanceMeters)
            {
                nearest = hit;
            }
        }

        return nearest?.Sanitized() ?? SimRaycastHit2D.NoHit(safeRay.MaxDistanceMeters);
    }

    private static SimRaycastHit2D IntersectObject(SimRay2D ray, HydronomWorldObject obj)
    {
        if (obj.Radius > 0.0)
        {
            return IntersectCircle(ray, obj);
        }

        return IntersectApproxBox(ray, obj);
    }

    private static SimRaycastHit2D IntersectCircle(SimRay2D ray, HydronomWorldObject obj)
    {
        var radius = obj.Radius <= 0.0 ? 0.5 : obj.Radius;

        var ox = ray.OriginX;
        var oy = ray.OriginY;
        var dx = ray.DirectionX;
        var dy = ray.DirectionY;

        var cx = obj.X;
        var cy = obj.Y;

        var fx = ox - cx;
        var fy = oy - cy;

        var a = dx * dx + dy * dy;
        var b = 2.0 * (fx * dx + fy * dy);
        var c = fx * fx + fy * fy - radius * radius;

        var discriminant = b * b - 4.0 * a * c;

        if (discriminant < 0.0)
        {
            return SimRaycastHit2D.NoHit(ray.MaxDistanceMeters);
        }

        var sqrt = Math.Sqrt(discriminant);

        var t1 = (-b - sqrt) / (2.0 * a);
        var t2 = (-b + sqrt) / (2.0 * a);

        var t = ChooseNearestPositive(t1, t2);

        if (t is null)
        {
            return SimRaycastHit2D.NoHit(ray.MaxDistanceMeters);
        }

        var p = ray.PointAt(t.Value);

        return new SimRaycastHit2D(
            Hit: true,
            ObjectId: obj.Id,
            Kind: obj.Kind,
            DistanceMeters: t.Value,
            HitX: p.X,
            HitY: p.Y
        );
    }

    private static SimRaycastHit2D IntersectApproxBox(SimRay2D ray, HydronomWorldObject obj)
    {
        var width = obj.Width > 0.0 ? obj.Width : Math.Max(0.5, obj.Radius * 2.0);
        var height = obj.Height > 0.0 ? obj.Height : Math.Max(0.5, obj.Radius * 2.0);

        var minX = obj.X - width / 2.0;
        var maxX = obj.X + width / 2.0;
        var minY = obj.Y - height / 2.0;
        var maxY = obj.Y + height / 2.0;

        var tMin = 0.0;
        var tMax = ray.MaxDistanceMeters;

        if (!ClipAxis(ray.OriginX, ray.DirectionX, minX, maxX, ref tMin, ref tMax))
        {
            return SimRaycastHit2D.NoHit(ray.MaxDistanceMeters);
        }

        if (!ClipAxis(ray.OriginY, ray.DirectionY, minY, maxY, ref tMin, ref tMax))
        {
            return SimRaycastHit2D.NoHit(ray.MaxDistanceMeters);
        }

        if (tMin < 0.0 || tMin > ray.MaxDistanceMeters)
        {
            return SimRaycastHit2D.NoHit(ray.MaxDistanceMeters);
        }

        var p = ray.PointAt(tMin);

        return new SimRaycastHit2D(
            Hit: true,
            ObjectId: obj.Id,
            Kind: obj.Kind,
            DistanceMeters: tMin,
            HitX: p.X,
            HitY: p.Y
        );
    }

    private static bool ClipAxis(
        double origin,
        double direction,
        double min,
        double max,
        ref double tMin,
        ref double tMax)
    {
        if (Math.Abs(direction) < 1e-9)
        {
            return origin >= min && origin <= max;
        }

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);

        return tMin <= tMax;
    }

    private static double? ChooseNearestPositive(double a, double b)
    {
        var candidates = new[] { a, b }
            .Where(x => x >= 0.0 && double.IsFinite(x))
            .OrderBy(x => x)
            .ToArray();

        return candidates.Length == 0 ? null : candidates[0];
    }
}