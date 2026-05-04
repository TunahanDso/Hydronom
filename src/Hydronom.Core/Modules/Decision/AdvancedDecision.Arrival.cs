using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private static ArrivalPlan PlanMissionArrival(
            TaskDefinition task,
            double baseThrottleNorm,
            NavigationGeometry nav)
        {
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
                    slowRadiusM: ScenarioSlowRadiusM,
                    coastRadiusM: ScenarioCoastRadiusM,
                    maxCaptureSpeedMps: ScenarioMaxCaptureSpeedMps,
                    desiredSpeedFloorMps: ScenarioDesiredSpeedFloorMps,
                    estimatedCoastDecelMps2: ScenarioEstimatedCoastDecelMps2,
                    creepThrottleNorm: ScenarioCreepThrottleNorm,
                    maxApproachThrottleNorm: ScenarioMaxApproachThrottleNorm
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
                slowRadiusM: GeneralArrivalSlowRadiusM,
                coastRadiusM: GeneralArrivalCoastRadiusM,
                maxCaptureSpeedMps: GeneralMaxCaptureSpeedMps,
                desiredSpeedFloorMps: GeneralDesiredSpeedFloorMps,
                estimatedDecelMps2: GeneralEstimatedDecelMps2,
                creepThrottleNorm: GeneralCreepThrottleNorm,
                maxApproachThrottleNorm: GeneralMaxApproachThrottleNorm,
                maxReverseThrottleNorm: MaxReverseThrottleNorm,
                allowReverseSurge: true,
                strictCapture: false,
                reasonPrefix: "MISSION"
            );
        }
    }
}