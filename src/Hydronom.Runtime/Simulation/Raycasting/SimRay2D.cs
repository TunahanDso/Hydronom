namespace Hydronom.Runtime.Simulation.Raycasting;

/// <summary>
/// 2D ray modeli.
/// Origin dünya koordinatındadır; Direction normalize edilmelidir.
/// </summary>
public readonly record struct SimRay2D(
    double OriginX,
    double OriginY,
    double DirectionX,
    double DirectionY,
    double MaxDistanceMeters
)
{
    public SimRay2D Sanitized()
    {
        var dx = double.IsFinite(DirectionX) ? DirectionX : 1.0;
        var dy = double.IsFinite(DirectionY) ? DirectionY : 0.0;

        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length <= 1e-9)
        {
            dx = 1.0;
            dy = 0.0;
            length = 1.0;
        }

        return new SimRay2D(
            OriginX: Safe(OriginX),
            OriginY: Safe(OriginY),
            DirectionX: dx / length,
            DirectionY: dy / length,
            MaxDistanceMeters: SafePositive(MaxDistanceMeters, 30.0)
        );
    }

    public (double X, double Y) PointAt(double distanceMeters)
    {
        var ray = Sanitized();
        var d = Math.Clamp(distanceMeters, 0.0, ray.MaxDistanceMeters);

        return (
            ray.OriginX + ray.DirectionX * d,
            ray.OriginY + ray.DirectionY * d
        );
    }

    public static SimRay2D FromAngleDeg(
        double originX,
        double originY,
        double angleDeg,
        double maxDistanceMeters)
    {
        var rad = angleDeg * Math.PI / 180.0;

        return new SimRay2D(
            OriginX: originX,
            OriginY: originY,
            DirectionX: Math.Cos(rad),
            DirectionY: Math.Sin(rad),
            MaxDistanceMeters: maxDistanceMeters
        ).Sanitized();
    }

    private static double Safe(double value, double fallback = 0.0)
    {
        return double.IsFinite(value) ? value : fallback;
    }

    private static double SafePositive(double value, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return value <= 0.0 ? fallback : value;
    }
}