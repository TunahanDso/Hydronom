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
         * Sadece gerÃ§ek Ã§arpÄ±ÅŸma adayÄ± / hard block varsa aracÄ± tut.
         * Bu kural parkur giriÅŸindeki iki sarÄ± duba gibi geÃ§ilebilir kapÄ±larÄ± durdurmamalÄ±.
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
         * Burada kritik dÃ¼zeltme var:
         *
         * Eski davranÄ±ÅŸ:
         *   geometry.SoftBlocked || geometry.RequiresSlowMode
         *
         * Bu, parkur giriÅŸinde "soft=False ama RequiresSlowMode=True" durumunu
         * GEOMETRY_SOFT_COLLISION_GUARD'a Ã§eviriyordu.
         *
         * Yeni davranÄ±ÅŸ:
         *   Sadece geometry.SoftBlocked gerÃ§ekse soft collision guard Ã§alÄ±ÅŸÄ±r.
         *
         * Risk 0.35-0.55 aralÄ±ÄŸÄ± ve clearance geÃ§ilebilir ise araÃ§ korkup durmaz;
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
         * Kritik dÃ¼zeltme:
         * geometry.RequiresSlowMode artÄ±k world-aware soft block sayÄ±lmÄ±yor.
         * Ã‡Ã¼nkÃ¼ giriÅŸ kapÄ±sÄ± gibi geÃ§ilebilir corridorlarda sadece "dikkatli geÃ§"
         * anlamÄ±na gelebilir; collision guard deÄŸildir.
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
        RuntimePlanningSnapshot? planningSnapshot,
        VehicleCapabilityProfile capability)
    {
        capability = capability.Sanitized();

        var geometry = (geometrySnapshot ?? RuntimeScenarioGeometrySnapshot.Empty)
            .Sanitized();

        var risk = Math.Clamp(
            Math.Max(finalRiskScore, geometry.RiskScore),
            0.0,
            1.0);

        var escapeTarget = geometry.HasEscapeTarget
            ? geometry.EscapeTarget
            : BuildFallbackGeometryEscapeTarget(state);

        var escapeHeadingDeg = geometry.HasEscapeHeading
            ? NormalizeAngleDegLocal(geometry.EscapeHeadingDeg)
            : BearingDeg2D(state.Position, escapeTarget);

        var collisionOrHard =
            geometry.CollisionCandidate ||
            geometry.HardBlocked;

        var hasReverse =
            capability.HasReverseAuthority &&
            capability.NegativeSurgeAuthority > 0.05 &&
            capability.ReverseConfidence > 0.05;

        var hasLateral =
            capability.CanGenerateLateralForce &&
            capability.LateralConfidence > 0.05 &&
            (capability.PositiveSwayAuthority > 0.05 ||
             capability.NegativeSwayAuthority > 0.05);

        var hasYaw =
            capability.CanGenerateYawMoment &&
            capability.YawConfidence > 0.03 &&
            (capability.PositiveYawAuthority > 0.02 ||
             capability.NegativeYawAuthority > 0.02);

        var kind = collisionOrHard
            ? ControlIntentKind.HoldPosition
            : ControlIntentKind.AvoidObstacle;

        var targetPosition = collisionOrHard
            ? state.Position
            : escapeTarget;

        var targetHeadingDeg = hasYaw
            ? escapeHeadingDeg
            : state.Orientation.YawDeg;

        var desiredSpeed = collisionOrHard
            ? 0.0
            : Math.Clamp(
                Math.Min(geometry.SuggestedSpeedMps, 0.20),
                0.0,
                0.28);

        var allowReverse = false;
        var recoveryMode = collisionOrHard
            ? "hold_depth"
            : "forward_escape_depth_hold";

        if (collisionOrHard)
        {
            if (!hasLateral && hasReverse)
            {
                /*
                 * Platform bağımsız kritik kural:
                 * Yanal kuvvet yok ama reverse var ise imkânsız Fy istemiyoruz.
                 * Araç kendi ekseni boyunca geri kaçıyor; yaw varsa güvenli heading'e dönebiliyor.
                 */
                var reverseDistance = geometry.CollisionCandidate ? 1.75 : 1.35;

                targetPosition = BuildReverseGeometryEscapeTarget(
                    state,
                    reverseDistance);

                kind = ControlIntentKind.AvoidObstacle;
                allowReverse = true;

                desiredSpeed = -Math.Clamp(
                    0.12 + risk * 0.14,
                    0.14,
                    0.30);

                targetHeadingDeg = hasYaw
                    ? escapeHeadingDeg
                    : state.Orientation.YawDeg;

                recoveryMode = hasYaw
                    ? "reverse_surge_yaw_depth_hold"
                    : "reverse_surge_depth_hold";
            }
            else if (hasLateral)
            {
                /*
                 * Omnidirectional / lateral authority olan platformlarda
                 * güvenli escape target'a yan kuvvetle çıkmak mümkündür.
                 */
                targetPosition = escapeTarget;
                kind = ControlIntentKind.AvoidObstacle;
                desiredSpeed = 0.0;
                allowReverse = hasReverse;
                targetHeadingDeg = hasYaw
                    ? escapeHeadingDeg
                    : state.Orientation.YawDeg;

                recoveryMode = hasYaw
                    ? "lateral_yaw_depth_hold"
                    : "lateral_depth_hold";
            }
            else if (hasYaw)
            {
                /*
                 * Reverse ve lateral yoksa, en azından heading'i güvenli yöne çevirip
                 * derinliği tutuyoruz. Bu hâlâ platform bağımsız güvenli davranıştır.
                 */
                targetPosition = state.Position;
                kind = ControlIntentKind.HoldPosition;
                desiredSpeed = 0.0;
                targetHeadingDeg = escapeHeadingDeg;
                allowReverse = false;
                recoveryMode = "yaw_only_depth_hold";
            }
        }
        else if (!hasLateral && hasReverse)
        {
            /*
             * Non-critical escape target arkada kalıyorsa reverse'e izin ver.
             * Önde/yan-önde kalıyorsa yaw + pozitif surge ile gidilir.
             */
            var deltaWorld = new Vec3(
                escapeTarget.X - state.Position.X,
                escapeTarget.Y - state.Position.Y,
                0.0);

            var deltaBody = state.Orientation.WorldToBody(deltaWorld);

            if (deltaBody.X < -0.25)
            {
                desiredSpeed = -Math.Clamp(
                    Math.Abs(deltaBody.X) * 0.08,
                    0.10,
                    0.18);

                allowReverse = true;
                recoveryMode = hasYaw
                    ? "reverse_to_escape_target_yaw_depth_hold"
                    : "reverse_to_escape_target_depth_hold";
            }
        }

        return new ControlIntent(
            Kind: kind,
            TargetPosition: targetPosition,
            TargetHeadingDeg: targetHeadingDeg,
            DesiredForwardSpeedMps: desiredSpeed,
            DesiredDepthMeters: state.Position.Z,
            DesiredAltitudeMeters: 0.0,
            HoldHeading: true,
            HoldDepth: true,
            AllowReverse: allowReverse,
            RiskLevel: risk,
            Reason:
                $"{geometry.Summary}" +
                $"|GEOM_ESCAPE_RECOVERY:{recoveryMode}" +
                $"|CAP_REV:{hasReverse}" +
                $"|CAP_LAT:{hasLateral}" +
                $"|CAP_YAW:{hasYaw}" +
                $"|GEOM_RISK:{geometry.RiskScore:F2}" +
                $"|PLAN_RISK:{planningRiskScore:F2}" +
                $"|FINAL_RISK:{finalRiskScore:F2}" +
                $"|NEAREST:{geometry.NearestObstacleId ?? "none"}" +
                $"|CLEAR:{geometry.NearestClearanceMeters:F2}" +
                $"|PLAN:{planningSnapshot?.Summary ?? "NO_PLAN"}" +
                $"|CAP:{capability.Summary}");
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

    private static Vec3 BuildReverseGeometryEscapeTarget(
        VehicleState state,
        double distanceMeters)
    {
        var distance = double.IsFinite(distanceMeters)
            ? Math.Clamp(distanceMeters, 0.50, 3.00)
            : 1.50;

        var yawRad = state.Orientation.YawDeg * Math.PI / 180.0;

        return new Vec3(
            state.Position.X - Math.Cos(yawRad) * distance,
            state.Position.Y - Math.Sin(yawRad) * distance,
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