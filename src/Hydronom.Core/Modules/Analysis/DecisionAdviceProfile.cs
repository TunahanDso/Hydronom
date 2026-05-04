using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Analysis katmanının Decision katmanına vereceği sürüş öneri profili.
    ///
    /// Bu model doğrudan komut üretmez.
    /// Karar modülüne "nasıl davranmalısın?" bilgisini verir.
    /// </summary>
    public readonly record struct DecisionAdviceProfile(
        double MaxSpeedScale,
        double ThrottleScale,
        double YawAggressionScale,
        double ArrivalCautionScale,
        double ObstacleAvoidanceUrgency,
        double HoldPreference,
        bool ForceCoast,
        bool PreferSafeHeading,
        bool RequireSlowMode,
        bool RecommendHold,
        bool RecommendReturnHome,
        bool RecommendMissionAbort,
        string PrimaryReason
    )
    {
        public static DecisionAdviceProfile Neutral { get; } = new(
            MaxSpeedScale: 1.0,
            ThrottleScale: 1.0,
            YawAggressionScale: 1.0,
            ArrivalCautionScale: 1.0,
            ObstacleAvoidanceUrgency: 0.0,
            HoldPreference: 0.0,
            ForceCoast: false,
            PreferSafeHeading: false,
            RequireSlowMode: false,
            RecommendHold: false,
            RecommendReturnHome: false,
            RecommendMissionAbort: false,
            PrimaryReason: "NEUTRAL"
        );

        public DecisionAdviceProfile Sanitized()
        {
            return this with
            {
                MaxSpeedScale = Clamp01OrDefault(MaxSpeedScale, 1.0),
                ThrottleScale = Clamp01OrDefault(ThrottleScale, 1.0),
                YawAggressionScale = ClampRange(YawAggressionScale, 0.25, 2.0, 1.0),
                ArrivalCautionScale = ClampRange(ArrivalCautionScale, 0.5, 3.0, 1.0),
                ObstacleAvoidanceUrgency = Clamp01OrDefault(ObstacleAvoidanceUrgency, 0.0),
                HoldPreference = Clamp01OrDefault(HoldPreference, 0.0),
                PrimaryReason = string.IsNullOrWhiteSpace(PrimaryReason)
                    ? "NEUTRAL"
                    : PrimaryReason.Trim()
            };
        }

        public DecisionAdviceProfile MergeConservative(DecisionAdviceProfile other)
        {
            var a = Sanitized();
            var b = other.Sanitized();

            string reason;
            if (a.PrimaryReason == "NEUTRAL")
                reason = b.PrimaryReason;
            else if (b.PrimaryReason == "NEUTRAL")
                reason = a.PrimaryReason;
            else
                reason = $"{a.PrimaryReason}+{b.PrimaryReason}";

            return new DecisionAdviceProfile(
                MaxSpeedScale: Math.Min(a.MaxSpeedScale, b.MaxSpeedScale),
                ThrottleScale: Math.Min(a.ThrottleScale, b.ThrottleScale),
                YawAggressionScale: Math.Max(a.YawAggressionScale, b.YawAggressionScale),
                ArrivalCautionScale: Math.Max(a.ArrivalCautionScale, b.ArrivalCautionScale),
                ObstacleAvoidanceUrgency: Math.Max(a.ObstacleAvoidanceUrgency, b.ObstacleAvoidanceUrgency),
                HoldPreference: Math.Max(a.HoldPreference, b.HoldPreference),
                ForceCoast: a.ForceCoast || b.ForceCoast,
                PreferSafeHeading: a.PreferSafeHeading || b.PreferSafeHeading,
                RequireSlowMode: a.RequireSlowMode || b.RequireSlowMode,
                RecommendHold: a.RecommendHold || b.RecommendHold,
                RecommendReturnHome: a.RecommendReturnHome || b.RecommendReturnHome,
                RecommendMissionAbort: a.RecommendMissionAbort || b.RecommendMissionAbort,
                PrimaryReason: reason
            ).Sanitized();
        }

        public override string ToString()
        {
            return
                $"Advice reason={PrimaryReason} " +
                $"speedScale={MaxSpeedScale:F2} throttleScale={ThrottleScale:F2} " +
                $"yawScale={YawAggressionScale:F2} arrivalCaution={ArrivalCautionScale:F2} " +
                $"avoidUrgency={ObstacleAvoidanceUrgency:F2} holdPref={HoldPreference:F2} " +
                $"coast={ForceCoast} safeHeading={PreferSafeHeading} slow={RequireSlowMode} " +
                $"hold={RecommendHold} rth={RecommendReturnHome} abort={RecommendMissionAbort}";
        }

        private static double Clamp01OrDefault(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, 0.0, 1.0);
        }

        private static double ClampRange(double value, double min, double max, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Clamp(value, min, max);
        }
    }
}