namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private static ArrivalPlan PlanScenarioArrival(
            double baseThrottleNorm,
            NavigationGeometry nav)
        {
            return AdaptiveArrivalPlanner.PlanScenarioArrival(
                distanceM: nav.DistanceXY,
                planarSpeedMps: nav.PlanarSpeedMps,
                forwardSpeedMps: nav.ForwardSpeedMps,
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
    }
}