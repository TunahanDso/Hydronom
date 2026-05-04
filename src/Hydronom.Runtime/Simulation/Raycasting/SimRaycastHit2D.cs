namespace Hydronom.Runtime.Simulation.Raycasting;

/// <summary>
/// 2D raycast sonucu.
/// </summary>
public readonly record struct SimRaycastHit2D(
    bool Hit,
    string ObjectId,
    string Kind,
    double DistanceMeters,
    double HitX,
    double HitY
)
{
    public static SimRaycastHit2D NoHit(double maxDistanceMeters)
    {
        return new SimRaycastHit2D(
            Hit: false,
            ObjectId: "",
            Kind: "none",
            DistanceMeters: SafePositive(maxDistanceMeters, 30.0),
            HitX: 0.0,
            HitY: 0.0
        );
    }

    public SimRaycastHit2D Sanitized()
    {
        return new SimRaycastHit2D(
            Hit: Hit,
            ObjectId: ObjectId?.Trim() ?? "",
            Kind: string.IsNullOrWhiteSpace(Kind) ? "unknown" : Kind.Trim(),
            DistanceMeters: SafeNonNegative(DistanceMeters),
            HitX: Safe(HitX),
            HitY: Safe(HitY)
        );
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

    private static double SafeNonNegative(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0.0;
        }

        return value < 0.0 ? 0.0 : value;
    }
}