using System;
using System.Collections.Generic;
using System.Linq;
using Hydronom.Core.World.Models;
using Hydronom.Core.Domain;

using Hydronom.Runtime.World.Runtime;
using Microsoft.Extensions.Configuration;

namespace Hydronom.Runtime.Scenarios.Runtime;

public sealed class RuntimeScenarioGeometryAuthority
{
    private readonly IConfiguration _config;
    private readonly RuntimeWorldModel _worldModel;

    private readonly bool _enabled;
    private readonly double _vehicleHullRadiusM;
    private readonly double _objectSafetyMarginM;
    private readonly double _maxConsiderDistanceM;
    private readonly double _aheadDistanceM;
    private readonly double _aheadHalfFovDeg;
    private readonly double _criticalClearanceM;
    private readonly double _hardClearanceM;
    private readonly double _softClearanceM;
    private readonly double _slowClearanceM;
    private readonly double _minObjectRadiusM;
    private readonly double _escapeDistanceM;
    private readonly double _suggestedSpeedMps;
    private readonly int _logEveryTicks;

    private long _lastLoggedTick = -1;

    public RuntimeScenarioGeometryAuthority(
        IConfiguration config,
        RuntimeWorldModel worldModel)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _worldModel = worldModel ?? throw new ArgumentNullException(nameof(worldModel));

        _enabled = ReadBool("Runtime:GeometryAuthority:Enabled", true);

        _vehicleHullRadiusM = ReadDouble("Runtime:GeometryAuthority:VehicleHullRadiusM", 0.55);
        _objectSafetyMarginM = ReadDouble("Runtime:GeometryAuthority:ObjectSafetyMarginM", 0.30);
        _maxConsiderDistanceM = ReadDouble("Runtime:GeometryAuthority:MaxConsiderDistanceM", 60.0);

        _aheadDistanceM = ReadDouble("Runtime:GeometryAuthority:AheadDistanceM", 9.0);
        _aheadHalfFovDeg = ReadDouble("Runtime:GeometryAuthority:AheadHalfFovDeg", 45.0);

        _criticalClearanceM = ReadDouble("Runtime:GeometryAuthority:CriticalClearanceM", 0.02);
        _hardClearanceM = ReadDouble("Runtime:GeometryAuthority:HardClearanceM", 0.05);
        _softClearanceM = ReadDouble("Runtime:GeometryAuthority:SoftClearanceM", 0.55);
        _slowClearanceM = ReadDouble("Runtime:GeometryAuthority:SlowClearanceM", 1.20);

        _minObjectRadiusM = ReadDouble("Runtime:GeometryAuthority:MinObjectRadiusM", 0.25);
        _escapeDistanceM = ReadDouble("Runtime:GeometryAuthority:EscapeDistanceM", 2.75);
        _suggestedSpeedMps = ReadDouble("Runtime:GeometryAuthority:SuggestedSpeedMps", 0.22);
        _logEveryTicks = Math.Max(1, ReadInt("Runtime:GeometryAuthority:LogEveryTicks", 10));

        Console.WriteLine(
            "[CFG] ScenarioGeometryAuthority → " +
            $"enabled={_enabled} " +
            $"hull={_vehicleHullRadiusM:F2}m " +
            $"margin={_objectSafetyMarginM:F2}m " +
            $"ahead={_aheadDistanceM:F1}m/{_aheadHalfFovDeg:F0}° " +
            $"critical={_criticalClearanceM:F2}m " +
            $"hard={_hardClearanceM:F2}m " +
            $"soft={_softClearanceM:F2}m " +
            $"slow={_slowClearanceM:F2}m");
    }

    public RuntimeScenarioGeometrySnapshot Update(
        VehicleState state,
        Vec3 referenceTarget,
        long tickIndex,
        DateTime timestampUtc)
    {
        if (!_enabled)
        {
            return RuntimeScenarioGeometrySnapshot.Empty with
            {
                TimestampUtc = timestampUtc == default ? DateTime.UtcNow : timestampUtc,
                TickIndex = tickIndex,
                Summary = "GEOMETRY_DISABLED"
            };
        }

        var now = timestampUtc == default
            ? DateTime.UtcNow
            : timestampUtc;

        var activeObjects = _worldModel.ActiveObjects();

        if (activeObjects.Count == 0)
        {
            var empty = RuntimeScenarioGeometrySnapshot.Empty with
            {
                TimestampUtc = now,
                TickIndex = tickIndex,
                Summary = "GEOMETRY_EMPTY"
            };

            MaybeLog(empty, tickIndex, state, referenceTarget);
            return empty;
        }

        var obstacles = new List<RuntimeScenarioGeometryObstacle>();

        foreach (var obj in activeObjects)
        {
            if (!ShouldUseAsGeometryObstacle(obj.Kind, obj.Layer, obj.IsActive, obj.IsBlocking, obj.IsObstacleLike, obj.Tags))
                continue;

            var obstacle = BuildObstacle(obj, state);

            if (obstacle.DistanceToVehicleMeters > _maxConsiderDistanceM)
                continue;

            obstacles.Add(obstacle.Sanitized());
        }

        if (obstacles.Count == 0)
        {
            var empty = RuntimeScenarioGeometrySnapshot.Empty with
            {
                TimestampUtc = now,
                TickIndex = tickIndex,
                Summary = "GEOMETRY_EMPTY"
            };

            MaybeLog(empty, tickIndex, state, referenceTarget);
            return empty;
        }

        obstacles = obstacles
            .OrderBy(x => x.ClearanceToVehicleMeters)
            .ThenBy(x => x.DistanceToVehicleMeters)
            .ToList();

        var nearest = obstacles[0];

        var ahead = obstacles
            .Where(x => x.IsAhead)
            .OrderBy(x => x.ClearanceToVehicleMeters)
            .ThenBy(x => Math.Abs(x.HeadingErrorDeg))
            .FirstOrDefault();

        var riskScore = obstacles
            .Select(x => x.RiskScore)
            .DefaultIfEmpty(0.0)
            .Max();

        var collisionCandidate =
            nearest.ClearanceToVehicleMeters <= _criticalClearanceM ||
            obstacles.Any(x => x.IsCollisionCandidate);

        var hardBlocked =
            collisionCandidate ||
            nearest.ClearanceToVehicleMeters <= _hardClearanceM ||
            riskScore >= 0.92;

        var softBlocked =
            hardBlocked ||
            nearest.ClearanceToVehicleMeters <= _softClearanceM ||
            (ahead is not null && ahead.ClearanceToVehicleMeters <= _softClearanceM) ||
            riskScore >= 0.65;

        var requiresSlowMode =
            softBlocked ||
            nearest.ClearanceToVehicleMeters <= _slowClearanceM ||
            (ahead is not null && ahead.ClearanceToVehicleMeters <= _slowClearanceM) ||
            riskScore >= 0.45;

        var escapeTarget = BuildEscapeTarget(state, nearest, referenceTarget);
        var escapeHeadingDeg = BearingDeg(state.Position, escapeTarget);

        var summary = BuildSummary(
            obstacles.Count,
            nearest.Id,
            nearest.ClearanceToVehicleMeters,
            riskScore,
            hardBlocked,
            softBlocked,
            collisionCandidate);

        var snapshot = new RuntimeScenarioGeometrySnapshot
        {
            TimestampUtc = now,
            TickIndex = tickIndex,

            Obstacles = obstacles,
            ObstacleCount = obstacles.Count,

            NearestObstacleId = nearest.Id,
            NearestObstacleKind = nearest.Kind,
            NearestObstacleX = nearest.Position.X,
            NearestObstacleY = nearest.Position.Y,
            NearestObstacleZ = nearest.Position.Z,
            NearestDistanceMeters = nearest.DistanceToVehicleMeters,
            NearestClearanceMeters = nearest.ClearanceToVehicleMeters,

            AheadObstacleId = ahead?.Id,
            AheadDistanceMeters = ahead?.DistanceToVehicleMeters ?? double.PositiveInfinity,
            AheadClearanceMeters = ahead?.ClearanceToVehicleMeters ?? double.PositiveInfinity,

            HasObstacleAhead = ahead is not null,
            HasCriticalObstacleAhead = ahead?.IsCritical ?? false,

            CollisionCandidate = collisionCandidate,
            HardBlocked = hardBlocked,
            SoftBlocked = softBlocked,
            RequiresSlowMode = requiresSlowMode,
            RiskScore = riskScore,

            HasEscapeTarget = true,
            EscapeTarget = escapeTarget,
            HasEscapeHeading = true,
            EscapeHeadingDeg = escapeHeadingDeg,
            SuggestedSpeedMps = _suggestedSpeedMps,

            Summary = summary
        }.Sanitized();

        MaybeLog(snapshot, tickIndex, state, referenceTarget);

        return snapshot;
    }

    private RuntimeScenarioGeometryObstacle BuildObstacle(
        HydronomWorldObject obj,
        VehicleState state)
    {
        var position = new Vec3(
            SanitizeFinite(obj.X),
            SanitizeFinite(obj.Y),
            SanitizeFinite(obj.Z));

        var dx = position.X - state.Position.X;
        var dy = position.Y - state.Position.Y;

        var distance = Math.Sqrt(dx * dx + dy * dy);

        var radius = ResolveObjectRadius(
            SanitizeFinite(obj.Radius),
            SanitizeFinite(obj.Width),
            SanitizeFinite(obj.Height));

        var clearance = distance - radius - _vehicleHullRadiusM - _objectSafetyMarginM;

        var bearingDeg = RadToDeg(Math.Atan2(dy, dx));
        var headingErrorDeg = NormalizeAngleDeg(bearingDeg - state.Orientation.YawDeg);

        var isAhead =
            distance <= _aheadDistanceM &&
            Math.Abs(headingErrorDeg) <= _aheadHalfFovDeg;

        var risk = ComputeRiskScore(
            distance,
            clearance,
            isAhead,
            obj.IsBlocking,
            obj.IsObstacleLike,
            TryGetTag(obj.Tags, "isNoGoZone") == "true");

        var isCollisionCandidate = clearance <= _criticalClearanceM;
        var isCritical = isCollisionCandidate || clearance <= _hardClearanceM;

        return new RuntimeScenarioGeometryObstacle
        {
            Id = NormalizeText(obj.Id, "unknown"),
            Kind = NormalizeText(obj.Kind, "object"),
            Layer = NormalizeText(obj.Layer, "scenario"),
            Name = NormalizeText(obj.Name, NormalizeText(obj.Id, "unknown")),

            Position = position,

            RadiusMeters = radius,
            WidthMeters = SanitizePositive(SanitizeFinite(obj.Width), radius * 2.0),
            HeightMeters = SanitizePositive(SanitizeFinite(obj.Height), radius * 2.0),

            IsBlocking = obj.IsBlocking,
            IsDetectable = TryGetTag(obj.Tags, "isDetectable") != "false",
            IsNoGoZone = TryGetTag(obj.Tags, "isNoGoZone") == "true",
            IsGate = TryGetTag(obj.Tags, "isGate") == "true",
            IsMissionMarker = TryGetTag(obj.Tags, "missionMarker") == "true",

            Side = NormalizeOptional(TryGetTag(obj.Tags, "side")),
            ObjectiveId = NormalizeOptional(TryGetTag(obj.Tags, "objectiveId")),

            DistanceToVehicleMeters = distance,
            ClearanceToVehicleMeters = clearance,
            BearingFromVehicleDeg = bearingDeg,
            HeadingErrorDeg = headingErrorDeg,

            IsAhead = isAhead,
            IsCritical = isCritical,
            IsCollisionCandidate = isCollisionCandidate,

            RiskScore = risk
        };
    }

    private bool ShouldUseAsGeometryObstacle(
        string? kind,
        string? layer,
        bool isActive,
        bool isBlocking,
        bool isObstacleLike,
        IReadOnlyDictionary<string, string> tags)
    {
        if (!isActive)
            return false;

        // Boundary objects are semantic track limits/visual safety rails by default.
        // They must NOT become giant circular collision obstacles just because they have a large width.
        // Only allow them into geometry authority when a scenario explicitly opts in.
        var isBoundary =
            TextEquals(kind, "boundary") ||
            TextEquals(layer, "scenario_boundary") ||
            TextEquals(TryGetTag(tags, "visual.kind"), "boundary") ||
            TextEquals(TryGetTag(tags, "geometry.kind"), "boundary");

        if (isBoundary)
        {
            return
                TextEquals(TryGetTag(tags, "geometryObstacle"), "true") ||
                TextEquals(TryGetTag(tags, "collisionBoundary"), "true") ||
                TextEquals(TryGetTag(tags, "treatAsObstacle"), "true");
        }

        if (TryGetTag(tags, "isCompleted") == "true" &&
            TryGetTag(tags, "missionMarker") == "true")
            return false;

        if (TryGetTag(tags, "isTargetZone") == "true" &&
            !isBlocking)
            return false;

        if (isBlocking || isObstacleLike)
            return true;

        if (TextEquals(kind, "obstacle") ||
            TextEquals(kind, "buoy") ||
            TextEquals(kind, "gate_left") ||
            TextEquals(kind, "gate_right") ||
            TextEquals(kind, "no_go_zone"))
            return true;

        if (TextEquals(layer, "scenario_obstacles") ||
            TextEquals(layer, "scenario_navigation") ||
            TextEquals(layer, "scenario_safety"))
            return true;

        if (TryGetTag(tags, "isNoGoZone") == "true" ||
            TryGetTag(tags, "isGate") == "true" ||
            TryGetTag(tags, "isBlocking") == "true")
            return true;

        return false;
    }

    private double ResolveObjectRadius(
        double radius,
        double width,
        double height)
    {
        var sizeRadius = Math.Max(
            Math.Max(radius, width > 0.0 ? width * 0.5 : 0.0),
            height > 0.0 ? height * 0.5 : 0.0);

        if (!double.IsFinite(sizeRadius) || sizeRadius <= 0.0)
            sizeRadius = _minObjectRadiusM;

        return Math.Max(sizeRadius, _minObjectRadiusM);
    }

    private double ComputeRiskScore(
        double distance,
        double clearance,
        bool isAhead,
        bool isBlocking,
        bool isObstacleLike,
        bool isNoGoZone)
    {
        if (!double.IsFinite(distance))
            return 0.0;

        if (clearance <= _criticalClearanceM)
            return 1.0;

        if (clearance <= _hardClearanceM)
            return 0.95;

        if (clearance <= _softClearanceM)
            return isAhead ? 0.88 : 0.72;

        if (clearance <= _slowClearanceM)
            return isAhead ? 0.62 : 0.42;

        var distanceRisk = 1.0 - Math.Clamp(clearance / Math.Max(_aheadDistanceM, 1.0), 0.0, 1.0);

        var multiplier = 0.35;

        if (isAhead)
            multiplier += 0.20;

        if (isBlocking || isObstacleLike)
            multiplier += 0.10;

        if (isNoGoZone)
            multiplier += 0.20;

        return Math.Clamp(distanceRisk * multiplier, 0.0, 0.65);
    }

    private Vec3 BuildEscapeTarget(
        VehicleState state,
        RuntimeScenarioGeometryObstacle nearest,
        Vec3 referenceTarget)
    {
        var awayX = state.Position.X - nearest.Position.X;
        var awayY = state.Position.Y - nearest.Position.Y;

        var awayLen = Math.Sqrt(awayX * awayX + awayY * awayY);
        if (awayLen < 1e-6)
        {
            var yawRad = DegToRad(state.Orientation.YawDeg);
            awayX = -Math.Cos(yawRad);
            awayY = -Math.Sin(yawRad);
            awayLen = 1.0;
        }

        awayX /= awayLen;
        awayY /= awayLen;

        var targetX = referenceTarget.X - state.Position.X;
        var targetY = referenceTarget.Y - state.Position.Y;
        var targetLen = Math.Sqrt(targetX * targetX + targetY * targetY);

        if (targetLen > 1e-6)
        {
            targetX /= targetLen;
            targetY /= targetLen;
        }
        else
        {
            var yawRad = DegToRad(state.Orientation.YawDeg);
            targetX = Math.Cos(yawRad);
            targetY = Math.Sin(yawRad);
        }

        var tangentX = -awayY;
        var tangentY = awayX;

        var leftX = targetX * 0.65 + tangentX * 0.55 + awayX * 0.45;
        var leftY = targetY * 0.65 + tangentY * 0.55 + awayY * 0.45;

        var rightX = targetX * 0.65 - tangentX * 0.55 + awayX * 0.45;
        var rightY = targetY * 0.65 - tangentY * 0.55 + awayY * 0.45;

        var leftAwayDot = leftX * awayX + leftY * awayY;
        var rightAwayDot = rightX * awayX + rightY * awayY;

        var escapeX = leftAwayDot >= rightAwayDot ? leftX : rightX;
        var escapeY = leftAwayDot >= rightAwayDot ? leftY : rightY;

        var escapeLen = Math.Sqrt(escapeX * escapeX + escapeY * escapeY);
        if (escapeLen < 1e-6)
        {
            escapeX = awayX;
            escapeY = awayY;
            escapeLen = 1.0;
        }

        escapeX /= escapeLen;
        escapeY /= escapeLen;

        return new Vec3(
            state.Position.X + escapeX * _escapeDistanceM,
            state.Position.Y + escapeY * _escapeDistanceM,
            state.Position.Z);
    }

    private void MaybeLog(
        RuntimeScenarioGeometrySnapshot snapshot,
        long tickIndex,
        VehicleState state,
        Vec3 referenceTarget)
    {
        if (tickIndex < 0)
            return;

        if (_lastLoggedTick >= 0 &&
            tickIndex - _lastLoggedTick < _logEveryTicks &&
            !snapshot.HardBlocked &&
            !snapshot.CollisionCandidate)
        {
            return;
        }

        _lastLoggedTick = tickIndex;

        Console.WriteLine(
            "[GEOM-AUTH] " +
            $"tick={tickIndex} " +
            $"obs={snapshot.ObstacleCount} " +
            $"nearest={snapshot.NearestObstacleId ?? "none"} " +
            $"clear={snapshot.NearestClearanceMeters:F2} " +
            $"ahead={snapshot.AheadObstacleId ?? "none"} " +
            $"aheadClear={snapshot.AheadClearanceMeters:F2} " +
            $"risk={snapshot.RiskScore:F2} " +
            $"hard={snapshot.HardBlocked} " +
            $"soft={snapshot.SoftBlocked} " +
            $"collision={snapshot.CollisionCandidate} " +
            $"pos=({state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}) " +
            $"ref=({referenceTarget.X:F2},{referenceTarget.Y:F2},{referenceTarget.Z:F2}) " +
            $"summary={snapshot.Summary}"
        );
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

    private bool ReadBool(string key, bool fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return bool.TryParse(raw, out var value)
            ? value
            : fallback;
    }

    private int ReadInt(string key, int fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return int.TryParse(
            raw,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private double ReadDouble(string key, double fallback)
    {
        var raw = _config[key];

        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        return double.TryParse(
            raw,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private static string? TryGetTag(
        IReadOnlyDictionary<string, string>? tags,
        string key)
    {
        if (tags is null || string.IsNullOrWhiteSpace(key))
            return null;

        return tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool TextEquals(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static double SanitizeFinite(double value)
    {
        return double.IsFinite(value)
            ? value
            : 0.0;
    }

    private static double SanitizePositive(double value, double fallback)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            value = fallback;

        if (!double.IsFinite(value) || value <= 0.0)
            value = 0.1;

        return value;
    }

    private static double BearingDeg(Vec3 from, Vec3 to)
    {
        return NormalizeAngleDeg(
            RadToDeg(Math.Atan2(
                to.Y - from.Y,
                to.X - from.X)));
    }

    private static double DegToRad(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double RadToDeg(double radians)
    {
        return radians * 180.0 / Math.PI;
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

