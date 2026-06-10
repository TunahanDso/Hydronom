using System;
using Hydronom.Core.Modules;
using Hydronom.Runtime.Planning;

partial class Program
{
    private static DecisionAdviceProfile BuildWorldAwareDecisionAdvice(
        DecisionAdviceProfile analysisAdvice,
        RuntimePlanningSnapshot? planningSnapshot)
    {
        var safeAnalysisAdvice = analysisAdvice.Sanitized();
        var planningAdvice = BuildPlanningDecisionAdvice(planningSnapshot);

        return safeAnalysisAdvice
            .MergeConservative(planningAdvice)
            .Sanitized();
    }

    private static DecisionAdviceProfile BuildPlanningDecisionAdvice(
        RuntimePlanningSnapshot? planningSnapshot)
    {
        var snapshot = (planningSnapshot ?? RuntimePlanningSnapshot.Empty).Sanitized();

        if (!snapshot.HasPlan ||
            !snapshot.IsValid ||
            snapshot.AgeMs > 500.0 ||
            snapshot.RequiresReplan)
        {
            if (snapshot.RequiresReplan)
            {
                return (DecisionAdviceProfile.Neutral with
                {
                    MaxSpeedScale = 0.0,
                    ThrottleScale = 0.0,
                    YawAggressionScale = 0.45,
                    ArrivalCautionScale = 2.0,
                    ObstacleAvoidanceUrgency = 1.0,
                    RequireSlowMode = true,
                    PreferSafeHeading = true,
                    RecommendHold = true,
                    ForceCoast = true,
                    PrimaryReason = "PLAN_REPLAN_REQUIRED",
                    HasPassableCorridor = false,
                    CorridorCenterOffsetDeg = 0.0,
                    CorridorWidthMeters = 0.0,
                    CorridorClearanceMeters = 0.0,
                    CorridorConfidence = 0.0,
                    SuppressObstaclePanic = false,
                    PreferCorridorHeading = false
                }).Sanitized();
            }

            return DecisionAdviceProfile.Neutral;
        }

        var riskScore = ComputePlanningRiskScore(snapshot);

        var localRisk = snapshot.LocalPath.Risk.Sanitized();
        var trajectoryRisk = snapshot.Trajectory.Risk.Sanitized();

        var clearance = ResolvePlanningClearanceMeters(
            localRisk.MinimumClearanceMeters,
            trajectoryRisk.MinimumClearanceMeters);

        var hardBlocked = IsPlanningHardBlocked(snapshot, riskScore);
        var softBlocked = IsPlanningSoftBlocked(snapshot, riskScore);

        if (hardBlocked)
        {
            return (DecisionAdviceProfile.Neutral with
            {
                MaxSpeedScale = 0.0,
                ThrottleScale = 0.0,
                YawAggressionScale = 0.45,
                ArrivalCautionScale = 2.0,
                ObstacleAvoidanceUrgency = 1.0,
                RequireSlowMode = true,
                PreferSafeHeading = true,
                RecommendHold = true,
                ForceCoast = true,
                PrimaryReason = "PLAN_HARD_COLLISION_GUARD",
                HasPassableCorridor = false,
                CorridorCenterOffsetDeg = 0.0,
                CorridorWidthMeters = 0.0,
                CorridorClearanceMeters = Math.Max(0.0, clearance),
                CorridorConfidence = 0.0,
                SuppressObstaclePanic = false,
                PreferCorridorHeading = false
            }).Sanitized();
        }

        if (softBlocked)
        {
            return (DecisionAdviceProfile.Neutral with
            {
                MaxSpeedScale = 0.25,
                ThrottleScale = 0.25,
                YawAggressionScale = 0.65,
                ArrivalCautionScale = 1.75,
                ObstacleAvoidanceUrgency = Math.Clamp(riskScore, 0.65, 0.95),
                RequireSlowMode = true,
                PreferSafeHeading = true,
                RecommendHold = riskScore >= 0.90,
                ForceCoast = false,
                PrimaryReason = "PLAN_SOFT_RISK_GUARD",
                HasPassableCorridor = false,
                CorridorCenterOffsetDeg = 0.0,
                CorridorWidthMeters = 0.0,
                CorridorClearanceMeters = Math.Max(0.0, clearance),
                CorridorConfidence = 0.0,
                SuppressObstaclePanic = false,
                PreferCorridorHeading = false
            }).Sanitized();
        }

        var isWorldCorridor =
            ContainsToken(snapshot.Summary, "WORLD_CORRIDOR") ||
            ContainsToken(snapshot.LocalPath.Summary, "WORLD_CORRIDOR") ||
            snapshot.LocalPath.Mode.ToString().Equals("Corridor", StringComparison.OrdinalIgnoreCase);

        var hasSafeCorridor =
            isWorldCorridor &&
            riskScore <= 0.45 &&
            clearance >= 1.0;

        if (!hasSafeCorridor)
        {
            return (DecisionAdviceProfile.Neutral with
            {
                MaxSpeedScale = riskScore <= 0.20 ? 0.90 : 0.70,
                ThrottleScale = riskScore <= 0.20 ? 0.85 : 0.60,
                YawAggressionScale = 0.80,
                ArrivalCautionScale = riskScore <= 0.20 ? 1.05 : 1.35,
                ObstacleAvoidanceUrgency = Math.Clamp(riskScore, 0.05, 0.55),
                RequireSlowMode = snapshot.RequiresSlowMode || riskScore >= 0.30,
                PreferSafeHeading = riskScore >= 0.20,
                PrimaryReason = "PLAN_CAUTION",
                HasPassableCorridor = false,
                CorridorCenterOffsetDeg = 0.0,
                CorridorWidthMeters = 0.0,
                CorridorClearanceMeters = Math.Max(0.0, clearance),
                CorridorConfidence = Math.Clamp(1.0 - riskScore, 0.0, 1.0),
                SuppressObstaclePanic = false,
                PreferCorridorHeading = false
            }).Sanitized();
        }

        var confidence = ComputeCorridorConfidence(
            riskScore,
            clearance,
            snapshot.AgeMs);

        return (DecisionAdviceProfile.Neutral with
        {
            MaxSpeedScale = riskScore <= 0.20 ? 0.90 : 0.75,
            ThrottleScale = riskScore <= 0.20 ? 0.85 : 0.65,
            YawAggressionScale = 0.85,
            ArrivalCautionScale = riskScore <= 0.20 ? 1.10 : 1.35,
            ObstacleAvoidanceUrgency = Math.Clamp(riskScore, 0.05, 0.35),
            RequireSlowMode = snapshot.RequiresSlowMode || riskScore >= 0.30,
            PreferSafeHeading = true,
            PrimaryReason = "WORLD_SAFE_CORRIDOR",
            HasPassableCorridor = true,
            CorridorCenterOffsetDeg = 0.0,
            CorridorWidthMeters = Math.Max(0.0, clearance),
            CorridorClearanceMeters = Math.Max(0.0, clearance),
            CorridorConfidence = confidence,
            SuppressObstaclePanic = true,
            PreferCorridorHeading = false
        }).Sanitized();
    }

    private static double ComputePlanningRiskScore(
        RuntimePlanningSnapshot? planningSnapshot)
    {
        var snapshot = (planningSnapshot ?? RuntimePlanningSnapshot.Empty).Sanitized();

        if (!snapshot.HasPlan || !snapshot.IsValid)
            return 1.0;

        var localRisk = snapshot.LocalPath.Risk.Sanitized();
        var trajectoryRisk = snapshot.Trajectory.Risk.Sanitized();
        var lookAheadRisk = snapshot.Trajectory.LookAheadPoint?.RiskScore ?? 0.0;

        var risk = Math.Max(
            Math.Max(localRisk.RiskScore, trajectoryRisk.RiskScore),
            lookAheadRisk);

        if (snapshot.RequiresReplan)
            risk = Math.Max(risk, 0.95);

        if (snapshot.AgeMs > 500.0)
            risk = Math.Max(risk, 0.90);

        if (ContainsToken(snapshot.Summary, "COLLISION_CANDIDATE") ||
            ContainsToken(snapshot.LocalPath.Summary, "COLLISION_CANDIDATE") ||
            ContainsToken(snapshot.Trajectory.Summary, "COLLISION_CANDIDATE"))
        {
            risk = Math.Max(risk, 0.98);
        }

        if (ContainsToken(snapshot.Summary, "BLOCKED") ||
            ContainsToken(snapshot.LocalPath.Summary, "BLOCKED") ||
            ContainsToken(snapshot.Trajectory.Summary, "BLOCKED"))
        {
            risk = Math.Max(risk, 0.95);
        }

        return Math.Clamp(risk, 0.0, 1.0);
    }

    private static bool IsPlanningHardBlocked(
        RuntimePlanningSnapshot? planningSnapshot,
        double riskScore)
    {
        var snapshot = (planningSnapshot ?? RuntimePlanningSnapshot.Empty).Sanitized();

        if (!snapshot.HasPlan || !snapshot.IsValid)
            return false;

        if (snapshot.AgeMs > 500.0)
            return false;

        if (riskScore >= 0.95)
            return true;

        if (snapshot.RequiresReplan)
            return true;

        if (ContainsToken(snapshot.Summary, "COLLISION_CANDIDATE") ||
            ContainsToken(snapshot.LocalPath.Summary, "COLLISION_CANDIDATE") ||
            ContainsToken(snapshot.Trajectory.Summary, "COLLISION_CANDIDATE"))
        {
            return true;
        }

        if (ContainsToken(snapshot.Summary, "BLOCKED") ||
            ContainsToken(snapshot.LocalPath.Summary, "BLOCKED") ||
            ContainsToken(snapshot.Trajectory.Summary, "BLOCKED"))
        {
            return true;
        }

        return false;
    }

    private static bool IsPlanningSoftBlocked(
        RuntimePlanningSnapshot? planningSnapshot,
        double riskScore)
    {
        var snapshot = (planningSnapshot ?? RuntimePlanningSnapshot.Empty).Sanitized();

        if (!snapshot.HasPlan || !snapshot.IsValid)
            return false;

        if (snapshot.AgeMs > 500.0)
            return false;

        if (riskScore >= 0.70)
            return true;

        if (snapshot.RequiresSlowMode)
            return true;

        return false;
    }

    private static double ResolvePlanningClearanceMeters(
        double localClearance,
        double trajectoryClearance)
    {
        var hasLocal = double.IsFinite(localClearance) && localClearance >= 0.0;
        var hasTrajectory = double.IsFinite(trajectoryClearance) && trajectoryClearance >= 0.0;

        if (hasLocal && hasTrajectory)
            return Math.Min(localClearance, trajectoryClearance);

        if (hasLocal)
            return localClearance;

        if (hasTrajectory)
            return trajectoryClearance;

        return 3.0;
    }

    private static double ComputeCorridorConfidence(
        double riskScore,
        double clearanceMeters,
        double ageMs)
    {
        var riskConfidence = 1.0 - Math.Clamp(riskScore, 0.0, 1.0);

        var clearanceConfidence = Math.Clamp(
            clearanceMeters / 4.0,
            0.0,
            1.0);

        var ageConfidence = 1.0 - Math.Clamp(
            ageMs / 500.0,
            0.0,
            1.0);

        return Math.Clamp(
            riskConfidence * 0.50 +
            clearanceConfidence * 0.35 +
            ageConfidence * 0.15,
            0.0,
            1.0);
    }

    private static bool ContainsToken(string? value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}