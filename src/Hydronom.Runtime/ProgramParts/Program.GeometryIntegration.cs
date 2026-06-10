using System;
using Hydronom.Core.Control;
using Hydronom.Core.Domain;
using Hydronom.Core.Modules;
using Hydronom.Core.Modules.Control;
using Hydronom.Runtime.Planning;
using Hydronom.Runtime.Scenarios.Runtime;

partial class Program
{
    private static DecisionAdviceProfile BuildWorldAwareDecisionAdvice(
        DecisionAdviceProfile analysisAdvice,
        RuntimePlanningSnapshot? planningSnapshot,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot)
    {
        var planningAwareAdvice = BuildWorldAwareDecisionAdvice(
            analysisAdvice,
            planningSnapshot);

        var geometryAdvice = BuildGeometryDecisionAdvice(geometrySnapshot);

        return planningAwareAdvice
            .MergeConservative(geometryAdvice)
            .Sanitized();
    }

    private static DecisionAdviceProfile BuildGeometryDecisionAdvice(
        RuntimeScenarioGeometrySnapshot? geometrySnapshot)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        if (geometry.ObstacleCount <= 0)
            return DecisionAdviceProfile.Neutral;

        /*
         * HARD GUARD:
         * Sadece gerçek çarpışma adayı / hard block varsa aracı tut.
         * Bu kural parkur girişindeki iki sarı duba gibi geçilebilir kapıları durdurmamalı.
         */
        if (geometry.CollisionCandidate || geometry.HardBlocked)
        {
            return (DecisionAdviceProfile.Neutral with
            {
                MaxSpeedScale = 0.0,
                ThrottleScale = 0.0,
                YawAggressionScale = 0.55,
                ArrivalCautionScale = 2.25,
                ObstacleAvoidanceUrgency = 1.0,
                RequireSlowMode = true,
                PreferSafeHeading = true,
                RecommendHold = true,
                ForceCoast = true,
                PrimaryReason = geometry.CollisionCandidate
                    ? "GEOMETRY_COLLISION_CANDIDATE"
                    : "GEOMETRY_HARD_COLLISION_GUARD",
                HasPassableCorridor = false,
                CorridorCenterOffsetDeg = 0.0,
                CorridorWidthMeters = 0.0,
                CorridorClearanceMeters = Math.Max(0.0, geometry.NearestClearanceMeters),
                CorridorConfidence = 0.0,
                SuppressObstaclePanic = false,
                PreferCorridorHeading = false
            }).Sanitized();
        }

        /*
         * SOFT GUARD:
         * Burada kritik düzeltme var:
         *
         * Eski davranış:
         *   geometry.SoftBlocked || geometry.RequiresSlowMode
         *
         * Bu, parkur girişinde "soft=False ama RequiresSlowMode=True" durumunu
         * GEOMETRY_SOFT_COLLISION_GUARD'a çeviriyordu.
         *
         * Yeni davranış:
         *   Sadece geometry.SoftBlocked gerçekse soft collision guard çalışır.
         *
         * Risk 0.35-0.55 aralığı ve clearance geçilebilir ise araç korkup durmaz;
         * sadece dikkatli corridor takip eder.
         */
        if (geometry.SoftBlocked)
        {
            return (DecisionAdviceProfile.Neutral with
            {
                MaxSpeedScale = geometry.RiskScore >= 0.75 ? 0.30 : 0.55,
                ThrottleScale = geometry.RiskScore >= 0.75 ? 0.30 : 0.55,
                YawAggressionScale = 0.75,
                ArrivalCautionScale = 1.65,
                ObstacleAvoidanceUrgency = Math.Clamp(geometry.RiskScore, 0.55, 0.90),
                RequireSlowMode = true,
                PreferSafeHeading = true,
                RecommendHold = geometry.RiskScore >= 0.92,
                ForceCoast = false,
                PrimaryReason = "GEOMETRY_SOFT_COLLISION_GUARD",
                HasPassableCorridor = false,
                CorridorCenterOffsetDeg = 0.0,
                CorridorWidthMeters = 0.0,
                CorridorClearanceMeters = Math.Max(0.0, geometry.NearestClearanceMeters),
                CorridorConfidence = Math.Clamp(1.0 - geometry.RiskScore, 0.0, 1.0),
                SuppressObstaclePanic = false,
                PreferCorridorHeading = false
            }).Sanitized();
        }

        var clearance = Math.Max(0.0, geometry.NearestClearanceMeters);

        var passableCorridor =
            clearance >= 1.0 &&
            geometry.RiskScore <= 0.60 &&
            !geometry.CollisionCandidate &&
            !geometry.HardBlocked &&
            !geometry.SoftBlocked;

        var speedScale =
            geometry.RiskScore <= 0.25 ? 1.00 :
            geometry.RiskScore <= 0.45 ? 0.95 :
            geometry.RiskScore <= 0.60 ? 0.82 :
            0.70;

        var throttleScale =
            geometry.RiskScore <= 0.25 ? 1.00 :
            geometry.RiskScore <= 0.45 ? 0.95 :
            geometry.RiskScore <= 0.60 ? 0.85 :
            0.72;

        return (DecisionAdviceProfile.Neutral with
        {
            MaxSpeedScale = speedScale,
            ThrottleScale = throttleScale,
            YawAggressionScale = passableCorridor ? 0.95 : 0.85,
            ArrivalCautionScale =
                geometry.RiskScore <= 0.25 ? 1.00 :
                geometry.RiskScore <= 0.45 ? 1.08 :
                geometry.RiskScore <= 0.60 ? 1.18 :
                1.35,

            ObstacleAvoidanceUrgency = passableCorridor
                ? Math.Clamp(geometry.RiskScore * 0.55, 0.0, 0.32)
                : Math.Clamp(geometry.RiskScore, 0.0, 0.55),

            RequireSlowMode = geometry.RiskScore >= 0.65,
            PreferSafeHeading = geometry.RiskScore >= 0.35,

            PrimaryReason = passableCorridor
                ? "GEOMETRY_PASSABLE_CORRIDOR"
                : "SCENARIO_GEOMETRY_AUTHORITY",

            HasPassableCorridor = passableCorridor,
            CorridorCenterOffsetDeg = 0.0,
            CorridorWidthMeters = clearance,
            CorridorClearanceMeters = clearance,
            CorridorConfidence = passableCorridor
                ? Math.Clamp(1.0 - geometry.RiskScore * 0.75, 0.35, 1.0)
                : Math.Clamp(1.0 - geometry.RiskScore, 0.0, 1.0),

            SuppressObstaclePanic = passableCorridor,
            PreferCorridorHeading = false
        }).Sanitized();
    }

    private static double ComputeWorldAwareRiskScore(
        RuntimePlanningSnapshot? planningSnapshot,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot)
    {
        var planningRisk = ComputePlanningRiskScore(planningSnapshot);

        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        return Math.Clamp(
            Math.Max(planningRisk, geometry.RiskScore),
            0.0,
            1.0);
    }

    private static bool IsWorldAwareHardBlocked(
        RuntimePlanningSnapshot? planningSnapshot,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot,
        double finalRiskScore)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        if (geometry.CollisionCandidate || geometry.HardBlocked)
            return true;

        if (finalRiskScore >= 0.95)
            return true;

        return IsPlanningHardBlocked(
            planningSnapshot,
            ComputePlanningRiskScore(planningSnapshot));
    }

    private static bool IsWorldAwareSoftBlocked(
        RuntimePlanningSnapshot? planningSnapshot,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot,
        double finalRiskScore)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        /*
         * Kritik düzeltme:
         * geometry.RequiresSlowMode artık world-aware soft block sayılmıyor.
         * Çünkü giriş kapısı gibi geçilebilir corridorlarda sadece "dikkatli geç"
         * anlamına gelebilir; collision guard değildir.
         */
        if (geometry.SoftBlocked)
            return true;

        if (finalRiskScore >= 0.70)
            return true;

        return IsPlanningSoftBlocked(
            planningSnapshot,
            ComputePlanningRiskScore(planningSnapshot));
    }

    private static ControlIntent BuildGeometryEscapeIntent(
        VehicleState state,
        RuntimeScenarioGeometrySnapshot? geometrySnapshot,
        double finalRiskScore,
        double planningRiskScore,
        RuntimePlanningSnapshot? planningSnapshot)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        var target = geometry.HasEscapeTarget
            ? geometry.EscapeTarget
            : BuildFallbackGeometryEscapeTarget(state);

        var headingDeg = geometry.HasEscapeHeading
            ? geometry.EscapeHeadingDeg
            : BearingDeg2D(state.Position, target);

        var speed = geometry.CollisionCandidate || geometry.HardBlocked
            ? 0.0
            : Math.Min(geometry.SuggestedSpeedMps, 0.20);

        return new ControlIntent(
            Kind: geometry.CollisionCandidate || geometry.HardBlocked
                ? ControlIntentKind.HoldPosition
                : ControlIntentKind.AvoidObstacle,
            TargetPosition: geometry.CollisionCandidate || geometry.HardBlocked
                ? state.Position
                : target,
            TargetHeadingDeg: headingDeg,
            DesiredForwardSpeedMps: speed,
            DesiredDepthMeters: state.Position.Z,
            DesiredAltitudeMeters: 0.0,
            HoldHeading: true,
            HoldDepth: true,
            AllowReverse: false,
            RiskLevel: Math.Clamp(Math.Max(finalRiskScore, geometry.RiskScore), 0.0, 1.0),
            Reason:
                $"{geometry.Summary}" +
                $"|GEOM_RISK:{geometry.RiskScore:F2}" +
                $"|PLAN_RISK:{planningRiskScore:F2}" +
                $"|FINAL_RISK:{finalRiskScore:F2}" +
                $"|NEAREST:{geometry.NearestObstacleId ?? "none"}" +
                $"|CLEAR:{geometry.NearestClearanceMeters:F2}" +
                $"|PLAN:{planningSnapshot?.Summary ?? "NO_PLAN"}");
    }

    private static Vec3 ResolveGeometryReferenceTarget(
        RuntimePlanningSnapshot? planningSnapshot,
        TaskDefinition? currentTask,
        VehicleState state)
    {
        var snapshot = (planningSnapshot ?? RuntimePlanningSnapshot.Empty)
            .Sanitized();

        if (snapshot.HasPlan &&
            snapshot.IsValid &&
            snapshot.AgeMs <= 750.0 &&
            snapshot.Trajectory.LookAheadPoint is not null)
        {
            return snapshot.Trajectory.LookAheadPoint.Position;
        }

        if (currentTask?.Target is Vec3 taskTarget)
            return taskTarget;

        var yawRad = state.Orientation.YawDeg * Math.PI / 180.0;

        return new Vec3(
            state.Position.X + Math.Cos(yawRad) * 4.0,
            state.Position.Y + Math.Sin(yawRad) * 4.0,
            state.Position.Z);
    }

    private static bool ShouldBlockScenarioAdvance(
        RuntimeScenarioGeometrySnapshot? geometrySnapshot)
    {
        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        return geometry.CollisionCandidate || geometry.HardBlocked;
    }

    private static Vec3 BuildFallbackGeometryEscapeTarget(
        VehicleState state)
    {
        var yawRad = state.Orientation.YawDeg * Math.PI / 180.0;

        return new Vec3(
            state.Position.X - Math.Cos(yawRad) * 1.5,
            state.Position.Y - Math.Sin(yawRad) * 1.5,
            state.Position.Z);
    }

    private static double BearingDeg2D(Vec3 from, Vec3 to)
    {
        var angle = Math.Atan2(
            to.Y - from.Y,
            to.X - from.X) * 180.0 / Math.PI;

        return NormalizeAngleDegLocal(angle);
    }

    private static double NormalizeAngleDegLocal(double degrees)
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