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
                return AdaptiveArrivalPlanner.PlanScenarioArrival(
                    distanceM: nav.DistanceXY,
                    planarSpeedMps: nav.PlanarSpeedMps,
                    forwardSpeedMps: nav.ForwardSpeedMps,
                    targetBodyX: nav.TargetBody.X,
                    headingErrorDeg: nav.HeadingErrorDeg,
                    baseThrottleNorm: baseThrottleNorm,
                    captureRadiusM: ScenarioCaptureRadiusM,
                    slowRadiusM: ScenarioSlowRadiusM * caution,
                    coastRadiusM: ScenarioCoastRadiusM * caution,
                    maxCaptureSpeedMps: ScenarioMaxCaptureSpeedMps * advice.MaxSpeedScale,
                    desiredSpeedFloorMps: ScenarioDesiredSpeedFloorMps,
                    estimatedCoastDecelMps2: ScenarioEstimatedCoastDecelMps2,
                    creepThrottleNorm: ScenarioCreepThrottleNorm,
                    maxApproachThrottleNorm: ScenarioMaxApproachThrottleNorm * advice.ThrottleScale
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
    }
}