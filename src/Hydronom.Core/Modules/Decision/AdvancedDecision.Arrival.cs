using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private static ArrivalPlan PlanMissionArrival(
            TaskDefinition task,
            double baseThrottleNorm,
            NavigationGeometry nav,
            DecisionAdviceProfile advice)
        {
            advice = advice.Sanitized();

            /*
             * Analysis tavsiyesi arrival davranışını daha temkinli yapabilir.
             * ArrivalCautionScale büyüdükçe:
             * - yavaşlama yarıçapı büyür
             * - coast daha erken başlar
             * - capture speed düşer
             */
            double caution = advice.ArrivalCautionScale;

            if (task.IsExternallyCompleted)
            {
                /*
                 * Scenario görevlerinde artık tüm checkpointlere aynı "dur-yavaşla-yaklaş"
                 * mantığı uygulanmaz.
                 *
                 * FlyThrough:
                 * - Erken/ara checkpointler için kullanılır.
                 * - Araç hedef alanına girince gereksiz hold/creep yapmadan akıcı geçer.
                 *
                 * TurnCritical:
                 * - U dönüş, keskin dönüş ve hizalanma gerektiren orta-son hedeflerde kullanılır.
                 * - Araç tamamen durmaz ama heading hatası büyürse daha kontrollü davranır.
                 *
                 * PrecisionStop:
                 * - Final hedefte kullanılır.
                 * - Hassas yaklaşma, düşük hız ve settle korunur.
                 */
                ArrivalProfileKind scenarioProfile = ResolveScenarioArrivalProfile(task, nav);

                double profileCaptureRadius = ScenarioCaptureRadiusM;
                double profileSlowRadius = ScenarioSlowRadiusM * caution;
                double profileCoastRadius = ScenarioCoastRadiusM * caution;
                double profileMaxCaptureSpeed = ScenarioMaxCaptureSpeedMps * advice.MaxSpeedScale;
                double profileDesiredSpeedFloor = ScenarioDesiredSpeedFloorMps;
                double profileCreepThrottle = ScenarioCreepThrottleNorm;
                double profileMaxApproachThrottle = ScenarioMaxApproachThrottleNorm * advice.ThrottleScale;

                /*
                 * Fly-through hedeflerde asıl amaç checkpoint içinde durmak değil,
                 * checkpoint alanından güvenli şekilde akıp sıradaki hedefe geçmektir.
                 * Bu yüzden capture speed ve hız tabanı biraz yükseltilir.
                 */
                if (scenarioProfile == ArrivalProfileKind.FlyThrough)
                {
                    profileMaxCaptureSpeed = Math.Max(profileMaxCaptureSpeed, ScenarioMaxCaptureSpeedMps * 1.35);
                    profileDesiredSpeedFloor = Math.Max(profileDesiredSpeedFloor, ScenarioDesiredSpeedFloorMps * 1.25);
                    profileCreepThrottle = Math.Max(profileCreepThrottle, ScenarioCreepThrottleNorm * 1.20);

                    /*
                     * Ara checkpointlerde çok erken yavaşlamayı azaltıyoruz.
                     * Böylece araç her waypointte "park etme" refleksine girmiyor.
                     */
                    profileSlowRadius = Math.Max(ScenarioCaptureRadiusM * 1.60, profileSlowRadius * 0.75);
                    profileCoastRadius = Math.Max(ScenarioCaptureRadiusM * 1.30, profileCoastRadius * 0.75);
                }
                else if (scenarioProfile == ArrivalProfileKind.TurnCritical)
                {
                    /*
                     * Dönüş kritik hedeflerde araç daha canlı kalabilir ama
                     * heading toparlama için yavaşlama alanını tamamen kaldırmıyoruz.
                     */
                    profileMaxCaptureSpeed = Math.Max(profileMaxCaptureSpeed, ScenarioMaxCaptureSpeedMps * 1.15);
                    profileDesiredSpeedFloor = Math.Max(profileDesiredSpeedFloor, ScenarioDesiredSpeedFloorMps * 1.10);
                    profileSlowRadius = Math.Max(ScenarioCaptureRadiusM * 2.00, profileSlowRadius);
                    profileCoastRadius = Math.Max(ScenarioCaptureRadiusM * 1.50, profileCoastRadius);
                }
                else
                {
                    /*
                     * Final hedefte önceki güvenli yaklaşma mantığı korunur.
                     * Burada süre kazanmak yerine doğru ve temiz bitiriş önceliklidir.
                     */
                    profileMaxCaptureSpeed = Math.Max(0.05, profileMaxCaptureSpeed);
                }

                return AdaptiveArrivalPlanner.PlanScenarioArrival(
                    distanceM: nav.DistanceXY,
                    planarSpeedMps: nav.PlanarSpeedMps,
                    forwardSpeedMps: nav.ForwardSpeedMps,
                    targetBodyX: nav.TargetBody.X,
                    headingErrorDeg: nav.HeadingErrorDeg,
                    baseThrottleNorm: baseThrottleNorm,
                    captureRadiusM: profileCaptureRadius,
                    slowRadiusM: profileSlowRadius,
                    coastRadiusM: profileCoastRadius,
                    maxCaptureSpeedMps: profileMaxCaptureSpeed,
                    desiredSpeedFloorMps: profileDesiredSpeedFloor,
                    estimatedCoastDecelMps2: ScenarioEstimatedCoastDecelMps2,
                    creepThrottleNorm: profileCreepThrottle,
                    maxApproachThrottleNorm: profileMaxApproachThrottle,
                    profileKind: scenarioProfile
                );
            }

            return AdaptiveArrivalPlanner.PlanMissionArrival(
                distanceM: nav.DistanceXY,
                planarSpeedMps: nav.PlanarSpeedMps,
                forwardSpeedMps: nav.ForwardSpeedMps,
                targetBodyX: nav.TargetBody.X,
                headingErrorDeg: nav.HeadingErrorDeg,
                baseThrottleNorm: baseThrottleNorm,
                captureRadiusM: GeneralArrivalCaptureRadiusM,
                slowRadiusM: GeneralArrivalSlowRadiusM * caution,
                coastRadiusM: GeneralArrivalCoastRadiusM * caution,
                maxCaptureSpeedMps: GeneralMaxCaptureSpeedMps * advice.MaxSpeedScale,
                desiredSpeedFloorMps: GeneralDesiredSpeedFloorMps,
                estimatedDecelMps2: GeneralEstimatedDecelMps2,
                creepThrottleNorm: GeneralCreepThrottleNorm,
                maxApproachThrottleNorm: GeneralMaxApproachThrottleNorm * advice.ThrottleScale,
                maxReverseThrottleNorm: MaxReverseThrottleNorm,
                allowReverseSurge: true,
                strictCapture: false,
                reasonPrefix: "MISSION"
            );
        }

        private static ArrivalProfileKind ResolveScenarioArrivalProfile(
            TaskDefinition task,
            NavigationGeometry nav)
        {
            /*
             * TaskDefinition modelinde Id/Title/Description alanları yok.
             * Bu yüzden scenario waypoint profilini aktif hedef koordinatına göre çözüyoruz.
             *
             * TEKNOFEST S-Slalom-U dönüş parkuru:
             * WP-1..WP-6  -> FlyThrough
             * WP-7..WP-9  -> TurnCritical
             * WP-10       -> PrecisionStop
             *
             * İleride ScenarioObjective metadata içine doğrudan profile/passMode eklenirse
             * bu koordinat bazlı çözüm yerine metadata bazlı çözüm kullanılabilir.
             */
            if (task.Target is not Vec3 target)
                return ArrivalProfileKind.TurnCritical;

            double x = target.X;
            double y = target.Y;

            // WP-10 / Final: (76, -10)
            if (IsNear(x, y, 76.0, -10.0, tolerance: 0.75))
                return ArrivalProfileKind.PrecisionStop;

            // WP-7, WP-8, WP-9: U dönüş ve final yaklaşma hattı.
            if (IsNear(x, y, 96.0, 8.0, tolerance: 0.75) ||
                IsNear(x, y, 104.0, 0.0, tolerance: 0.75) ||
                IsNear(x, y, 92.0, -8.0, tolerance: 0.75))
            {
                return ArrivalProfileKind.TurnCritical;
            }

            // Diğer scenario checkpointleri akıcı geçiş noktasıdır.
            return ArrivalProfileKind.FlyThrough;
        }

        private static bool IsNear(
            double x,
            double y,
            double targetX,
            double targetY,
            double tolerance)
        {
            double dx = x - targetX;
            double dy = y - targetY;

            return (dx * dx + dy * dy) <= tolerance * tolerance;
        }
    }
}