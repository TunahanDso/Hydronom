using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Analysis / Planning / Geometry katmanlarının Decision katmanına vereceği sürüş öneri profili.
    ///
    /// Bu model doğrudan komut üretmez.
    /// Karar modülüne "nasıl davranmalısın?" bilgisini verir.
    ///
    /// Önemli prensip:
    /// Güvenlik katmanı aracı öldüren bir duvar değil, geçişi şekillendiren bir rehber olmalıdır.
    /// Bu yüzden MergeConservative adı geriye dönük uyumluluk için korunur; ancak davranış artık
    /// "en korkak modül kazansın" değil, "last-resort stop yoksa güvenli ve canlı geçiş üret" mantığıdır.
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
        string PrimaryReason,
        bool HasPassableCorridor,
        double CorridorCenterOffsetDeg,
        double CorridorWidthMeters,
        double CorridorClearanceMeters,
        double CorridorConfidence,
        bool SuppressObstaclePanic,
        bool PreferCorridorHeading
    )
    {
        private const double SoftAdviceMinSpeedScale = 0.25;
        private const double SoftAdviceMinThrottleScale = 0.22;

        private const double CorridorMinSpeedScale = 0.55;
        private const double CorridorMinThrottleScale = 0.45;

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
            PrimaryReason: "NEUTRAL",
            HasPassableCorridor: false,
            CorridorCenterOffsetDeg: 0.0,
            CorridorWidthMeters: 0.0,
            CorridorClearanceMeters: 0.0,
            CorridorConfidence: 0.0,
            SuppressObstaclePanic: false,
            PreferCorridorHeading: false
        );

        public DecisionAdviceProfile Sanitized()
        {
            bool hasCorridor =
                HasPassableCorridor &&
                double.IsFinite(CorridorWidthMeters) &&
                CorridorWidthMeters > 0.0 &&
                double.IsFinite(CorridorConfidence) &&
                CorridorConfidence > 0.0;

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
                    : PrimaryReason.Trim(),
                HasPassableCorridor = hasCorridor,
                CorridorCenterOffsetDeg = ClampRange(CorridorCenterOffsetDeg, -120.0, 120.0, 0.0),
                CorridorWidthMeters = ClampRange(CorridorWidthMeters, 0.0, 100.0, 0.0),
                CorridorClearanceMeters = ClampRange(CorridorClearanceMeters, 0.0, 100.0, 0.0),
                CorridorConfidence = Clamp01OrDefault(CorridorConfidence, 0.0),
                SuppressObstaclePanic = hasCorridor && SuppressObstaclePanic,
                PreferCorridorHeading = hasCorridor && PreferCorridorHeading
            };
        }

        public DecisionAdviceProfile MergeConservative(DecisionAdviceProfile other)
        {
            var a = Sanitized();
            var b = other.Sanitized();

            var corridor = PickBetterCorridor(a, b);

            bool hasCorridor = corridor.HasPassableCorridor;
            bool missionAbort = a.RecommendMissionAbort || b.RecommendMissionAbort;
            bool returnHome = a.RecommendReturnHome || b.RecommendReturnHome;

            bool lastResortStop =
                missionAbort ||
                IsLastResortStopAdvice(a) ||
                IsLastResortStopAdvice(b);

            string reason = BuildMergedReason(a, b);

            /*
             * Gerçek hard/collision/abort varsa güvenlik kazanır.
             * Bu, teknenin gerçekten bindirme veya kaçınılmaz çarpışma durumudur.
             */
            if (lastResortStop)
            {
                return new DecisionAdviceProfile(
                    MaxSpeedScale: Math.Min(a.MaxSpeedScale, b.MaxSpeedScale),
                    ThrottleScale: Math.Min(a.ThrottleScale, b.ThrottleScale),
                    YawAggressionScale: Math.Max(a.YawAggressionScale, b.YawAggressionScale),
                    ArrivalCautionScale: Math.Max(a.ArrivalCautionScale, b.ArrivalCautionScale),
                    ObstacleAvoidanceUrgency: Math.Max(a.ObstacleAvoidanceUrgency, b.ObstacleAvoidanceUrgency),
                    HoldPreference: Math.Max(a.HoldPreference, b.HoldPreference),
                    ForceCoast: a.ForceCoast || b.ForceCoast,
                    PreferSafeHeading: true,
                    RequireSlowMode: true,
                    RecommendHold: a.RecommendHold || b.RecommendHold || missionAbort,
                    RecommendReturnHome: returnHome,
                    RecommendMissionAbort: missionAbort,
                    PrimaryReason: reason,
                    HasPassableCorridor: false,
                    CorridorCenterOffsetDeg: 0.0,
                    CorridorWidthMeters: 0.0,
                    CorridorClearanceMeters: 0.0,
                    CorridorConfidence: 0.0,
                    SuppressObstaclePanic: false,
                    PreferCorridorHeading: false
                ).Sanitized();
            }

            /*
             * Geçilebilir koridor varsa:
             * - Tek modülün ForceCoast/Hold tavsiyesi sistemi öldüremez.
             * - Gaz/hız tabanı korunur.
             * - Obstacle panic bastırılır.
             * - Güvenlik, "dur" değil "koridordan geç" şeklinde davranır.
             */
            if (hasCorridor)
            {
                double confidence = Math.Clamp(corridor.CorridorConfidence, 0.0, 1.0);

                double minSpeedWithCorridor =
                    Math.Clamp(CorridorMinSpeedScale + confidence * 0.20, CorridorMinSpeedScale, 0.82);

                double minThrottleWithCorridor =
                    Math.Clamp(CorridorMinThrottleScale + confidence * 0.20, CorridorMinThrottleScale, 0.75);

                double mergedSpeed = Math.Max(
                    Math.Min(a.MaxSpeedScale, b.MaxSpeedScale),
                    minSpeedWithCorridor);

                double mergedThrottle = Math.Max(
                    Math.Min(a.ThrottleScale, b.ThrottleScale),
                    minThrottleWithCorridor);

                double urgency = Math.Min(
                    Math.Max(a.ObstacleAvoidanceUrgency, b.ObstacleAvoidanceUrgency),
                    0.70);

                return new DecisionAdviceProfile(
                    MaxSpeedScale: mergedSpeed,
                    ThrottleScale: mergedThrottle,
                    YawAggressionScale: Math.Max(a.YawAggressionScale, b.YawAggressionScale),
                    ArrivalCautionScale: Math.Max(a.ArrivalCautionScale, b.ArrivalCautionScale),
                    ObstacleAvoidanceUrgency: urgency,
                    HoldPreference: Math.Min(Math.Max(a.HoldPreference, b.HoldPreference), 0.35),
                    ForceCoast: false,
                    PreferSafeHeading: true,
                    RequireSlowMode: a.RequireSlowMode || b.RequireSlowMode || urgency >= 0.45,
                    RecommendHold: false,
                    RecommendReturnHome: returnHome,
                    RecommendMissionAbort: false,
                    PrimaryReason: AppendReason(reason, "COOPERATIVE_PASSABLE_CORRIDOR"),
                    HasPassableCorridor: true,
                    CorridorCenterOffsetDeg: corridor.CorridorCenterOffsetDeg,
                    CorridorWidthMeters: corridor.CorridorWidthMeters,
                    CorridorClearanceMeters: corridor.CorridorClearanceMeters,
                    CorridorConfidence: corridor.CorridorConfidence,
                    SuppressObstaclePanic: true,
                    PreferCorridorHeading: a.PreferCorridorHeading || b.PreferCorridorHeading || corridor.CorridorConfidence >= 0.45
                ).Sanitized();
            }

            /*
             * Koridor yok ama hard stop da yoksa:
             * Bu hâlâ "öl" anlamına gelmez.
             * Sistem recovery/avoidance için canlı kalmalı.
             * Throttle ve speed tamamen sıfıra gömülmez.
             */
            double rawSpeedScale = Math.Min(a.MaxSpeedScale, b.MaxSpeedScale);
            double rawThrottleScale = Math.Min(a.ThrottleScale, b.ThrottleScale);
            double rawUrgency = Math.Max(a.ObstacleAvoidanceUrgency, b.ObstacleAvoidanceUrgency);
            double rawHoldPreference = Math.Max(a.HoldPreference, b.HoldPreference);

            bool bothDemandCoast = a.ForceCoast && b.ForceCoast;
            bool bothDemandHold = a.RecommendHold && b.RecommendHold;

            bool allowSoftCoast =
                bothDemandCoast &&
                rawUrgency >= 0.95 &&
                rawHoldPreference >= 0.85;

            bool allowSoftHold =
                bothDemandHold &&
                rawUrgency >= 0.95 &&
                rawHoldPreference >= 0.90;

            return new DecisionAdviceProfile(
                MaxSpeedScale: Math.Max(rawSpeedScale, SoftAdviceMinSpeedScale),
                ThrottleScale: Math.Max(rawThrottleScale, SoftAdviceMinThrottleScale),
                YawAggressionScale: Math.Max(a.YawAggressionScale, b.YawAggressionScale),
                ArrivalCautionScale: Math.Max(a.ArrivalCautionScale, b.ArrivalCautionScale),
                ObstacleAvoidanceUrgency: rawUrgency,
                HoldPreference: allowSoftHold ? rawHoldPreference : Math.Min(rawHoldPreference, 0.65),
                ForceCoast: allowSoftCoast,
                PreferSafeHeading: a.PreferSafeHeading || b.PreferSafeHeading,
                RequireSlowMode: a.RequireSlowMode || b.RequireSlowMode || rawUrgency >= 0.65,
                RecommendHold: allowSoftHold,
                RecommendReturnHome: returnHome,
                RecommendMissionAbort: false,
                PrimaryReason: AppendReason(reason, "COOPERATIVE_RECOVERY_ALLOWED"),
                HasPassableCorridor: false,
                CorridorCenterOffsetDeg: 0.0,
                CorridorWidthMeters: 0.0,
                CorridorClearanceMeters: Math.Max(a.CorridorClearanceMeters, b.CorridorClearanceMeters),
                CorridorConfidence: 0.0,
                SuppressObstaclePanic: false,
                PreferCorridorHeading: false
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
                $"hold={RecommendHold} rth={RecommendReturnHome} abort={RecommendMissionAbort} " +
                $"corridor={HasPassableCorridor} corridorOffset={CorridorCenterOffsetDeg:F1} " +
                $"corridorWidth={CorridorWidthMeters:F2} corridorClear={CorridorClearanceMeters:F2} " +
                $"corridorConf={CorridorConfidence:F2} suppressPanic={SuppressObstaclePanic} " +
                $"preferCorridor={PreferCorridorHeading}";
        }

        private static DecisionAdviceProfile PickBetterCorridor(
            DecisionAdviceProfile a,
            DecisionAdviceProfile b)
        {
            if (a.HasPassableCorridor && b.HasPassableCorridor)
                return b.CorridorConfidence > a.CorridorConfidence ? b : a;

            if (a.HasPassableCorridor)
                return a;

            if (b.HasPassableCorridor)
                return b;

            return Neutral;
        }

        private static string BuildMergedReason(
            DecisionAdviceProfile a,
            DecisionAdviceProfile b)
        {
            if (a.PrimaryReason == "NEUTRAL")
                return b.PrimaryReason;

            if (b.PrimaryReason == "NEUTRAL")
                return a.PrimaryReason;

            if (string.Equals(a.PrimaryReason, b.PrimaryReason, StringComparison.OrdinalIgnoreCase))
                return a.PrimaryReason;

            return $"{a.PrimaryReason}+{b.PrimaryReason}";
        }

        private static string AppendReason(string reason, string suffix)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason == "NEUTRAL")
                return suffix;

            if (reason.Contains(suffix, StringComparison.OrdinalIgnoreCase))
                return reason;

            return $"{reason}+{suffix}";
        }

        private static bool IsLastResortStopAdvice(DecisionAdviceProfile advice)
        {
            if (advice.RecommendMissionAbort)
                return true;

            if (ReasonContains(advice, "COLLISION_CANDIDATE"))
                return true;

            if (ReasonContains(advice, "HARD_COLLISION"))
                return true;

            if (ReasonContains(advice, "HARD_BLOCK"))
                return true;

            if (ReasonContains(advice, "MISSION_ABORT"))
                return true;

            /*
             * CRITICAL_CLOSE_OBSTACLE tek başına artık last-resort stop değildir.
             * Çünkü yakın engel varsa bile rota/koridor/recovery alternatifi aranmalıdır.
             */
            return false;
        }

        private static bool ReasonContains(
            DecisionAdviceProfile advice,
            string token)
        {
            return !string.IsNullOrWhiteSpace(advice.PrimaryReason) &&
                   advice.PrimaryReason.Contains(token, StringComparison.OrdinalIgnoreCase);
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