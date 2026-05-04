using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private DecisionCommand ReportAndReturn(
            DecisionMode mode,
            string reason,
            DecisionCommand rawCommand,
            DecisionCommand outputCommand,
            VehicleState state,
            Vec3? target,
            double distanceXY,
            double headingErrorDeg,
            double forwardSpeedMps,
            double yawRateDeg,
            bool obstacleAhead,
            double throttleNorm = 0.0,
            double rudderNorm = 0.0)
        {
            LastDecisionReport = new AdvancedDecisionReport(
                Mode: mode,
                Reason: reason,
                Target: target,
                Position: state.Position,
                DistanceXY: SafeNonNegative(distanceXY, 0.0),
                HeadingErrorDeg: Safe(headingErrorDeg),
                ForwardSpeedMps: Safe(forwardSpeedMps),
                YawRateDeg: Safe(yawRateDeg),
                ObstacleAhead: obstacleAhead,
                IsHoldingPosition: _isHoldingPosition,
                FrozenHoldHeadingDeg: _frozenHoldHeadingDeg,
                ThrottleNorm: Math.Clamp(Safe(throttleNorm), -1.0, 1.0),
                RudderNorm: Math.Clamp(Safe(rudderNorm), -1.0, 1.0),
                RawCommand: rawCommand,
                OutputCommand: outputCommand
            );

            return outputCommand;
        }
    }
}