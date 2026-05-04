using System;
using Hydronom.Core.Domain;

namespace Hydronom.Core.Modules
{
    public partial class AdvancedDecision
    {
        private void ResetControllerState()
        {
            ResetHeaveIntegral();
            _frozenHoldHeadingDeg = null;
            _lastTarget = null;
            _isHoldingPosition = false;
        }

        private void HandleTargetChange(Vec3 currentTarget)
        {
            if (_lastTarget is null)
            {
                _lastTarget = currentTarget;
                return;
            }

            bool changed =
                Math.Abs(_lastTarget.Value.X - currentTarget.X) > 1e-6 ||
                Math.Abs(_lastTarget.Value.Y - currentTarget.Y) > 1e-6 ||
                Math.Abs(_lastTarget.Value.Z - currentTarget.Z) > 1e-6;

            if (changed)
            {
                ResetHeaveIntegral();
                _frozenHoldHeadingDeg = null;
                _isHoldingPosition = false;
                _lastTarget = currentTarget;
            }
        }

        private static double Normalize(double deg)
        {
            if (!double.IsFinite(deg))
                return 0.0;

            deg %= 360.0;

            if (deg > 180.0)
                deg -= 360.0;

            if (deg < -180.0)
                deg += 360.0;

            return deg;
        }

        private static double SanitizeDt(double dt)
        {
            if (!double.IsFinite(dt))
                return 0.1;

            if (dt <= 1e-4)
                return 1e-4;

            if (dt > 0.25)
                return 0.25;

            return dt;
        }

        private static Vec3 SanitizeVec(Vec3 v)
        {
            return new Vec3(
                Safe(v.X),
                Safe(v.Y),
                Safe(v.Z)
            );
        }

        private static double Safe(double value)
        {
            return double.IsFinite(value) ? value : 0.0;
        }

        private static double SafeNonNegative(double value, double fallback)
        {
            if (!double.IsFinite(value))
                return fallback;

            return Math.Max(0.0, value);
        }

        private static double HeadingScale(double absDelta)
        {
            if (absDelta <= 10.0) return 1.0;
            if (absDelta >= 90.0) return 0.22;

            double x = (absDelta - 10.0) / 80.0;
            double s = x * x * (3.0 - 2.0 * x);

            return 1.0 + (0.22 - 1.0) * s;
        }

        private static double HeadingThrottleGate(double absDeltaDeg, double absYawRateDeg)
        {
            double gate;

            if (absDeltaDeg >= 150.0)
                gate = 0.03;
            else if (absDeltaDeg >= 120.0)
                gate = 0.10;
            else if (absDeltaDeg >= 90.0)
                gate = 0.25;
            else if (absDeltaDeg >= 60.0)
                gate = 0.55;
            else
                gate = 1.0;

            if (absYawRateDeg > 50.0)
                gate *= 0.25;
            else if (absYawRateDeg > 25.0)
                gate *= 0.55;

            return gate;
        }

        private static double ComputeApproachBrakeNorm(double dist, double forwardSpeed)
        {
            if (dist >= BrakeRadiusM)
                return 0.0;

            if (forwardSpeed <= BrakeSpeedStartMps)
                return 0.0;

            double distFactor = 1.0 - Math.Clamp((dist - StopRadiusM) / (BrakeRadiusM - StopRadiusM), 0.0, 1.0);
            double speedFactor = Math.Clamp(
                (forwardSpeed - BrakeSpeedStartMps) / (BrakeSpeedFullMps - BrakeSpeedStartMps),
                0.0,
                1.0
            );

            double brake = distFactor * speedFactor * MaxReverseThrottleNorm;
            return Math.Clamp(brake, 0.0, MaxReverseThrottleNorm);
        }
    }
}