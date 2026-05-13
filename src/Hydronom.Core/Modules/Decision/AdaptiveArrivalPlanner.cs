using System;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// Scenario veya görev hedefinin nasıl geçileceğini belirler.
    /// 
    /// FlyThrough:
    /// - Ara checkpointler için kullanılır.
    /// - Araç hedef bölgesine girince durmaya/hold etmeye çalışmaz.
    /// - Amaç, rotayı akıcı şekilde takip etmektir.
    ///
    /// TurnCritical:
    /// - Keskin dönüş, U dönüş, heading toparlama gerektiren hedefler için kullanılır.
    /// - Araç tamamen durmaz ama heading hatası yüksekse daha dikkatli davranır.
    ///
    /// PrecisionStop:
    /// - Final hedef veya gerçekten hassas duruş gereken hedefler için kullanılır.
    /// - Capture/settle/aktif fren mantığı korunur.
    /// </summary>
    public enum ArrivalProfileKind
    {
        FlyThrough = 0,
        TurnCritical = 1,
        PrecisionStop = 2
    }

    /// <summary>
    /// Göreve duyarlı varış planlayıcı.
    ///
    /// v4:
    /// - Scenario hedefleri artık tek tip "yavaşla-yaklaş-tamamla" mantığıyla ele alınmaz.
    /// - FlyThrough / TurnCritical / PrecisionStop profilleri desteklenir.
    /// - Ara checkpointlerde gereksiz creep/hold azaltılır.
    /// - Keskin dönüş hedeflerinde hız kontrollü tutulur ama araç tamamen durdurulmaz.
    /// - Final hedefte hassas varış ve settle davranışı korunur.
    /// - Reverse braking daha sakin seviyeye çekilmiştir.
    /// - Tek yönlü thruster görevlerinde negatif surge istemez.
    /// - İki yönlü/reverse destekli görevlerde kontrollü negatif surge frenine izin verir.
    /// </summary>
    public static class AdaptiveArrivalPlanner
    {
        public static ArrivalPlan PlanMissionArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm,
            double maxReverseThrottleNorm,
            bool allowReverseSurge,
            bool strictCapture,
            string reasonPrefix)
        {
            return PlanMissionArrival(
                distanceM: distanceM,
                planarSpeedMps: planarSpeedMps,
                forwardSpeedMps: forwardSpeedMps,
                targetBodyX: targetBodyX,
                headingErrorDeg: headingErrorDeg,
                baseThrottleNorm: baseThrottleNorm,
                captureRadiusM: captureRadiusM,
                slowRadiusM: slowRadiusM,
                coastRadiusM: coastRadiusM,
                maxCaptureSpeedMps: maxCaptureSpeedMps,
                desiredSpeedFloorMps: desiredSpeedFloorMps,
                estimatedDecelMps2: estimatedDecelMps2,
                creepThrottleNorm: creepThrottleNorm,
                maxApproachThrottleNorm: maxApproachThrottleNorm,
                maxReverseThrottleNorm: maxReverseThrottleNorm,
                allowReverseSurge: allowReverseSurge,
                strictCapture: strictCapture,
                profileKind: strictCapture ? ArrivalProfileKind.PrecisionStop : ArrivalProfileKind.TurnCritical,
                reasonPrefix: reasonPrefix
            );
        }

        public static ArrivalPlan PlanMissionArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm,
            double maxReverseThrottleNorm,
            bool allowReverseSurge,
            bool strictCapture,
            ArrivalProfileKind profileKind,
            string reasonPrefix)
        {
            distanceM = SafeNonNegative(distanceM);
            planarSpeedMps = SafeNonNegative(planarSpeedMps);
            forwardSpeedMps = Safe(forwardSpeedMps);
            targetBodyX = Safe(targetBodyX);

            maxApproachThrottleNorm = Math.Max(0.0, SafeNonNegative(maxApproachThrottleNorm));
            maxReverseThrottleNorm = SafeNonNegative(maxReverseThrottleNorm);
            creepThrottleNorm = SafeNonNegative(creepThrottleNorm);

            baseThrottleNorm = Math.Clamp(Safe(baseThrottleNorm), 0.0, maxApproachThrottleNorm);

            double absHeading = Math.Abs(Safe(headingErrorDeg));
            double decel = Math.Max(0.001, SafeNonNegative(estimatedDecelMps2));

            ArrivalProfileTuning tuning = CreateProfileTuning(
                profileKind,
                captureRadiusM,
                slowRadiusM,
                coastRadiusM,
                maxCaptureSpeedMps,
                desiredSpeedFloorMps,
                creepThrottleNorm,
                maxApproachThrottleNorm,
                maxReverseThrottleNorm,
                allowReverseSurge);

            double stoppingDistance = ComputeStoppingDistance(planarSpeedMps, decel);
            double desiredSpeed = ComputeDesiredSpeed(
                distanceM,
                tuning.CaptureRadiusM,
                tuning.DesiredSpeedFloorMps,
                decel);

            // Türkçe yorum:
            // Fly-through hedeflerde araç hedefe yaklaşırken "çok erken durma" eğilimine girmesin.
            // Bu yüzden istenen hız profilini biraz daha yukarıda tutuyoruz.
            if (profileKind == ArrivalProfileKind.FlyThrough)
            {
                double flyThroughSpeed = ComputeFlyThroughDesiredSpeed(
                    distanceM,
                    tuning.CaptureRadiusM,
                    tuning.SlowRadiusM,
                    tuning.DesiredSpeedFloorMps,
                    tuning.MaxCaptureSpeedMps);

                desiredSpeed = Math.Max(desiredSpeed, flyThroughSpeed);
            }

            // Türkçe yorum:
            // Turn-critical hedeflerde heading hatası büyükse hız hedefini kontrollü düşürüyoruz.
            // Bu, U dönüşte fazla geniş yay çizme riskini azaltır.
            if (profileKind == ArrivalProfileKind.TurnCritical)
            {
                double headingFactor = Math.Clamp(absHeading / 70.0, 0.0, 1.0);
                double turnSpeedCap = Lerp(tuning.MaxCaptureSpeedMps, tuning.DesiredSpeedFloorMps, headingFactor * 0.65);
                desiredSpeed = Math.Min(desiredSpeed, Math.Max(tuning.DesiredSpeedFloorMps, turnSpeedCap));
            }

            double speedError = desiredSpeed - planarSpeedMps;

            bool targetLikelyBehind =
                absHeading >= tuning.OvershootHeadingDeg ||
                targetBodyX < -0.05 ||
                forwardSpeedMps < -0.05;

            bool cannotStopComfortably =
                distanceM <= stoppingDistance + Math.Max(0.35, tuning.CaptureRadiusM * 0.35);

            bool overshootLikely =
                targetLikelyBehind ||
                (cannotStopComfortably && planarSpeedMps > tuning.MaxCaptureSpeedMps);

            if (overshootLikely && distanceM <= Math.Max(tuning.SlowRadiusM, stoppingDistance + tuning.CaptureRadiusM))
            {
                double recoveryThrottle = ComputeRecoveryThrottle(
                    tuning.AllowReverseSurge,
                    forwardSpeedMps,
                    tuning.MaxReverseThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.OvershootRecovery,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: recoveryThrottle,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: recoveryThrottle >= 0.0,
                    ShouldHold: false,
                    IsOvershootLikely: true,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: profileKind == ArrivalProfileKind.TurnCritical ? 1.18 : 1.10,
                    Reason: $"{reasonPrefix}_{profileKind}_OVERSHOOT_RECOVERY"
                );
            }

            if (absHeading > tuning.TurnAlignHeadingDeg)
            {
                double turnAlignThrottle;

                if (tuning.AllowReverseSurge && forwardSpeedMps > tuning.MaxCaptureSpeedMps && profileKind != ArrivalProfileKind.FlyThrough)
                {
                    turnAlignThrottle = -Math.Min(tuning.MaxReverseThrottleNorm * 0.30, tuning.MaxReverseThrottleNorm);
                }
                else
                {
                    // Türkçe yorum:
                    // Fly-through modda keskin heading hatasında bile tamamen sürünmeye düşmüyoruz.
                    // Ama dönüş kritikse veya final ise daha temkinli ilerliyoruz.
                    turnAlignThrottle = profileKind == ArrivalProfileKind.FlyThrough
                        ? Math.Min(Math.Max(tuning.CreepThrottleNorm, baseThrottleNorm * 0.45), tuning.MaxApproachThrottleNorm)
                        : Math.Min(tuning.CreepThrottleNorm, tuning.MaxApproachThrottleNorm);
                }

                return new ArrivalPlan(
                    Phase: ArrivalPhase.TurnAlign,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: turnAlignThrottle,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: turnAlignThrottle >= 0.0 && turnAlignThrottle <= tuning.CreepThrottleNorm,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: profileKind == ArrivalProfileKind.TurnCritical ? 1.15 : 1.05,
                    Reason: $"{reasonPrefix}_{profileKind}_TURN_ALIGN"
                );
            }

            if (distanceM <= tuning.CaptureRadiusM)
            {
                if (profileKind == ArrivalProfileKind.FlyThrough)
                {
                    // Türkçe yorum:
                    // Ara checkpointlerde capture alanına girince durma/hold yapmıyoruz.
                    // Hedef tamamlanma kararını üst seviye karar katmanı verecek.
                    // Bu plan sadece "akıcı geçiş" davranışı üretir.
                    double flyThroughThrottle = Math.Clamp(
                        Math.Max(tuning.CreepThrottleNorm, baseThrottleNorm * 0.35),
                        0.0,
                        tuning.MaxApproachThrottleNorm);

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.Capture,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: flyThroughThrottle,
                        AllowReverseSurge: tuning.AllowReverseSurge,
                        ShouldCoast: false,
                        ShouldHold: false,
                        IsOvershootLikely: false,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: 0.92,
                        Reason: $"{reasonPrefix}_{profileKind}_CAPTURE_PASS"
                    );
                }

                if (profileKind == ArrivalProfileKind.TurnCritical)
                {
                    if (planarSpeedMps > tuning.MaxCaptureSpeedMps)
                    {
                        double turnCaptureThrottle = tuning.AllowReverseSurge
                            ? -ComputeBrakeThrottle(planarSpeedMps, tuning.MaxCaptureSpeedMps, tuning.MaxReverseThrottleNorm * 0.55)
                            : 0.0;

                        return new ArrivalPlan(
                            Phase: ArrivalPhase.CaptureCoast,
                            DesiredSpeedMps: desiredSpeed,
                            StoppingDistanceM: stoppingDistance,
                            ThrottleNorm: turnCaptureThrottle,
                            AllowReverseSurge: tuning.AllowReverseSurge,
                            ShouldCoast: turnCaptureThrottle >= 0.0,
                            ShouldHold: false,
                            IsOvershootLikely: true,
                            SpeedErrorMps: speedError,
                            RecommendedYawGain: 0.90,
                            Reason: turnCaptureThrottle < 0.0
                                ? $"{reasonPrefix}_{profileKind}_CAPTURE_ACTIVE_BRAKE"
                                : $"{reasonPrefix}_{profileKind}_CAPTURE_COAST"
                        );
                    }

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.Capture,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: tuning.CreepThrottleNorm,
                        AllowReverseSurge: tuning.AllowReverseSurge,
                        ShouldCoast: false,
                        ShouldHold: false,
                        IsOvershootLikely: false,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: 0.88,
                        Reason: $"{reasonPrefix}_{profileKind}_CAPTURE_TURN_PASS"
                    );
                }

                if (planarSpeedMps > tuning.MaxCaptureSpeedMps)
                {
                    double captureThrottle = tuning.AllowReverseSurge
                        ? -ComputeBrakeThrottle(planarSpeedMps, tuning.MaxCaptureSpeedMps, tuning.MaxReverseThrottleNorm)
                        : 0.0;

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.CaptureCoast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: captureThrottle,
                        AllowReverseSurge: tuning.AllowReverseSurge,
                        ShouldCoast: captureThrottle >= 0.0,
                        ShouldHold: false,
                        IsOvershootLikely: true,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: strictCapture ? 0.70 : 0.80,
                        Reason: captureThrottle < 0.0
                            ? $"{reasonPrefix}_{profileKind}_CAPTURE_ACTIVE_BRAKE"
                            : $"{reasonPrefix}_{profileKind}_CAPTURE_COAST"
                    );
                }

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Capture,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: tuning.CreepThrottleNorm,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: false,
                    ShouldHold: true,
                    IsOvershootLikely: false,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: strictCapture ? 0.65 : 0.80,
                    Reason: $"{reasonPrefix}_{profileKind}_CAPTURE_CREEP"
                );
            }

            if (cannotStopComfortably && profileKind != ArrivalProfileKind.FlyThrough)
            {
                double stoppingThrottle = tuning.AllowReverseSurge
                    ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, tuning.MaxReverseThrottleNorm)
                    : 0.0;

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Coast,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: stoppingThrottle,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: stoppingThrottle >= 0.0,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: strictCapture ? 0.90 : 0.95,
                    Reason: stoppingThrottle < 0.0
                        ? $"{reasonPrefix}_{profileKind}_STOPPING_DISTANCE_BRAKE"
                        : $"{reasonPrefix}_{profileKind}_STOPPING_DISTANCE_COAST"
                );
            }

            if (distanceM <= tuning.CoastRadiusM && planarSpeedMps > desiredSpeed)
            {
                double nearThrottle;

                if (profileKind == ArrivalProfileKind.FlyThrough)
                {
                    // Türkçe yorum:
                    // Fly-through hedeflerde "hemen frenle" yerine gazı azaltarak akıcı geçiş yapıyoruz.
                    nearThrottle = Math.Clamp(baseThrottleNorm * 0.35, 0.0, tuning.MaxApproachThrottleNorm);
                }
                else
                {
                    nearThrottle = tuning.AllowReverseSurge
                        ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, tuning.MaxReverseThrottleNorm * 0.70)
                        : 0.0;
                }

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Coast,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: nearThrottle,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: nearThrottle >= 0.0 && nearThrottle <= tuning.CreepThrottleNorm,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: profileKind == ArrivalProfileKind.FlyThrough ? 0.92 : 0.90,
                    Reason: nearThrottle < 0.0
                        ? $"{reasonPrefix}_{profileKind}_NEAR_ACTIVE_BRAKE"
                        : $"{reasonPrefix}_{profileKind}_NEAR_FLOW"
                );
            }

            if (distanceM <= tuning.SlowRadiusM)
            {
                if (speedError <= -0.05)
                {
                    double approachBrakeThrottle;

                    if (profileKind == ArrivalProfileKind.FlyThrough)
                    {
                        // Türkçe yorum:
                        // Fly-through modda küçük hız fazlasında negatif fren yerine gaz azaltılır.
                        // Böylece araç her ara checkpointte gereksiz yavaşlamaz.
                        approachBrakeThrottle = Math.Clamp(baseThrottleNorm * 0.45, 0.0, tuning.MaxApproachThrottleNorm);
                    }
                    else
                    {
                        approachBrakeThrottle = tuning.AllowReverseSurge
                            ? -ComputeBrakeThrottle(planarSpeedMps, desiredSpeed, tuning.MaxReverseThrottleNorm * 0.50)
                            : 0.0;
                    }

                    return new ArrivalPlan(
                        Phase: ArrivalPhase.Coast,
                        DesiredSpeedMps: desiredSpeed,
                        StoppingDistanceM: stoppingDistance,
                        ThrottleNorm: approachBrakeThrottle,
                        AllowReverseSurge: tuning.AllowReverseSurge,
                        ShouldCoast: approachBrakeThrottle >= 0.0 && approachBrakeThrottle <= tuning.CreepThrottleNorm,
                        ShouldHold: false,
                        IsOvershootLikely: overshootLikely,
                        SpeedErrorMps: speedError,
                        RecommendedYawGain: profileKind == ArrivalProfileKind.TurnCritical ? 0.98 : 0.94,
                        Reason: approachBrakeThrottle < 0.0
                            ? $"{reasonPrefix}_{profileKind}_APPROACH_ACTIVE_BRAKE"
                            : $"{reasonPrefix}_{profileKind}_APPROACH_FLOW"
                    );
                }

                double distanceFactor = Math.Clamp(distanceM / Math.Max(0.001, tuning.SlowRadiusM), 0.0, 1.0);
                double speedFactor = Math.Clamp(speedError / Math.Max(0.1, desiredSpeed), 0.0, 1.0);

                double approachThrottle = baseThrottleNorm * distanceFactor * speedFactor;

                if (profileKind == ArrivalProfileKind.FlyThrough)
                {
                    approachThrottle = Math.Max(tuning.CreepThrottleNorm, approachThrottle);
                }
                else
                {
                    approachThrottle = Math.Max(tuning.CreepThrottleNorm * 0.85, approachThrottle);
                }

                approachThrottle = Math.Clamp(approachThrottle, 0.0, tuning.MaxApproachThrottleNorm);

                return new ArrivalPlan(
                    Phase: ArrivalPhase.Approach,
                    DesiredSpeedMps: desiredSpeed,
                    StoppingDistanceM: stoppingDistance,
                    ThrottleNorm: approachThrottle,
                    AllowReverseSurge: tuning.AllowReverseSurge,
                    ShouldCoast: false,
                    ShouldHold: false,
                    IsOvershootLikely: overshootLikely,
                    SpeedErrorMps: speedError,
                    RecommendedYawGain: profileKind == ArrivalProfileKind.TurnCritical ? 1.02 : 0.98,
                    Reason: $"{reasonPrefix}_{profileKind}_APPROACH"
                );
            }

            return new ArrivalPlan(
                Phase: ArrivalPhase.Cruise,
                DesiredSpeedMps: desiredSpeed,
                StoppingDistanceM: stoppingDistance,
                ThrottleNorm: Math.Clamp(baseThrottleNorm, 0.0, tuning.MaxApproachThrottleNorm),
                AllowReverseSurge: tuning.AllowReverseSurge,
                ShouldCoast: false,
                ShouldHold: false,
                IsOvershootLikely: false,
                SpeedErrorMps: speedError,
                RecommendedYawGain: profileKind == ArrivalProfileKind.TurnCritical ? 1.05 : 1.0,
                Reason: $"{reasonPrefix}_{profileKind}_NAVIGATE"
            );
        }

        public static ArrivalPlan PlanScenarioArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedCoastDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm)
        {
            // Türkçe yorum:
            // Geriye uyumluluk için eski scenario çağrısı korunur.
            // İkinci dosya güncellemesi yapılana kadar tüm scenario hedefleri TurnCritical gibi davranır.
            // Sonraki adımda AdvancedDecision.Arrival.cs içinden hedef tipine göre
            // FlyThrough / TurnCritical / PrecisionStop geçirilecek.
            return PlanScenarioArrival(
                distanceM: distanceM,
                planarSpeedMps: planarSpeedMps,
                forwardSpeedMps: forwardSpeedMps,
                targetBodyX: targetBodyX,
                headingErrorDeg: headingErrorDeg,
                baseThrottleNorm: baseThrottleNorm,
                captureRadiusM: captureRadiusM,
                slowRadiusM: slowRadiusM,
                coastRadiusM: coastRadiusM,
                maxCaptureSpeedMps: maxCaptureSpeedMps,
                desiredSpeedFloorMps: desiredSpeedFloorMps,
                estimatedCoastDecelMps2: estimatedCoastDecelMps2,
                creepThrottleNorm: creepThrottleNorm,
                maxApproachThrottleNorm: maxApproachThrottleNorm,
                profileKind: ArrivalProfileKind.TurnCritical);
        }

        public static ArrivalPlan PlanScenarioArrival(
            double distanceM,
            double planarSpeedMps,
            double forwardSpeedMps,
            double targetBodyX,
            double headingErrorDeg,
            double baseThrottleNorm,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double estimatedCoastDecelMps2,
            double creepThrottleNorm,
            double maxApproachThrottleNorm,
            ArrivalProfileKind profileKind)
        {
            // Türkçe yorum:
            // Scenario testlerinde reverse fren açık kalır ama önceki 0.22 değeri
            // bazı rota parçalarında fazla agresif davranıp hattı uzatabildi.
            // Bu yüzden daha sakin ve kontrollü bir değer kullanıyoruz.
            const double scenarioReverseThrottleNorm = 0.14;

            bool scenarioAllowReverseSurge = true;

            return PlanMissionArrival(
                distanceM: distanceM,
                planarSpeedMps: planarSpeedMps,
                forwardSpeedMps: forwardSpeedMps,
                targetBodyX: targetBodyX,
                headingErrorDeg: headingErrorDeg,
                baseThrottleNorm: baseThrottleNorm,
                captureRadiusM: captureRadiusM,
                slowRadiusM: slowRadiusM,
                coastRadiusM: coastRadiusM,
                maxCaptureSpeedMps: maxCaptureSpeedMps,
                desiredSpeedFloorMps: desiredSpeedFloorMps,
                estimatedDecelMps2: estimatedCoastDecelMps2,
                creepThrottleNorm: creepThrottleNorm,
                maxApproachThrottleNorm: maxApproachThrottleNorm,
                maxReverseThrottleNorm: scenarioReverseThrottleNorm,
                allowReverseSurge: scenarioAllowReverseSurge,
                strictCapture: profileKind == ArrivalProfileKind.PrecisionStop,
                profileKind: profileKind,
                reasonPrefix: "SCENARIO"
            );
        }

        private static ArrivalProfileTuning CreateProfileTuning(
            ArrivalProfileKind profileKind,
            double captureRadiusM,
            double slowRadiusM,
            double coastRadiusM,
            double maxCaptureSpeedMps,
            double desiredSpeedFloorMps,
            double creepThrottleNorm,
            double maxApproachThrottleNorm,
            double maxReverseThrottleNorm,
            bool allowReverseSurge)
        {
            captureRadiusM = Math.Max(0.05, SafeNonNegative(captureRadiusM));
            slowRadiusM = Math.Max(captureRadiusM, SafeNonNegative(slowRadiusM));
            coastRadiusM = Math.Max(captureRadiusM, SafeNonNegative(coastRadiusM));
            maxCaptureSpeedMps = Math.Max(0.05, SafeNonNegative(maxCaptureSpeedMps));
            desiredSpeedFloorMps = Math.Max(0.0, SafeNonNegative(desiredSpeedFloorMps));
            creepThrottleNorm = SafeNonNegative(creepThrottleNorm);
            maxApproachThrottleNorm = SafeNonNegative(maxApproachThrottleNorm);
            maxReverseThrottleNorm = SafeNonNegative(maxReverseThrottleNorm);

            return profileKind switch
            {
                ArrivalProfileKind.FlyThrough => new ArrivalProfileTuning(
                    CaptureRadiusM: captureRadiusM,
                    SlowRadiusM: Math.Max(slowRadiusM * 0.70, captureRadiusM * 1.60),
                    CoastRadiusM: Math.Max(coastRadiusM * 0.70, captureRadiusM * 1.30),
                    MaxCaptureSpeedMps: Math.Max(maxCaptureSpeedMps * 1.45, maxCaptureSpeedMps + 0.18),
                    DesiredSpeedFloorMps: Math.Max(desiredSpeedFloorMps, maxCaptureSpeedMps * 0.85),
                    CreepThrottleNorm: Math.Max(creepThrottleNorm, maxApproachThrottleNorm * 0.28),
                    MaxApproachThrottleNorm: maxApproachThrottleNorm,
                    MaxReverseThrottleNorm: maxReverseThrottleNorm * 0.35,
                    AllowReverseSurge: allowReverseSurge,
                    TurnAlignHeadingDeg: 110.0,
                    OvershootHeadingDeg: 135.0),

                ArrivalProfileKind.TurnCritical => new ArrivalProfileTuning(
                    CaptureRadiusM: captureRadiusM,
                    SlowRadiusM: Math.Max(slowRadiusM, captureRadiusM * 2.00),
                    CoastRadiusM: Math.Max(coastRadiusM, captureRadiusM * 1.50),
                    MaxCaptureSpeedMps: Math.Max(maxCaptureSpeedMps * 1.10, maxCaptureSpeedMps + 0.05),
                    DesiredSpeedFloorMps: Math.Max(desiredSpeedFloorMps, maxCaptureSpeedMps * 0.55),
                    CreepThrottleNorm: creepThrottleNorm,
                    MaxApproachThrottleNorm: maxApproachThrottleNorm,
                    MaxReverseThrottleNorm: maxReverseThrottleNorm * 0.75,
                    AllowReverseSurge: allowReverseSurge,
                    TurnAlignHeadingDeg: 90.0,
                    OvershootHeadingDeg: 120.0),

                ArrivalProfileKind.PrecisionStop => new ArrivalProfileTuning(
                    CaptureRadiusM: captureRadiusM,
                    SlowRadiusM: slowRadiusM,
                    CoastRadiusM: coastRadiusM,
                    MaxCaptureSpeedMps: maxCaptureSpeedMps,
                    DesiredSpeedFloorMps: desiredSpeedFloorMps,
                    CreepThrottleNorm: creepThrottleNorm,
                    MaxApproachThrottleNorm: maxApproachThrottleNorm,
                    MaxReverseThrottleNorm: maxReverseThrottleNorm,
                    AllowReverseSurge: allowReverseSurge,
                    TurnAlignHeadingDeg: 95.0,
                    OvershootHeadingDeg: 115.0),

                _ => new ArrivalProfileTuning(
                    CaptureRadiusM: captureRadiusM,
                    SlowRadiusM: slowRadiusM,
                    CoastRadiusM: coastRadiusM,
                    MaxCaptureSpeedMps: maxCaptureSpeedMps,
                    DesiredSpeedFloorMps: desiredSpeedFloorMps,
                    CreepThrottleNorm: creepThrottleNorm,
                    MaxApproachThrottleNorm: maxApproachThrottleNorm,
                    MaxReverseThrottleNorm: maxReverseThrottleNorm,
                    AllowReverseSurge: allowReverseSurge,
                    TurnAlignHeadingDeg: 95.0,
                    OvershootHeadingDeg: 115.0)
            };
        }

        private static double ComputeDesiredSpeed(
            double distanceM,
            double captureRadiusM,
            double desiredSpeedFloorMps,
            double decelMps2)
        {
            double brakingDistance = Math.Max(0.0, distanceM - captureRadiusM * 0.65);

            double desired = Math.Sqrt(
                Math.Max(
                    0.0,
                    2.0 * Math.Max(0.001, decelMps2) * brakingDistance
                )
            );

            return Math.Max(desiredSpeedFloorMps, desired);
        }

        private static double ComputeFlyThroughDesiredSpeed(
            double distanceM,
            double captureRadiusM,
            double slowRadiusM,
            double desiredSpeedFloorMps,
            double maxCaptureSpeedMps)
        {
            distanceM = SafeNonNegative(distanceM);
            captureRadiusM = Math.Max(0.05, SafeNonNegative(captureRadiusM));
            slowRadiusM = Math.Max(captureRadiusM, SafeNonNegative(slowRadiusM));

            double normalized = Math.Clamp(distanceM / Math.Max(0.001, slowRadiusM), 0.0, 1.0);
            double throughSpeed = Lerp(maxCaptureSpeedMps, maxCaptureSpeedMps * 1.35, normalized);

            if (distanceM <= captureRadiusM)
                throughSpeed = Math.Max(throughSpeed, maxCaptureSpeedMps * 0.95);

            return Math.Max(desiredSpeedFloorMps, throughSpeed);
        }

        private static double ComputeStoppingDistance(double speedMps, double decelMps2)
        {
            speedMps = SafeNonNegative(speedMps);
            decelMps2 = Math.Max(0.001, SafeNonNegative(decelMps2));
            return (speedMps * speedMps) / (2.0 * decelMps2);
        }

        private static double ComputeBrakeThrottle(
            double currentSpeedMps,
            double desiredSpeedMps,
            double maxReverseThrottleNorm)
        {
            currentSpeedMps = SafeNonNegative(currentSpeedMps);
            desiredSpeedMps = SafeNonNegative(desiredSpeedMps);
            maxReverseThrottleNorm = SafeNonNegative(maxReverseThrottleNorm);

            if (maxReverseThrottleNorm <= 0.0)
                return 0.0;

            if (currentSpeedMps <= desiredSpeedMps)
                return 0.0;

            double error = currentSpeedMps - desiredSpeedMps;
            double k = Math.Clamp(error / Math.Max(0.1, desiredSpeedMps + 0.5), 0.0, 1.0);

            return Math.Clamp(k * maxReverseThrottleNorm, 0.0, maxReverseThrottleNorm);
        }

        private static double ComputeRecoveryThrottle(
            bool allowReverseSurge,
            double forwardSpeedMps,
            double maxReverseThrottleNorm)
        {
            if (!allowReverseSurge)
                return 0.0;

            if (forwardSpeedMps <= 0.10)
                return 0.0;

            double k = Math.Clamp(forwardSpeedMps / 1.50, 0.0, 1.0);
            return -Math.Clamp(k * maxReverseThrottleNorm, 0.0, maxReverseThrottleNorm);
        }

        private static double Lerp(double a, double b, double t)
        {
            t = Math.Clamp(Safe(t), 0.0, 1.0);
            return a + (b - a) * t;
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value)
        {
            if (!double.IsFinite(value))
                return 0.0;

            return Math.Max(0.0, value);
        }

        private sealed record ArrivalProfileTuning(
            double CaptureRadiusM,
            double SlowRadiusM,
            double CoastRadiusM,
            double MaxCaptureSpeedMps,
            double DesiredSpeedFloorMps,
            double CreepThrottleNorm,
            double MaxApproachThrottleNorm,
            double MaxReverseThrottleNorm,
            bool AllowReverseSurge,
            double TurnAlignHeadingDeg,
            double OvershootHeadingDeg);
    }
}