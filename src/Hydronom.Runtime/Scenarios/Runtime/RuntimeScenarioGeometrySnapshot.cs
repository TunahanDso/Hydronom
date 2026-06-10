using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.Domain;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed record RuntimeScenarioGeometrySnapshot
{
    public static RuntimeScenarioGeometrySnapshot Empty { get; } = new();

    public DateTime TimestampUtc { get; init; } = DateTime.MinValue;

    public long TickIndex { get; init; } = -1;

    public IReadOnlyList<RuntimeScenarioGeometryObstacle> Obstacles { get; init; } =
        Array.Empty<RuntimeScenarioGeometryObstacle>();

    public int ObstacleCount { get; init; }

    public string? NearestObstacleId { get; init; }

    public string? NearestObstacleKind { get; init; }

    public double NearestObstacleX { get; init; }

    public double NearestObstacleY { get; init; }

    public double NearestObstacleZ { get; init; }

    public double NearestDistanceMeters { get; init; } = double.PositiveInfinity;

    public double NearestClearanceMeters { get; init; } = double.PositiveInfinity;

    public string? AheadObstacleId { get; init; }

    public double AheadDistanceMeters { get; init; } = double.PositiveInfinity;

    public double AheadClearanceMeters { get; init; } = double.PositiveInfinity;

    public bool HasObstacleAhead { get; init; }

    public bool HasCriticalObstacleAhead { get; init; }

    public bool CollisionCandidate { get; init; }

    public bool HardBlocked { get; init; }

    public bool SoftBlocked { get; init; }

    public bool RequiresSlowMode { get; init; }

    public double RiskScore { get; init; }

    public bool HasEscapeTarget { get; init; }

    public Vec3 EscapeTarget { get; init; } = Vec3.Zero;

    public bool HasEscapeHeading { get; init; }

    public double EscapeHeadingDeg { get; init; }

    public double SuggestedSpeedMps { get; init; }

    public string Summary { get; init; } = "GEOMETRY_EMPTY";

    public RuntimeScenarioGeometrySnapshot Sanitized()
    {
        var safeObstacles = (Obstacles ?? Array.Empty<RuntimeScenarioGeometryObstacle>())
            .Where(x => x is not null)
            .Select(x => x.Sanitized())
            .OrderBy(x => x.ClearanceToVehicleMeters)
            .ThenBy(x => x.DistanceToVehicleMeters)
            .ToArray();

        var obstacleCount = Math.Max(0, ObstacleCount);
        if (obstacleCount == 0 && safeObstacles.Length > 0)
            obstacleCount = safeObstacles.Length;

        var nearest = safeObstacles.FirstOrDefault();
        var ahead = safeObstacles
            .Where(x => x.IsAhead)
            .OrderBy(x => x.ClearanceToVehicleMeters)
            .ThenBy(x => Math.Abs(x.HeadingErrorDeg))
            .FirstOrDefault();

        var nearestClearance = SanitizeClearance(
            double.IsFinite(NearestClearanceMeters)
                ? NearestClearanceMeters
                : nearest?.ClearanceToVehicleMeters ?? double.PositiveInfinity);

        var nearestDistance = SanitizeDistance(
            double.IsFinite(NearestDistanceMeters)
                ? NearestDistanceMeters
                : nearest?.DistanceToVehicleMeters ?? double.PositiveInfinity);

        var aheadClearance = SanitizeClearance(
            double.IsFinite(AheadClearanceMeters)
                ? AheadClearanceMeters
                : ahead?.ClearanceToVehicleMeters ?? double.PositiveInfinity);

        var aheadDistance = SanitizeDistance(
            double.IsFinite(AheadDistanceMeters)
                ? AheadDistanceMeters
                : ahead?.DistanceToVehicleMeters ?? double.PositiveInfinity);

        var riskScore = Math.Clamp(
            double.IsFinite(RiskScore) ? RiskScore : safeObstacles.Select(x => x.RiskScore).DefaultIfEmpty(0.0).Max(),
            0.0,
            1.0);

        var collisionCandidate =
            CollisionCandidate ||
            safeObstacles.Any(x => x.IsCollisionCandidate) ||
            nearestClearance <= 0.0;

        var hardBlocked =
            HardBlocked ||
            collisionCandidate ||
            riskScore >= 0.92 ||
            nearestClearance <= 0.25;

        var softBlocked =
            SoftBlocked ||
            hardBlocked ||
            riskScore >= 0.65 ||
            aheadClearance <= 1.25;

        var requiresSlowMode =
            RequiresSlowMode ||
            softBlocked ||
            riskScore >= 0.45 ||
            aheadClearance <= 2.25;

        var summary = string.IsNullOrWhiteSpace(Summary)
            ? BuildSummary(
                obstacleCount,
                nearest?.Id,
                nearestClearance,
                riskScore,
                hardBlocked,
                softBlocked,
                collisionCandidate)
            : Summary.Trim();

        return this with
        {
            TimestampUtc = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc,
            TickIndex = TickIndex,
            Obstacles = safeObstacles,
            ObstacleCount = obstacleCount,

            NearestObstacleId = NormalizeOptional(NearestObstacleId) ?? nearest?.Id,
            NearestObstacleKind = NormalizeOptional(NearestObstacleKind) ?? nearest?.Kind,
            NearestObstacleX = double.IsFinite(NearestObstacleX) ? NearestObstacleX : nearest?.Position.X ?? 0.0,
            NearestObstacleY = double.IsFinite(NearestObstacleY) ? NearestObstacleY : nearest?.Position.Y ?? 0.0,
            NearestObstacleZ = double.IsFinite(NearestObstacleZ) ? NearestObstacleZ : nearest?.Position.Z ?? 0.0,
            NearestDistanceMeters = nearestDistance,
            NearestClearanceMeters = nearestClearance,

            AheadObstacleId = NormalizeOptional(AheadObstacleId) ?? ahead?.Id,
            AheadDistanceMeters = aheadDistance,
            AheadClearanceMeters = aheadClearance,

            HasObstacleAhead = HasObstacleAhead || ahead is not null,
            HasCriticalObstacleAhead = HasCriticalObstacleAhead || (ahead?.IsCritical ?? false),

            CollisionCandidate = collisionCandidate,
            HardBlocked = hardBlocked,
            SoftBlocked = softBlocked,
            RequiresSlowMode = requiresSlowMode,

            RiskScore = riskScore,

            HasEscapeTarget = HasEscapeTarget && IsFiniteVec3(EscapeTarget),
            EscapeTarget = IsFiniteVec3(EscapeTarget) ? EscapeTarget : Vec3.Zero,

            HasEscapeHeading = HasEscapeHeading && double.IsFinite(EscapeHeadingDeg),
            EscapeHeadingDeg = NormalizeAngleDeg(EscapeHeadingDeg),

            SuggestedSpeedMps = Math.Clamp(
                double.IsFinite(SuggestedSpeedMps) ? SuggestedSpeedMps : 0.0,
                0.0,
                2.0),

            Summary = summary
        };
    }

    public string Compact()
    {
        var safe = Sanitized();

        return
            $"{safe.Summary} " +
            $"obs={safe.ObstacleCount} " +
            $"nearest={safe.NearestObstacleId ?? "none"} " +
            $"clear={safe.NearestClearanceMeters:F2} " +
            $"ahead={safe.AheadObstacleId ?? "none"} " +
            $"aheadClear={safe.AheadClearanceMeters:F2} " +
            $"risk={safe.RiskScore:F2} " +
            $"hard={safe.HardBlocked} " +
            $"soft={safe.SoftBlocked} " +
            $"collision={safe.CollisionCandidate}";
    }

    private static string BuildSummary(
        int obstacleCount,
        string? nearestId,
        double nearestClearance,
        double riskScore,
        bool hardBlocked,
        bool softBlocked,
        bool collisionCandidate)
    {
        if (obstacleCount <= 0)
            return "GEOMETRY_EMPTY";

        if (collisionCandidate)
            return $"GEOMETRY_COLLISION_CANDIDATE nearest={nearestId ?? "none"} clear={nearestClearance:F2} risk={riskScore:F2}";

        if (hardBlocked)
            return $"GEOMETRY_HARD_COLLISION_GUARD nearest={nearestId ?? "none"} clear={nearestClearance:F2} risk={riskScore:F2}";

        if (softBlocked)
            return $"GEOMETRY_SOFT_COLLISION_GUARD nearest={nearestId ?? "none"} clear={nearestClearance:F2} risk={riskScore:F2}";

        return $"SCENARIO_GEOMETRY_AUTHORITY nearest={nearestId ?? "none"} clear={nearestClearance:F2} risk={riskScore:F2}";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static double SanitizeDistance(double value)
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

    private static bool IsFiniteVec3(Vec3 value)
    {
        return
            double.IsFinite(value.X) &&
            double.IsFinite(value.Y) &&
            double.IsFinite(value.Z);
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