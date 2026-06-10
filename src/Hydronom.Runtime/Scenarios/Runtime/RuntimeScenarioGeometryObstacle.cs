using System;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed record RuntimeScenarioGeometryObstacle
{
    public string Id { get; init; } = "unknown";

    public string Kind { get; init; } = "object";

    public string Layer { get; init; } = "scenario";

    public string Name { get; init; } = "unknown";

    public Vec3 Position { get; init; } = Vec3.Zero;

    public double RadiusMeters { get; init; } = 0.50;

    public double WidthMeters { get; init; } = 1.0;

    public double HeightMeters { get; init; } = 1.0;

    public bool IsBlocking { get; init; } = true;

    public bool IsDetectable { get; init; } = true;

    public bool IsNoGoZone { get; init; }

    public bool IsGate { get; init; }

    public bool IsMissionMarker { get; init; }

    public string? Side { get; init; }

    public string? ObjectiveId { get; init; }

    public double DistanceToVehicleMeters { get; init; } = double.PositiveInfinity;

    public double ClearanceToVehicleMeters { get; init; } = double.PositiveInfinity;

    public double BearingFromVehicleDeg { get; init; }

    public double HeadingErrorDeg { get; init; }

    public bool IsAhead { get; init; }

    public bool IsCritical { get; init; }

    public bool IsCollisionCandidate { get; init; }

    public double RiskScore { get; init; }

    public RuntimeScenarioGeometryObstacle Sanitized()
    {
        return this with
        {
            Id = Normalize(Id, "unknown"),
            Kind = Normalize(Kind, "object"),
            Layer = Normalize(Layer, "scenario"),
            Name = Normalize(Name, Id),
            Position = SanitizeVec3(Position),
            RadiusMeters = SanitizePositive(RadiusMeters, 0.50, 0.05, 100.0),
            WidthMeters = SanitizePositive(WidthMeters, Math.Max(RadiusMeters * 2.0, 0.10), 0.05, 250.0),
            HeightMeters = SanitizePositive(HeightMeters, Math.Max(RadiusMeters * 2.0, 0.10), 0.05, 250.0),
            DistanceToVehicleMeters = SanitizeNonNegativeOrInfinity(DistanceToVehicleMeters),
            ClearanceToVehicleMeters = SanitizeClearance(ClearanceToVehicleMeters),
            BearingFromVehicleDeg = NormalizeAngleDeg(BearingFromVehicleDeg),
            HeadingErrorDeg = NormalizeAngleDeg(HeadingErrorDeg),
            RiskScore = Math.Clamp(
                double.IsFinite(RiskScore) ? RiskScore : 0.0,
                0.0,
                1.0)
        };
    }

    public string Compact()
    {
        var safe = Sanitized();

        return
            $"{safe.Id} kind={safe.Kind} " +
            $"dist={safe.DistanceToVehicleMeters:F2} " +
            $"clear={safe.ClearanceToVehicleMeters:F2} " +
            $"headErr={safe.HeadingErrorDeg:F1} " +
            $"ahead={safe.IsAhead} " +
            $"risk={safe.RiskScore:F2}";
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static Vec3 SanitizeVec3(Vec3 value)
    {
        return new Vec3(
            double.IsFinite(value.X) ? value.X : 0.0,
            double.IsFinite(value.Y) ? value.Y : 0.0,
            double.IsFinite(value.Z) ? value.Z : 0.0);
    }

    private static double SanitizePositive(
        double value,
        double fallback,
        double min,
        double max)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            value = fallback;

        if (!double.IsFinite(value) || value <= 0.0)
            value = min;

        return Math.Clamp(value, min, max);
    }

    private static double SanitizeNonNegativeOrInfinity(double value)
    {
        if (double.IsPositiveInfinity(value))
            return value;

        if (!double.IsFinite(value))
            return double.PositiveInfinity;

        return Math.Max(0.0, value);
    }

    private static double SanitizeClearance(double value)
    {
        if (double.IsPositiveInfinity(value))
            return value;

        if (!double.IsFinite(value))
            return double.PositiveInfinity;

        return Math.Clamp(value, -100.0, 10_000.0);
    }

    private static double NormalizeAngleDeg(double degrees)
    {
        if (!double.IsFinite(degrees))
            return 0.0;

        var value = degrees % 360.0;

        if (value > 180.0)
            value -= 360.0;

        if (value < -180.0)
            value += 360.0;

        return value;
    }
}