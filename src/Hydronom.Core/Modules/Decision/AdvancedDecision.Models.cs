using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public enum DecisionMode
    {
        Idle = 0,
        Navigate = 1,
        Avoid = 2,
        Hold = 3
    }

    public enum ArrivalPhase
    {
        Cruise = 0,
        Approach = 1,
        Coast = 2,
        Capture = 3,
        CaptureCoast = 4,
        TurnAlign = 5,
        OvershootRecovery = 6,
        Hold = 7
    }

    public readonly record struct AdvancedDecisionReport(
        DecisionMode Mode,
        string Reason,
        Vec3? Target,
        Vec3 Position,
        double DistanceXY,
        double HeadingErrorDeg,
        double ForwardSpeedMps,
        double YawRateDeg,
        bool ObstacleAhead,
        bool IsHoldingPosition,
        double? FrozenHoldHeadingDeg,
        double ThrottleNorm,
        double RudderNorm,
        DecisionCommand RawCommand,
        DecisionCommand OutputCommand
    )
    {
        public static AdvancedDecisionReport Empty { get; } =
            new(
                Mode: DecisionMode.Idle,
                Reason: "NOT_COMPUTED",
                Target: null,
                Position: Vec3.Zero,
                DistanceXY: 0.0,
                HeadingErrorDeg: 0.0,
                ForwardSpeedMps: 0.0,
                YawRateDeg: 0.0,
                ObstacleAhead: false,
                IsHoldingPosition: false,
                FrozenHoldHeadingDeg: null,
                ThrottleNorm: 0.0,
                RudderNorm: 0.0,
                RawCommand: DecisionCommand.Zero,
                OutputCommand: DecisionCommand.Zero
            );

        public override string ToString()
        {
            return
                $"Decision mode={Mode} reason={Reason} " +
                $"dist={DistanceXY:F2}m dHead={HeadingErrorDeg:F1}° " +
                $"vFwd={ForwardSpeedMps:F2}m/s yawRate={YawRateDeg:F1}°/s " +
                $"obs={ObstacleAhead} hold={IsHoldingPosition} " +
                $"thr={ThrottleNorm:F2} rud={RudderNorm:F2}";
        }
    }

    public readonly record struct ArrivalPlan(
        ArrivalPhase Phase,
        double DesiredSpeedMps,
        double StoppingDistanceM,
        double ThrottleNorm,
        bool AllowReverseSurge,
        bool ShouldCoast,
        bool ShouldHold,
        bool IsOvershootLikely,
        string Reason
    );

    public partial class AdvancedDecision
    {
        private readonly record struct NavigationGeometry(
            double DistanceXY,
            Vec3 TargetBody,
            Vec3 VelocityBody,
            double HeadingErrorDeg,
            double ForwardSpeedMps,
            double PlanarSpeedMps,
            double YawRateDeg
        );

        private readonly record struct DecisionResult(
            DecisionCommand RawCommand,
            DecisionCommand OutputCommand,
            string Reason,
            double ThrottleNorm,
            double RudderNorm
        );
    }
}